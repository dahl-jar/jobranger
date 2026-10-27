namespace Jobranger.Cli.Config;

internal static class HomePath {
    private const string prefix = "~";
    private const char slash = '/';
    private const string currentDir = ".";
    private const string parentDir = "..";

    public static string Expand(string path, string home) {
        if (path == prefix) {
            return home;
        }
        string homeDir = prefix + slash;
        return path.StartsWith(homeDir, StringComparison.Ordinal) ? Path.Combine(home, path[homeDir.Length..]) : path;
    }

    public static string Contract(string path, string home) {
        string relative = Path.GetRelativePath(home, path);
        bool underHome = Path.IsPathRooted(path)
            && relative != currentDir
            && !relative.StartsWith(parentDir, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
        return underHome ? prefix + Path.DirectorySeparatorChar + relative : path;
    }
}
