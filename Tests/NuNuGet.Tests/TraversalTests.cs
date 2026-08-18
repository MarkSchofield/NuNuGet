namespace NuNuGet.Tests;

using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;
using System.Collections.Generic;
using TUnit.Assertions.Enums;

public class TraversalTests
{
    [Test]
    public async Task Empty()
    {
        IEnumerable<(string, NuGetVersion)> result = Traversal.ReverseTopological(new PackagesLockFile
        {
            Targets = [new PackagesLockFileTarget
            {
                Dependencies = []
            }]
        });

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task SingleElement()
    {
        IEnumerable<(string, NuGetVersion)> result = Traversal.ReverseTopological(new PackagesLockFile
        {
            Targets = [
                new PackagesLockFileTarget
                {
                    Dependencies =
                    [
                        new LockFileDependency
                        {
                            Id = "A",
                            ResolvedVersion = NuGetVersion.Parse("1.0.0"),
                        }
                    ]
                }]
        });

        await Assert.That(result).IsEquivalentTo([("A", NuGetVersion.Parse("1.0.0"))], CollectionOrdering.Matching);
    }

    [Test]
    public async Task TwoDependentElements()
    {
        IEnumerable<(string, NuGetVersion)> result = Traversal.ReverseTopological(new PackagesLockFile
        {
            Targets = [
                new PackagesLockFileTarget
                {
                    Dependencies =
                    [
                        new LockFileDependency
                        {
                            Id = "A",
                            ResolvedVersion = NuGetVersion.Parse("1.0.0"),
                            Dependencies =
                            [
                                new PackageDependency("B", VersionRange.Parse("2.0.0"))
                            ]
                        },
                        new LockFileDependency
                        {
                            Id = "B",
                            ResolvedVersion = NuGetVersion.Parse("2.0.0")
                        },

                    ]
                }]
        });

        await Assert.That(result).IsEquivalentTo([
            ("B", NuGetVersion.Parse("2.0.0")),
            ("A", NuGetVersion.Parse("1.0.0")),
        ], CollectionOrdering.Matching);
    }

    [Test]
    public async Task TwoIndependentElements()
    {
        IEnumerable<(string, NuGetVersion)> result = Traversal.ReverseTopological(new PackagesLockFile
        {
            Targets = [new PackagesLockFileTarget
            {
                Dependencies =
                [
                    new LockFileDependency
                    {
                        Id = "A",
                        ResolvedVersion = NuGetVersion.Parse("1.0.0"),
                    },
                    new LockFileDependency
                    {
                        Id = "B",
                        ResolvedVersion = NuGetVersion.Parse("2.0.0")
                    },

                ]
            }]
        });

        await Assert.That(result).IsEquivalentTo(
        [
            ("B", NuGetVersion.Parse("2.0.0")),
            ("A", NuGetVersion.Parse("1.0.0")),
        ]);
    }

    [Test]
    public async Task ModerateGraph()
    {
        IEnumerable<(string, NuGetVersion)> result = Traversal.ReverseTopological(new PackagesLockFile
        {
            Targets = [new PackagesLockFileTarget
            {
                Dependencies =
                [
                    new LockFileDependency
                    {
                        Id = "B",
                        ResolvedVersion = NuGetVersion.Parse("2.0.0")
                    },
                    new LockFileDependency
                    {
                        Id = "A",
                        ResolvedVersion = NuGetVersion.Parse("1.0.0"),
                        Dependencies =
                        [
                            new PackageDependency("B", VersionRange.Parse("2.0.0")),
                            new PackageDependency("C", VersionRange.Parse("3.0.0")),
                        ]
                    },
                    new LockFileDependency
                    {
                        Id = "C",
                        ResolvedVersion = NuGetVersion.Parse("3.0.0"),
                        Dependencies =
                        [
                            new PackageDependency("D", VersionRange.Parse("4.0.0"))
                        ]
                    },
                    new LockFileDependency
                    {
                        Id = "D",
                        ResolvedVersion = NuGetVersion.Parse("4.0.0")
                    },
                ]
            }]
        });

        List<(string, NuGetVersion)> resultList = [.. result];

        int indexOfA = resultList.IndexOf(("A", NuGetVersion.Parse("1.0.0")));
        int indexOfB = resultList.IndexOf(("B", NuGetVersion.Parse("2.0.0")));
        int indexOfC = resultList.IndexOf(("C", NuGetVersion.Parse("3.0.0")));
        int indexOfD = resultList.IndexOf(("D", NuGetVersion.Parse("4.0.0")));

        await Assert.That(indexOfD < indexOfC).IsTrue().Because("D should come before C");
        await Assert.That(indexOfC < indexOfA).IsTrue().Because("C should come before A");
        await Assert.That(indexOfB < indexOfA).IsTrue().Because("B should come before A");
    }

    [Test]
    public async Task CaseInsensitivePackageIds()
    {
        // Dependency references "b" in lowercase, but the package is declared as "B".
        IEnumerable<(string, NuGetVersion)> result = Traversal.ReverseTopological(new PackagesLockFile
        {
            Targets = [
                new PackagesLockFileTarget
                {
                    Dependencies =
                    [
                        new LockFileDependency
                        {
                            Id = "A",
                            ResolvedVersion = NuGetVersion.Parse("1.0.0"),
                            Dependencies =
                            [
                                new PackageDependency("b", VersionRange.Parse("2.0.0"))
                            ]
                        },
                        new LockFileDependency
                        {
                            Id = "B",
                            ResolvedVersion = NuGetVersion.Parse("2.0.0")
                        },
                    ]
                }]
        });

        await Assert.That(result).IsEquivalentTo([
            ("B", NuGetVersion.Parse("2.0.0")),
            ("A", NuGetVersion.Parse("1.0.0")),
        ], CollectionOrdering.Matching);
    }
}
