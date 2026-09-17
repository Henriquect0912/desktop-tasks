using System.Reflection;
using HojeWidget.Services.System;
using Microsoft.Win32;

namespace HojeWidget.Services.Startup;

/// <summary>
/// Entrada por usuario em HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// Nao exige privilegios de administrador e e totalmente reversivel.
/// </summary>
public sealed class RegistryStartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HojeWidget";

    private readonly string _executablePath;

    public RegistryStartupRegistration(string? executablePath = null)
        => _executablePath = executablePath ?? AppPaths.ExecutablePath;

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) is string value && value.Length > 0;
            }
            catch (Exception ex)
            {
                AppLog.Warn("Falha ao ler a inicializacao com o Windows.", ex);
                return false;
            }
        }
    }

    public void Enable()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            key?.SetValue(ValueName, BuildCommand(), RegistryValueKind.String);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Falha ao ativar a inicializacao com o Windows.", ex);
        }
    }

    /// <summary>
    /// Comando absoluto e escapado que reinicia o aplicativo.
    /// Na publicacao self-contained o proprio executavel basta. Quando o app roda
    /// pelo runtime compartilhado, o processo e o dotnet.exe e o assembly precisa
    /// ir como argumento, senao a inicializacao automatica abriria o dotnet sozinho.
    /// </summary>
    private string BuildCommand()
    {
        if (Path.GetFileName(_executablePath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var assembly = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(assembly))
            {
                return $"{Quote(_executablePath)} {Quote(assembly)}";
            }
        }

        return Quote(_executablePath);
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not null)
            {
                // Remove somente a entrada do Hoje Widget.
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Falha ao desativar a inicializacao com o Windows.", ex);
        }
    }

    private static string Quote(string path)
        => path.StartsWith('"') ? path : $"\"{path}\"";
}
