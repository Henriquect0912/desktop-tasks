using System.ComponentModel;
using System.Diagnostics;

namespace HojeWidget.Services.System;

/// <summary>
/// Atalho para uma conversa nova no Claude, aberta no navegador padrão.
///
/// O aplicativo continua sem acessar a rede: quem abre o endereço é o Windows, no
/// navegador, e nada do que está no aplicativo vai junto — nem no próprio endereço,
/// que é sempre o mesmo e não carrega tarefa, prova nem texto algum.
/// </summary>
public static class ClaudeChat
{
    public const string NewChatUrl = "https://claude.ai/new";

    /// <summary>Retorna false quando o Windows não conseguiu abrir o navegador.</summary>
    public static bool Open()
    {
        try
        {
            Process.Start(new ProcessStartInfo(NewChatUrl) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            AppLog.Warn("Nao foi possivel abrir o Claude no navegador.", ex);
            return false;
        }
    }
}
