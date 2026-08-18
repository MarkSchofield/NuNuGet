namespace NuNuGet.Tests;

using NuNuGet.Models;
using System.IO;

using static NuNuGet.Tests.Helper;

internal static class TestEnvironment
{
    public static readonly string TestFolder = AppContext.BaseDirectory;

    public static readonly string RepositoryRoot = Path.GetFullPath(Git.GetRepositoryRoot(TestFolder));

    public static readonly string BuiltPackagesFolder = Path.Combine(RepositoryRoot, "__artifacts", "package", Build.GetConfiguration().ToLowerInvariant());

    public static readonly string Package050 = Path.Combine(BuiltPackagesFolder, "NuNuGet.Reference.0.5.0.nupkg");

    public static readonly string Package060 = Path.Combine(BuiltPackagesFolder, "NuNuGet.Reference.0.6.0.nupkg");
}

// The tests in this class share a single 'ReferenceFolder' on disk (nuget.config, packages.lock.json, etc.),
// so they must not run concurrently with each other.
[NotInParallel(nameof(ScenarioTests))]
public class ScenarioTests
{
    private static readonly string NuNuGetExecutableName = OperatingSystem.IsWindows() ? "NuNuGet.exe" : "NuNuGet";

    private ProcessManagement ProcessManagement { get; } = new ProcessManagement();

    private string ReferenceFolder { get; } = Path.Combine(TestEnvironment.RepositoryRoot, "Tests", "NuNuGet.Tests", "Reference");

    private string OutputFolder { get; } = TestEnvironment.TestFolder;

    private string NuNuGetPath { get; }

    public ScenarioTests()
    {
        this.NuNuGetPath = Path.Combine(this.OutputFolder, NuNuGetExecutableName);

        this.ProcessManagement.WorkingDirectory = this.OutputFolder;
        this.ProcessManagement.EnvironmentVariables["NUGET_PACKAGES"] = null;
    }

    [Before(Test)]
    public async Task VerifyNuNuGetExists()
    {
        await Assert.That(File.Exists(this.NuNuGetPath)).IsTrue().Because($"Expected {NuNuGetExecutableName} to be present in the working folder: {this.OutputFolder}");
    }

    private ProcessResult RunNuNuGet(params string[] args)
    {
        TestContext.Current!.Output.WriteLine($"Running '{this.NuNuGetPath} {string.Join(' ', args)}' in folder '{this.OutputFolder}'");
        return this.ProcessManagement.Run(this.NuNuGetPath, string.Join(' ', args));
    }

    [Test]
    public async Task InvocationErrorsWithNoParameters()
    {
        ProcessResult processResult = this.RunNuNuGet();

        await Assert.That(processResult.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    public async Task InvocationSucceedsWithHelp()
    {
        ProcessResult processResult = this.RunNuNuGet("--help");

        await Assert.That(processResult.ExitCode).IsEqualTo(0);
    }

    [Test]
    public async Task InvocationErrorsWithOnlyInstall()
    {
        ProcessResult processResult = this.RunNuNuGet("install");

        await Assert.That(processResult.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    public async Task InvocationErrorsWithConfigFile()
    {
        NuGetEnvironment nuGetEnvironment = new NuGetEnvironment(this.ReferenceFolder);
        ProcessResult processResult = this.RunNuNuGet("install", "--configFile", nuGetEnvironment.ConfigPath);

        await Assert.That(processResult.ExitCode).IsNotEqualTo(0);
    }

    [Test]
    public async Task EndToEndScenario()
    {
        NuGetEnvironment nuGetEnvironment = new NuGetEnvironment(this.ReferenceFolder);

        WriteObject(nuGetEnvironment.PackagesListPath, new PackageList
        {
            TargetFramework = "net10.0",
            Packages =
            [
                new PackageEntry { Id = "NuNuGet.Reference", Version = "0.5.0" }
            ]
        });
        nuGetEnvironment.AddPackageToSource(TestEnvironment.Package050);

        // Act: Run NuNuGet.exe to generate the lock file.
        {
            ProcessResult processResult = this.RunNuNuGet("install",
                "--configFile", nuGetEnvironment.ConfigPath,
                "--lockFile", nuGetEnvironment.PackagesLockPath,
                "--listFile", nuGetEnvironment.PackagesListPath);

            await Assert.That(processResult.ExitCode).IsEqualTo(0);
            await Assert.That(processResult.StandardOutput).Contains($"GlobalPackagesPath: {nuGetEnvironment.GlobalPackagesPath}");
            await Assert.That(processResult.StandardOutput).Contains("NuNuGet.Reference/0.5.0");
        }

        // Re-run with the same parameters, check for success and the same output.
        {
            ProcessResult processResult = this.RunNuNuGet("install",
                "--configFile", nuGetEnvironment.ConfigPath,
                "--lockFile", nuGetEnvironment.PackagesLockPath,
                "--listFile", nuGetEnvironment.PackagesListPath);

            await Assert.That(processResult.ExitCode).IsEqualTo(0);
            await Assert.That(processResult.StandardOutput).Contains("NuNuGet.Reference/0.5.0");
        }

        // Add the 0.6.0 package to the package source, the 0.5.0 package should still be used as it's already in the lock file.
        {
            nuGetEnvironment.AddPackageToSource(TestEnvironment.Package060);

            ProcessResult processResult = this.RunNuNuGet("install",
                "--configFile", nuGetEnvironment.ConfigPath,
                "--lockFile", nuGetEnvironment.PackagesLockPath,
                "--listFile", nuGetEnvironment.PackagesListPath);

            await Assert.That(processResult.ExitCode).IsEqualTo(0);
            await Assert.That(processResult.StandardOutput).Contains("NuNuGet.Reference/0.5.0");
        }

        // Touch the packages.list.json, the 0.5.0 package should still be used as it's already in the lock file.
        {
            Touch(nuGetEnvironment.PackagesListPath);

            ProcessResult processResult = this.RunNuNuGet("install",
                "--configFile", nuGetEnvironment.ConfigPath,
                "--lockFile", nuGetEnvironment.PackagesLockPath,
                "--listFile", nuGetEnvironment.PackagesListPath);

            await Assert.That(processResult.ExitCode).IsEqualTo(0);
            await Assert.That(processResult.StandardOutput).Contains("NuNuGet.Reference/0.5.0");
        }

        // Update the packages.list.json to reference 0.6.0, 'install' should fail.
        {
            WriteObject(nuGetEnvironment.PackagesListPath, new PackageList
            {
                TargetFramework = "net10.0",
                Packages = [new() { Id = "NuNuGet.Reference", Version = "0.6.0" }]
            });

            ProcessResult processResult = this.RunNuNuGet("install",
                "--configFile", nuGetEnvironment.ConfigPath,
                "--lockFile", nuGetEnvironment.PackagesLockPath,
                "--listFile", nuGetEnvironment.PackagesListPath);

            await Assert.That(processResult.ExitCode).IsEqualTo(100);
        }

        // Delete the lock file, re-run 'install' and 0.6.0 should be picked-up.
        {
            DeleteFile(nuGetEnvironment.PackagesLockPath);

            ProcessResult processResult = this.RunNuNuGet("install",
                "--configFile", nuGetEnvironment.ConfigPath,
                "--lockFile", nuGetEnvironment.PackagesLockPath,
                "--listFile", nuGetEnvironment.PackagesListPath);

            await Assert.That(processResult.ExitCode).IsEqualTo(0);
            await Assert.That(processResult.StandardOutput).Contains("NuNuGet.Reference/0.6.0");
        }
    }

    [Test]
    public async Task EndToEndScenario_PackageSourceMapping()
    {
        NuGetEnvironment nuGetEnvironment = new NuGetEnvironment(this.ReferenceFolder);

        WriteObject(nuGetEnvironment.PackagesListPath, new PackageList
        {
            TargetFramework = "net10.0",
            Packages =
            [
                new PackageEntry { Id = "NuNuGet.Reference", Version = "0.5.0" }
            ]
        });
        nuGetEnvironment.AddPackageToSource(TestEnvironment.Package050);

        WriteFile(nuGetEnvironment.ConfigPath, $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
                <config>
                    <add key="globalPackagesFolder" value="{{nuGetEnvironment.GlobalPackagesPath}}" />
                </config>
                <packageSources>
                    <clear />
                    <add key="store" value="{{nuGetEnvironment.PackageSourcePath}}" />
                </packageSources>
                <packageSourceMapping>
                    <packageSource key="store">
                        <package pattern="Microsoft.*" />
                    </packageSource>
                </packageSourceMapping>
            </configuration>
            """);

        // Act: Run NuNuGet.exe - it should fail because the 'package source mapping' does not allow 'store' to be considered for the NuGet.Reference package.
        {
            ProcessResult processResult = this.RunNuNuGet("install", "--configFile", nuGetEnvironment.ConfigPath, "--lockFile", nuGetEnvironment.PackagesLockPath, "--listFile", nuGetEnvironment.PackagesListPath);

            await Assert.That(processResult.ExitCode).IsEqualTo(1);
            await Assert.That(processResult.StandardError).Contains("Unable to resolve 'NuNuGet.Reference");
        }

        // Update nuget.config to allow 'store' to be considered for 'NuNuGet.*' packages, the install should succeed and pick-up the package from the 'store' source.
        {
            WriteFile(nuGetEnvironment.ConfigPath, $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                    <config>
                        <add key="globalPackagesFolder" value="{{nuGetEnvironment.GlobalPackagesPath}}" />
                    </config>
                    <packageSources>
                        <clear />
                        <add key="store" value="{{nuGetEnvironment.PackageSourcePath}}" />
                    </packageSources>
                    <packageSourceMapping>
                        <packageSource key="store">
                            <package pattern="NuNuGet.*" />
                        </packageSource>
                    </packageSourceMapping>
                </configuration>
                """);
            ProcessResult processResult = this.RunNuNuGet("install", "--configFile", nuGetEnvironment.ConfigPath, "--lockFile", nuGetEnvironment.PackagesLockPath, "--listFile", nuGetEnvironment.PackagesListPath);

            await Assert.That(processResult.ExitCode).IsEqualTo(0);
            await Assert.That(processResult.StandardOutput).Contains("NuNuGet.Reference/0.5.0");
        }
    }

    [Test]
    public async Task EndToEndScenario_SpecialTargetFrameworkFallbacks()
    {
        foreach (string targetFramework in new[] { "any", "native" })
        {
            NuGetEnvironment nuGetEnvironment = new NuGetEnvironment(this.ReferenceFolder);

            WriteObject(nuGetEnvironment.PackagesListPath, new PackageList
            {
                TargetFramework = targetFramework,
                Packages =
                [
                    new PackageEntry { Id = "NuNuGet.Reference", Version = "0.5.0" }
                ]
            });
            nuGetEnvironment.AddPackageToSource(TestEnvironment.Package050);

            ProcessResult processResult = this.RunNuNuGet("install",
                "--configFile", nuGetEnvironment.ConfigPath,
                "--lockFile", nuGetEnvironment.PackagesLockPath,
                "--listFile", nuGetEnvironment.PackagesListPath);

            await Assert.That(processResult.ExitCode).IsEqualTo(0);
            await Assert.That(processResult.StandardOutput).Contains("NuNuGet.Reference/0.5.0");
        }
    }
}
