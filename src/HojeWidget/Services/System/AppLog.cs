using System.Globalization;
using System.Text;

namespace HojeWidget.Services.System;

/// <summary>
/// Log local, pequeno e rotativo. Nao registra o texto das tarefas e nao existe telemetria.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 256 * 1024;
    private const int KeptFiles = 2;

    private static readonly object Gate = new();
    private static string? _file;

    public static void Initialize()
    {
        lock (Gate)
        {
            try
            {
                AppPaths.EnsureCreated();
                _file = Path.Combine(AppPaths.LogDirectory, "hojewidget.log");
            }
            catch (Exception)
            {
                // Sem log, o aplicativo continua funcionando normalmente.
                _file = null;
            }
        }
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message, Exception? error = null) => Write("WARN", message, error);

    public static void Error(string message, Exception? error = null) => Write("ERRO", message, error);

    private static void Write(string level, string message, Exception? error)
    {
        lock (Gate)
        {
            if (_file is null)
            {
                return;
            }

            try
            {
                Rotate();

                var builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                builder.Append(' ').Append(level).Append(' ').Append(message);

                if (error is not null)
                {
                    builder.Append(" :: ").Append(error.GetType().Name).Append(": ").Append(error.Message);
                }

                builder.AppendLine();
                File.AppendAllText(_file, builder.ToString(), Encoding.UTF8);
            }
            catch (IOException)
            {
                // Falha de log nunca interrompe o fluxo do usuario.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void Rotate()
    {
        if (_file is null || !File.Exists(_file))
        {
            return;
        }

        var info = new FileInfo(_file);
        if (info.Length < MaxBytes)
        {
            return;
        }

        var previous = _file + ".1";
        if (File.Exists(previous))
        {
            File.Delete(previous);
        }

        File.Move(_file, previous);

        for (var index = KeptFiles; index < KeptFiles + 5; index++)
        {
            var stale = $"{_file}.{index}";
            if (File.Exists(stale))
            {
                File.Delete(stale);
            }
        }
    }
}
