namespace NuNuGet.Tests;

internal static class Build
{
    public static string GetRootPath()
    {
        return Git.GetRepositoryRoot(AppContext.BaseDirectory);
    }

    public static string GetConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
