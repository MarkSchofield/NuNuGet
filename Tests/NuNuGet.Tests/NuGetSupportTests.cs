namespace NuNuGet.Tests;

using System;
using System.IO;

public class NuGetSupportTests
{
    [Test]
    public async Task ParsePackageNameAndVersion_SimpleThreePartVersion()
    {
        (string name, string version) = NuGetSupport.GetPackageNameAndVersion("MyPackage.1.0.0.nupkg");

        await Assert.That(name).IsEqualTo("MyPackage");
        await Assert.That(version).IsEqualTo("1.0.0");
    }

    [Test]
    public async Task ParsePackageNameAndVersion_LargeVersionNumbers()
    {
        (string name, string version) = NuGetSupport.GetPackageNameAndVersion("MyPackage.10.200.3000.nupkg");

        await Assert.That(name).IsEqualTo("MyPackage");
        await Assert.That(version).IsEqualTo("10.200.3000");
    }

    [Test]
    public async Task ParsePackageNameAndVersion_DottedPackageName()
    {
        (string name, string version) = NuGetSupport.GetPackageNameAndVersion("System.Text.Json.9.0.0.nupkg");

        await Assert.That(name).IsEqualTo("System.Text.Json");
        await Assert.That(version).IsEqualTo("9.0.0");
    }

    [Test]
    public async Task ParsePackageNameAndVersion_PrereleaseVersion()
    {
        (string name, string version) = NuGetSupport.GetPackageNameAndVersion("MyPackage.1.0.0-beta.1.nupkg");

        await Assert.That(name).IsEqualTo("MyPackage");
        await Assert.That(version).IsEqualTo("1.0.0-beta.1");
    }

    [Test]
    public async Task ParsePackageNameAndVersion_DottedNameWithPrerelease()
    {
        (string name, string version) = NuGetSupport.GetPackageNameAndVersion("My.Complex.Package.2.3.4-rc.2.nupkg");

        await Assert.That(name).IsEqualTo("My.Complex.Package");
        await Assert.That(version).IsEqualTo("2.3.4-rc.2");
    }

    [Test]
    public async Task ParsePackageNameAndVersion_MultiSegmentPrerelease()
    {
        (string name, string version) = NuGetSupport.GetPackageNameAndVersion("MyPackage.1.0.0-beta.2.3.nupkg");

        await Assert.That(name).IsEqualTo("MyPackage");
        await Assert.That(version).IsEqualTo("1.0.0-beta.2.3");
    }

    [Test]
    public async Task ParsePackageNameAndVersion_FullPathIsHandled()
    {
        (string name, string version) = NuGetSupport.GetPackageNameAndVersion(Path.Combine("C:", "packages", "Newtonsoft.Json.13.0.3.nupkg"));

        await Assert.That(name).IsEqualTo("Newtonsoft.Json");
        await Assert.That(version).IsEqualTo("13.0.3");
    }

    [Test]
    public void ParsePackageNameAndVersion_NoVersionThrows()
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
            NuGetSupport.GetPackageNameAndVersion("NoVersion.nupkg"));
    }

    [Test]
    public void ParsePackageNameAndVersion_EmptyStringThrows()
    {
        _ = Assert.Throws<InvalidOperationException>(() =>
            NuGetSupport.GetPackageNameAndVersion(".nupkg"));
    }

    [Test]
    public async Task GetPackageSha512Hash_ReturnsConsistentHash()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "test content for hashing");

            string hash1 = NuGetSupport.GetPackageSha512Hash(tempFile);
            string hash2 = NuGetSupport.GetPackageSha512Hash(tempFile);

            await Assert.That(hash1).IsNotEmpty();
            await Assert.That(hash2).IsEqualTo(hash1);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task GetPackageSha512Hash_DifferentContentProducesDifferentHash()
    {
        string tempFile1 = Path.GetTempFileName();
        string tempFile2 = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile1, "content A");
            File.WriteAllText(tempFile2, "content B");

            string hash1 = NuGetSupport.GetPackageSha512Hash(tempFile1);
            string hash2 = NuGetSupport.GetPackageSha512Hash(tempFile2);

            await Assert.That(hash2).IsNotEqualTo(hash1);
        }
        finally
        {
            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }
    }
}
