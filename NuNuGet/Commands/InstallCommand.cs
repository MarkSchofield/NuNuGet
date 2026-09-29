namespace NuNuGet.Commands;

using Microsoft.Extensions.Logging;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Signing;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuNuGet.Models;
using NuNuGet.Options;
using System.CommandLine;
using System.Text.Json;

using Logging = Microsoft.Extensions.Logging;

internal static class PackageEntryExtensions
{
    public static LibraryDependency ToLibraryDependency(this PackageEntry package)
    {
        return package.ToLibraryDependency(null);
    }

    /// <summary>
    /// Creates the <see cref="LibraryDependency"/> for a package entry. If there is a lock file, the dependency is
    /// pinned to the exact version that the lock file resolved, so that new versions on the feed are never considered.
    /// </summary>
    public static LibraryDependency ToLibraryDependency(this PackageEntry package, PackagesLockFile? lockFile)
    {
        VersionRange range = VersionRange.Parse(package.Version);

        LockFileDependency? locked = lockFile?.Targets[0].Dependencies
            .FirstOrDefault(d => string.Equals(d.Id, package.Id, StringComparison.OrdinalIgnoreCase));
        if (locked is not null)
        {
            return InstallCommand.PinnedDependency(package.Id, locked.ResolvedVersion);
        }

        return new()
        {
            LibraryRange = new(package.Id, range, LibraryDependencyTarget.Package)
        };
    }
}

/// <summary>
/// A command that installs packages from a packages.list.json file to the global packages folder, using a lock file to ensure repeatable restores and to write out the resolved package graph.
/// </summary>
internal sealed class InstallCommand : Command
{
    private static readonly NuGetFramework[] SpecialTargetFrameworkFallbacks =
    [
        NuGetFramework.Parse("net10.0"),
        NuGetFramework.Parse("net461"),
    ];

    private ILoggerFactory LoggerFactory { get; }

    private Logging.ILogger Logger { get; set; } = Logging.Abstractions.NullLogger.Instance;

    private string WorkingDirectory { get; } = Directory.GetCurrentDirectory();

    private FileInfo? ConfigFile { get; set; }

    private FileInfo? ListFile { get; set; }

    private string LockFile { get; set; } = string.Empty;

    private string ProjectName { get; set; } = "NuNuGet";

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallCommand"/> class.
    /// </summary>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to create loggers.</param>
    public InstallCommand(ILoggerFactory loggerFactory)
        : base("install", "Install packages from a packages.list.json file to the global packages folder")
    {
        this.LoggerFactory = loggerFactory;

        this.Add(ConfigFileOption.Instance);
        this.Add(ListFileOption.Instance);
        this.Add(LockFileOption.Instance);
        this.Add(VerboseOption.Instance);

        this.SetAction(this.Invoke);
    }

    private void ParseOptions(ParseResult parseResult)
    {
        this.LockFile = parseResult.GetValue(LockFileOption.Instance) ?? throw new InvalidOperationException("The --lockFile option is required.");
        if (!Path.IsPathFullyQualified(this.LockFile))
        {
            this.LockFile = Path.Combine(this.WorkingDirectory, this.LockFile);
        }
        this.LockFile = Path.GetFullPath(this.LockFile, this.WorkingDirectory);

        this.ConfigFile = parseResult.GetValue(ConfigFileOption.Instance) ?? throw new InvalidOperationException("The --configFile option is required.");
        if (!this.ConfigFile.Exists)
        {
            throw new InvalidOperationException("Config file does not exist.");
        }

        this.ListFile = parseResult.GetValue(ListFileOption.Instance) ?? throw new InvalidOperationException("The --listFile option is required.");
        if (!this.ListFile.Exists)
        {
            throw new InvalidOperationException("Package list file does not exist.");
        }
    }

    private ISettings LoadSettings()
    {
        return Settings.LoadSpecificSettings(this.ConfigFile!.DirectoryName!, this.ConfigFile!.Name);
    }

    private static PackageList LoadPackageList(FileInfo listFile)
    {
        using FileStream packageListStream = listFile.OpenRead();
        PackageList? packageList = JsonSerializer.Deserialize(packageListStream, PackageListJsonContext.Default.PackageList);

        return packageList ?? throw new InvalidOperationException("Failed to deserialize PackageList file.");
    }

    private static NuGetFramework BuildRestoreFramework(string targetFramework)
    {
        NuGetFramework framework = NuGetFramework.Parse(targetFramework);

        if (!string.Equals(targetFramework, "any", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(targetFramework, "native", StringComparison.OrdinalIgnoreCase))
        {
            return framework;
        }

        return new FallbackFramework(framework, [.. SpecialTargetFrameworkFallbacks]);
    }

    /// <summary>
    /// Determines whether the lock file still matches the package list: the same target framework, and the same set of
    /// direct dependencies with the same requested version ranges.
    /// </summary>
    private static bool IsLockFileCurrent(PackagesLockFile lockFile, PackageList packageList)
    {
        if (lockFile.Targets.Count != 1)
        {
            return false;
        }

        PackagesLockFileTarget target = lockFile.Targets[0];
        NuGetFramework expectedFramework = BuildRestoreFramework(packageList.TargetFramework);
        if (!string.Equals(target.TargetFramework.DotNetFrameworkName, expectedFramework.DotNetFrameworkName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Dictionary<string, LockFileDependency> direct = target.Dependencies
            .Where(d => d.Type == PackageDependencyType.Direct)
            .ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

        if (direct.Count != packageList.Packages.Count)
        {
            return false;
        }

        return packageList.Packages.All(p =>
            direct.TryGetValue(p.Id, out LockFileDependency? dependency)
            && dependency.RequestedVersion is not null
            && dependency.RequestedVersion.Equals(VersionRange.Parse(p.Version)));
    }

    /// <summary>
    /// Determines whether the packages restored match the lock file exactly: the same ids, versions and content hashes.
    /// </summary>
    private static bool MatchesLockFile(PackagesLockFile restored, PackagesLockFile lockFile)
    {
        IList<LockFileDependency> expected = lockFile.Targets[0].Dependencies;
        IList<LockFileDependency> actual = restored.Targets.Count == 1 ? restored.Targets[0].Dependencies : [];

        if (expected.Count != actual.Count)
        {
            return false;
        }

        Dictionary<string, LockFileDependency> actualById = actual.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
        return expected.All(e =>
            actualById.TryGetValue(e.Id, out LockFileDependency? a)
            && a.ResolvedVersion == e.ResolvedVersion
            && string.Equals(a.ContentHash, e.ContentHash, StringComparison.Ordinal));
    }

    /// <summary>
    /// Pins the transitive packages in the lock file to their exact locked versions. Without this, a transitive
    /// dependency whose parent requests a lower minimum version than the locked one would not be found in the global
    /// packages folder, and NuGet would go to the package sources to resolve it (which fails when offline, and could
    /// pick a different version).
    /// </summary>
    private static IEnumerable<LibraryDependency> TransitivePins(PackagesLockFile? lockFile)
    {
        if (lockFile is null)
        {
            return [];
        }

        return lockFile.Targets[0].Dependencies
            .Where(d => d.Type != PackageDependencyType.Direct)
            .Select(d => PinnedDependency(d.Id, d.ResolvedVersion));
    }

    internal static LibraryDependency PinnedDependency(string id, NuGetVersion version)
    {
        return new()
        {
            LibraryRange = new(id, new VersionRange(version, includeMinVersion: true, version, includeMaxVersion: true), LibraryDependencyTarget.Package)
        };
    }

    private PackageSpec BuildPackageSpec(PackageList packageList, string globalPackagesPath, PackagesLockFile? existingLockFile)
    {
        PackageSpec packageSpec = new()
        {
            Name = this.ProjectName,
            FilePath = Path.Combine(this.WorkingDirectory, this.ProjectName + ".csproj"),
            RestoreMetadata = new ProjectRestoreMetadata
            {
                ProjectName = this.ProjectName,
                ProjectUniqueName = this.ProjectName,
                CacheFilePath = null,
                ProjectPath = Path.Combine(this.WorkingDirectory, this.ProjectName + ".csproj"),
                OutputPath = Path.Combine(this.WorkingDirectory, "obj"),
                PackagesPath = globalPackagesPath,
                ProjectStyle = ProjectStyle.PackageReference,
                // When there is an existing lock file, the versions are pinned explicitly (see 'ToLibraryDependency')
                // rather than relying on NuGet's locked mode, which silently stops pinning for 'FallbackFramework'
                // target frameworks ('any' and 'native') and then fails with a content hash validation error.
                RestoreLockProperties = existingLockFile is null
                    ? new RestoreLockProperties(restorePackagesWithLockFile: "True", nuGetLockFilePath: this.LockFile, restoreLockedMode: false)
                    : new RestoreLockProperties(restorePackagesWithLockFile: null, nuGetLockFilePath: null, restoreLockedMode: false),

                // The vulnerability audit queries the package sources, which is slow and produces warnings when they
                // are unreachable. When there is a lock file the packages are already pinned and nothing new is being
                // resolved, so only audit when there is no lock file (i.e. when resolving the packages for the first time).
                RestoreAuditProperties = existingLockFile is null
                    ? null
                    : new RestoreAuditProperties { EnableAudit = "false" },
            },
        };

        packageSpec.TargetFrameworks.Add(new TargetFrameworkInformation
        {
            FrameworkName = BuildRestoreFramework(packageList.TargetFramework),
            Dependencies = [.. packageList.Packages.Select(p => p.ToLibraryDependency(existingLockFile)), .. TransitivePins(existingLockFile)]
        });

        return packageSpec;
    }

    private void WriteLockFile(PackagesLockFile packagesLockFile)
    {
        string newLockFileContent = PackagesLockFileFormat.Render(packagesLockFile);

        if (File.Exists(this.LockFile))
        {
            FileInfo existingLockFile = new FileInfo(this.LockFile);

            if (existingLockFile.Length == newLockFileContent.Length)
            {
                if (newLockFileContent == File.ReadAllText(this.LockFile))
                {
                    // Nothing has changed, no need to update the lock file on disk.
                    return;
                }
            }
        }

        File.WriteAllText(this.LockFile, newLockFileContent);
    }

    private static int ReportStaleLockFile()
    {
        Console.Error.WriteLine("Restore failed due to a mismatch between the package list and the lock file. Delete the lock file to force a rebuild.");
        return 100;
    }

    private async Task<int> Invoke(ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (parseResult.GetValue(VerboseOption.Instance))
        {
            this.Logger = this.LoggerFactory.CreateLogger<InstallCommand>();
        }

        this.ParseOptions(parseResult);

        ISettings settings = this.LoadSettings();
        string globalPackagesPath = SettingsUtility.GetGlobalPackagesFolder(settings);

        // Load and deserialize the package list JSON file
        PackageList packageList = LoadPackageList(this.ListFile!);

        // If there is a lock file then it pins the restore, provided it is still current for the package list.
        PackagesLockFile? existingLockFile = null;
        if (File.Exists(this.LockFile))
        {
            existingLockFile = PackagesLockFileFormat.Read(this.LockFile, new NuGetLoggerAdapter(this.Logger));
            if (!IsLockFileCurrent(existingLockFile, packageList))
            {
                return ReportStaleLockFile();
            }
        }

        PackageSpec packageSpec = this.BuildPackageSpec(packageList, globalPackagesPath, existingLockFile);
        DependencyGraphSpec dependencyGraphSpec = new();
        dependencyGraphSpec.AddProject(packageSpec);
        dependencyGraphSpec.AddRestore(packageSpec.RestoreMetadata.ProjectUniqueName);

        IEnumerable<Lazy<INuGetResourceProvider>> providers = Repository.Provider.GetCoreV3();
        RestoreCommandProvidersCache restoreCommandProvidersCache = new();
        SourceCacheContext cacheContext = new();
        PackageSourceProvider sourceProvider = new(settings);
        LockFileBuilderCache lockFileBuilderCache = new();
        List<SourceRepository> repositories = [.. sourceProvider.LoadPackageSources().Where(s => s.IsEnabled).Select(packageSource => new SourceRepository(packageSource, providers))];
        NuGetLoggerAdapter nugetLogger = new(this.Logger);
        ClientPolicyContext clientPolicyContext = ClientPolicyContext.GetClientPolicy(settings, nugetLogger);
        RestoreCommandProviders dependencyProviders = restoreCommandProvidersCache.GetOrCreate(
            globalPackagesPath: globalPackagesPath,
            fallbackPackagesPaths: [],
            sources: repositories,
            cacheContext: cacheContext,
            log: nugetLogger
            );

        PackageSourceMapping packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
        RestoreRequest request = new(packageSpec, dependencyProviders, cacheContext, clientPolicyContext, packageSourceMapping, nugetLogger, lockFileBuilderCache)
        {
            AllowNoOp = true,
            ProjectStyle = ProjectStyle.PackageReference,
            DependencyGraphSpec = dependencyGraphSpec,
        };

        // Run the RestoreCommand
        RestoreCommand command = new(request);
        RestoreResult result = await command.ExecuteAsync(cancellationToken);

        if (!result.Success)
        {
            // If result.LogMessages contains a 'NU1004', then the ListFile has deviated from the LockFile.
            bool staleLockFile = result.LogMessages.Any(m => m.Code == NuGetLogCode.NU1004);
            if (staleLockFile)
            {
                return ReportStaleLockFile();
            }

            throw new InvalidOperationException("Restore failed:\n" + string.Join("\n", result.LogMessages.Select(m => m.Message)));
        }

        // Write warnings to Console.Error
        foreach (var warning in result.LogMessages.Where(m => m.Level == NuGet.Common.LogLevel.Warning))
        {
            Console.Error.WriteLine(warning.Message);
            // Look for audit errors?
        }

        // Write output files
        //
        // Note: Ideally, this would write out the 'cache file' as well, but that is currently not supported by the
        // public API. The cache file is used to speed up subsequent restores by caching information about the remote
        // sources, so it is not strictly necessary to write it out for the install command to function correctly.

        // If there was no lock file, write the one that the restore produced. Otherwise the restore was pinned to the
        // existing lock file: check that it restored exactly what the lock file says, and use the lock file as-is.
        PackagesLockFile packagesLockFile;
        PackagesLockFile restoredLockFile = new PackagesLockFileBuilder().CreateNuGetLockFile(result.LockFile);
        if (existingLockFile is null)
        {
            packagesLockFile = restoredLockFile;

            this.WriteLockFile(packagesLockFile);
        }
        else
        {
            if (!MatchesLockFile(restoredLockFile, existingLockFile))
            {
                Console.Error.WriteLine("Restore produced packages that differ from the lock file (a package or its content hash has changed). Delete the lock file to force a rebuild.");
                return 100;
            }

            packagesLockFile = existingLockFile;
        }

        // Write out the metadata for consumption
        Console.WriteLine($"GlobalPackagesPath: {globalPackagesPath}");

        // Write the reverse-topological install order
        foreach (var (id, version) in Traversal.ReverseTopological(packagesLockFile))
        {
            Console.WriteLine($"Package: {id}/{version}");
        }

        return 0;
    }
}
