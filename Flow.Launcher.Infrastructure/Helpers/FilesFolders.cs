using System;
using System.IO;

namespace Flow.Launcher.Infrastructure.Helpers;

// TODO: Remove this
[Obsolete("This class will be removed in the future")]
public static class FilesFolders
{
    /// <summary>
    /// Returns if <paramref name="parentPath"/> contains <paramref name="subPath"/>. Equal paths are not considered to be contained by default.
    /// From https://stackoverflow.com/a/66877016
    /// </summary>
    /// <param name="parentPath">Parent path</param>
    /// <param name="subPath">Sub path</param>
    /// <param name="allowEqual">If <see langword="true"/>, when <paramref name="parentPath"/> and <paramref name="subPath"/> are equal, returns <see langword="true"/></param>
    /// <returns></returns>
    public static bool PathContains(string parentPath, string subPath, bool allowEqual = false)
    {
        if (!parentPath.EndsWith('\\'))
            parentPath += '\\';

        var rel = Path.GetRelativePath(parentPath, subPath);
        return (rel != "." || allowEqual)
               && rel != ".."
               && !rel.StartsWith("../")
               && !rel.StartsWith(@"..\")
               && !Path.IsPathRooted(rel);
    }
}
