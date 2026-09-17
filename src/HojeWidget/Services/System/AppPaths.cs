namespace HojeWidget.Services.System;

/// <summary>Locais de dados por usuario. Nada mutavel fica junto ao executavel.</summary>
public static class AppPaths
{
    public const string FolderName = "HojeWidget";

    public static string RootDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderName);

    public static string DatabaseFile => Path.Combine(RootDirectory, "app.db");

    public static string LogDirectory => Path.Combine(RootDirectory, "logs");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    /// <summary>Caminho absoluto do executavel em execucao, usado na entrada de inicializacao.</summary>
    public static string ExecutablePath
    {
        get
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            var module = global::System.Diagnostics.Process.GetCurrentProcess().MainModule;
            return module?.FileName ?? AppContext.BaseDirectory;
        }
    }
}
