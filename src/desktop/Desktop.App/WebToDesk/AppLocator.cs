using System.Collections.Generic;
using System.IO;

namespace WebToDesk;

internal static class AppLocator
{
    public static string? TryFindFile(string relativeOrFileName)
    {
        foreach (var directory in EnumerateSearchDirectories())
        {
            var candidate = Path.Combine(directory, relativeOrFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in ExpandDirectory(AppContext.BaseDirectory))
        {
            if (seen.Add(path))
            {
                yield return path;
            }
        }

        foreach (var path in ExpandDirectory(Directory.GetCurrentDirectory()))
        {
            if (seen.Add(path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> ExpandDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        for (var depth = 0; current is not null && depth < 10; depth++, current = current.Parent)
        {
            yield return current.FullName;
        }
    }
}
