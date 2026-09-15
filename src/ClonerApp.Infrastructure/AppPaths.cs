namespace ClonerApp.Infrastructure;

public static class AppPaths
{
    public static string AppDataDirectory
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ProdigyNova",
                "ProdigyDownloader");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string DatabasePath => Path.Combine(AppDataDirectory, "prodigy-downloader.db");

    public static string LogsDirectory
    {
        get
        {
            var path = Path.Combine(AppDataDirectory, "logs");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
