namespace NuNuGet.Tests;

using System.IO;

using static NuNuGet.Tests.Helper;

internal sealed class NuGetEnvironment
{
    private INuGetCli NuGetCli { get; } = Tests.NuGetCli.Create();

    public string ConfigPath { get; }

    public string PackagesListPath { get; }

    public string PackagesLockPath { get; }

    public string PackageSourcePath { get; }

    public string GlobalPackagesPath { get; }

    public NuGetEnvironment(string rootPath)
    {
        this.ConfigPath = Path.Combine(rootPath, "nuget.config");
        this.PackagesListPath = Path.Combine(rootPath, "packages.list.json");
        this.PackagesLockPath = Path.Combine(rootPath, "packages.lock.json");
        this.PackageSourcePath = Path.Combine(rootPath, "__packages");
        this.GlobalPackagesPath = Path.Combine(rootPath, "__global_packages");

        // Clean state:
        //  - Delete the 'packages.list.json'
        //  - Delete the 'packages.lock.json'
        //  - Create the 'nuget.config' file, and add the local package source to it.
        //  - Delete the 'packageSourcePath', recreate it.
        //  - Delete the 'globalPackagesPath', recreate it.
        DeleteFile(this.PackagesListPath);
        DeleteFile(this.PackagesLockPath);
        this.SetSource(this.PackageSourcePath);
        RemoveFolder(this.PackageSourcePath);
        CreateFolder(this.PackageSourcePath);

        RemoveFolder(this.GlobalPackagesPath);
        CreateFolder(this.GlobalPackagesPath);
    }

    /// <summary>
    /// Rewrites the 'nuget.config' so that <paramref name="source"/> is the only package source.
    /// </summary>
    internal void SetSource(string source)
    {
        WriteFile(this.ConfigPath, $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
                <config>
                    <add key="globalPackagesFolder" value="{{this.GlobalPackagesPath}}" />
                </config>
                <packageSources>
                    <clear />
                    <add key="store" value="{{source}}" allowInsecureConnections="true" />
                </packageSources>
            </configuration>
            """);
    }

    internal void AddPackageToSource(string nupkgPath)
    {
        this.NuGetCli.Add(nupkgPath, this.PackageSourcePath);
    }
}
