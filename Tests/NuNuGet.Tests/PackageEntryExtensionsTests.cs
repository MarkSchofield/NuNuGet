namespace NuNuGet.Tests;

using NuGet.LibraryModel;
using NuGet.Versioning;
using NuNuGet.Commands;
using NuNuGet.Models;

public class PackageEntryExtensionsTests
{
    [Test]
    public async Task ToLibraryDependency_WithExactVersion_CreatesCorrectLibraryDependency()
    {
        PackageEntry package = new()
        {
            Id = "Newtonsoft.Json",
            Version = "[13.0.3]"
        };

        LibraryDependency result = package.ToLibraryDependency();

        await Assert.That(result).IsNotNull();
        await Assert.That(result.LibraryRange).IsNotNull();
        await Assert.That(result.LibraryRange.VersionRange).IsNotNull();

        await Assert.That(result.LibraryRange.Name).IsEqualTo("Newtonsoft.Json");
        await Assert.That(result.LibraryRange.TypeConstraint).IsEqualTo(LibraryDependencyTarget.Package);

        await Assert.That(result.LibraryRange.VersionRange.IsFloating).IsFalse();
        await Assert.That(result.LibraryRange.VersionRange.HasLowerBound).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.HasUpperBound).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.IsMinInclusive).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.IsMaxInclusive).IsTrue();
    }

    [Test]
    public async Task ToLibraryDependency_WithMinimumVersion_CreatesCorrectVersionRange()
    {
        PackageEntry package = new()
        {
            Id = "AutoMapper",
            Version = "12.0.1"
        };

        LibraryDependency result = package.ToLibraryDependency();

        await Assert.That(result).IsNotNull();
        await Assert.That(result.LibraryRange).IsNotNull();
        await Assert.That(result.LibraryRange.VersionRange).IsNotNull();

        await Assert.That(result.LibraryRange.Name).IsEqualTo("AutoMapper");
        await Assert.That(result.LibraryRange.TypeConstraint).IsEqualTo(LibraryDependencyTarget.Package);

        await Assert.That(result.LibraryRange.VersionRange.IsFloating).IsFalse();
        await Assert.That(result.LibraryRange.VersionRange.HasLowerBound).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.HasUpperBound).IsFalse();
        await Assert.That(result.LibraryRange.VersionRange.IsMinInclusive).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.IsMaxInclusive).IsFalse();
    }

    [Test]
    public async Task ToLibraryDependency_WithFloatingMajorVersion_CreatesCorrectVersionRange()
    {
        PackageEntry package = new()
        {
            Id = "Microsoft.Extensions.Logging",
            Version = "1.*"
        };

        LibraryDependency result = package.ToLibraryDependency();

        await Assert.That(result).IsNotNull();
        await Assert.That(result.LibraryRange).IsNotNull();
        await Assert.That(result.LibraryRange.VersionRange).IsNotNull();

        await Assert.That(result.LibraryRange.VersionRange.IsFloating).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.HasLowerBound).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.HasUpperBound).IsFalse();
        await Assert.That(result.LibraryRange.VersionRange.IsMinInclusive).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.IsMaxInclusive).IsFalse();
    }

    [Test]
    public async Task ToLibraryDependency_WithFloatingMinorVersion_CreatesCorrectVersionRange()
    {
        PackageEntry package = new()
        {
            Id = "System.Text.Json",
            Version = "8.0.*"
        };

        LibraryDependency result = package.ToLibraryDependency();

        await Assert.That(result).IsNotNull();
        await Assert.That(result.LibraryRange).IsNotNull();
        await Assert.That(result.LibraryRange.VersionRange).IsNotNull();

        await Assert.That(result.LibraryRange.VersionRange.IsFloating).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.HasLowerBound).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.HasUpperBound).IsFalse();
        await Assert.That(result.LibraryRange.VersionRange.IsMinInclusive).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.IsMaxInclusive).IsFalse();
    }

    [Test]
    public async Task ToLibraryDependency_WithVersionRangeInclusiveBounds_CreatesCorrectVersionRange()
    {
        PackageEntry package = new()
        {
            Id = "NuGet.Protocol",
            Version = "[6.0.0,7.0.0]"
        };

        LibraryDependency result = package.ToLibraryDependency();

        await Assert.That(result).IsNotNull();
        await Assert.That(result.LibraryRange).IsNotNull();
        await Assert.That(result.LibraryRange.VersionRange).IsNotNull();

        await Assert.That(result.LibraryRange.VersionRange.IsFloating).IsFalse();
        await Assert.That(result.LibraryRange.VersionRange.HasLowerBound).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.HasUpperBound).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.IsMinInclusive).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.IsMaxInclusive).IsTrue();
    }

    [Test]
    public async Task ToLibraryDependency_WithVersionRangeExclusiveUpperBound_CreatesCorrectVersionRange()
    {
        PackageEntry package = new()
        {
            Id = "Castle.Core",
            Version = "[1.0.0,2.0.0)"
        };

        LibraryDependency result = package.ToLibraryDependency();

        await Assert.That(result).IsNotNull();
        await Assert.That(result.LibraryRange).IsNotNull();
        await Assert.That(result.LibraryRange.VersionRange).IsNotNull();

        await Assert.That(result.LibraryRange.VersionRange.IsFloating).IsFalse();
        await Assert.That(result.LibraryRange.VersionRange.HasLowerBound).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.HasUpperBound).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.IsMinInclusive).IsTrue();
        await Assert.That(result.LibraryRange.VersionRange.IsMaxInclusive).IsFalse();
    }

    [Test]
    public async Task ToLibraryDependency_WithPrereleaseVersion_CreatesCorrectVersionRange()
    {
        PackageEntry package = new()
        {
            Id = "Experimental.Package",
            Version = "1.0.0-beta.1"
        };

        LibraryDependency result = package.ToLibraryDependency();

        await Assert.That(result).IsNotNull();
        await Assert.That(result.LibraryRange).IsNotNull();
        await Assert.That(result.LibraryRange.Name).IsEqualTo("Experimental.Package");
        await Assert.That(result.LibraryRange.VersionRange).IsEqualTo(VersionRange.Parse("1.0.0-beta.1"));
        await Assert.That(result.LibraryRange.TypeConstraint).IsEqualTo(LibraryDependencyTarget.Package);
    }

    [Test]
    public void ToLibraryDependency_WithInvalidVersionString_ThrowsException()
    {
        PackageEntry package = new()
        {
            Id = "InvalidPackage",
            Version = "[13.0.0"
        };

        _ = Assert.Throws<ArgumentException>(() => package.ToLibraryDependency());
    }
}
