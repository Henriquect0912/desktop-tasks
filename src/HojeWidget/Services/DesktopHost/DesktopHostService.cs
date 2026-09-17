using System.Drawing;
using HojeWidget.Interop;
using HojeWidget.Services.System;

namespace HojeWidget.Services.DesktopHost;

/// <summary>
/// Mantém o widget na camada da área de trabalho do Windows.
///
/// Estratégia: localizar a janela que hospeda o <c>SHELLDLL_DefView</c> (o container dos
/// ícones da área de trabalho, filho do <c>Progman</c> ou de um <c>WorkerW</c> conforme a
/// versão do Windows e o modo do papel de parede) e ancorar o widget imediatamente
/// **acima dela na ordem Z**, com <c>SetWindowPos</c>. Como o conjunto do shell fica no
/// fundo da ordem Z global, o widget aparece acima do papel de parede e dos ícones,
/// continua interativo e nunca sobe acima de aplicativos normais. Ele jamais recebe
/// <c>Topmost</c> nesse modo.
///
/// Por que não <c>SetParent</c>: tornar o widget filho do Progman até funciona do ponto
/// de vista de janelas — o HWND existe, responde a <c>WindowFromPoint</c> e fica na
/// posição certa — mas o DWM não compõe o conteúdo de uma janela WPF reparentada para
/// dentro do shell, e o widget simplesmente nunca pinta. Foi verificado nesta máquina:
/// com <c>SetParent</c>, <c>WindowFromPoint</c> devolvia o widget no ponto exato enquanto
/// a tela mostrava só o papel de parede, tanto com <c>AllowsTransparency</c> ligado quanto
/// desligado. A ancoragem por ordem Z mantém a janela de nível superior, então a
/// renderização do WPF funciona normalmente.
/// </summary>
public sealed class DesktopHostService
{
    private const string ProgmanClass = "Progman";
    private const string WorkerWClass = "WorkerW";
    private const string DefViewClass = "SHELLDLL_DefView";

    private IntPtr _host = IntPtr.Zero;

    /// <summary>Janela do shell usada como âncora, ou IntPtr.Zero se ainda não resolvida.</summary>
    public IntPtr Host => _host;

    /// <summary>
    /// Resolve a âncora do desktop, reaproveitando a anterior enquanto continuar válida.
    /// Revalidar cobre a reinicialização do Explorer, que destrói o Progman antigo.
    /// </summary>
    public IntPtr ResolveHost()
    {
        if (IsValidHost(_host))
        {
            return _host;
        }

        var host = FindHost();
        _host = IsValidHost(host) ? host : IntPtr.Zero;

        if (_host != IntPtr.Zero)
        {
            AppLog.Info($"Âncora da área de trabalho resolvida ({NativeMethods.GetClassNameOf(_host)}).");
        }
        else
        {
            AppLog.Warn("Âncora da área de trabalho não encontrada; o widget fica como janela normal.");
        }

        return _host;
    }

    /// <summary>
    /// Coloca o widget imediatamente acima da área de trabalho na ordem Z.
    /// Chamado ao entrar no modo área de trabalho e periodicamente, para reancorar
    /// depois de "Mostrar área de trabalho" ou de um reinício do Explorer.
    /// </summary>
    public bool Pin(IntPtr widget)
    {
        if (widget == IntPtr.Zero || !NativeMethods.IsWindow(widget))
        {
            return false;
        }

        var host = ResolveHost();
        if (host == IntPtr.Zero)
        {
            return false;
        }

        return NativeMethods.SetWindowPos(
            widget,
            host,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>Retângulo do widget em coordenadas de tela.</summary>
    public static Rectangle GetBounds(IntPtr widget)
    {
        if (!NativeMethods.GetWindowRect(widget, out var rect))
        {
            return Rectangle.Empty;
        }

        return new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
    }

    /// <summary>
    /// Move o widget para um retângulo em coordenadas de tela.
    /// Aplica, confere com GetWindowRect e corrige pela diferença medida, em vez de
    /// assumir como o Windows interpretou as coordenadas.
    /// </summary>
    public static void SetBounds(IntPtr widget, Rectangle screenBounds)
    {
        if (widget == IntPtr.Zero || screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            return;
        }

        const uint flags = NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE;

        NativeMethods.SetWindowPos(widget, IntPtr.Zero, screenBounds.X, screenBounds.Y, screenBounds.Width, screenBounds.Height, flags);

        if (!NativeMethods.GetWindowRect(widget, out var actual))
        {
            return;
        }

        var deltaX = screenBounds.X - actual.Left;
        var deltaY = screenBounds.Y - actual.Top;

        if (deltaX != 0 || deltaY != 0)
        {
            NativeMethods.SetWindowPos(
                widget,
                IntPtr.Zero,
                screenBounds.X + deltaX,
                screenBounds.Y + deltaY,
                screenBounds.Width,
                screenBounds.Height,
                flags);
        }
    }

    /// <summary>Valida que o HWND é mesmo a janela da área de trabalho.</summary>
    public static bool IsValidHost(IntPtr candidate)
    {
        if (candidate == IntPtr.Zero || !NativeMethods.IsWindow(candidate))
        {
            return false;
        }

        var className = NativeMethods.GetClassNameOf(candidate);
        if (className is not (ProgmanClass or WorkerWClass))
        {
            return false;
        }

        return NativeMethods.FindWindowEx(candidate, IntPtr.Zero, DefViewClass, null) != IntPtr.Zero
               || className == ProgmanClass;
    }

    /// <summary>
    /// Localiza a janela que hospeda os ícones da área de trabalho.
    /// O truque do WorkerW varia entre versões do Windows, então a busca é defensiva:
    /// pede ao Progman para instanciar o WorkerW, tenta o próprio Progman e, só então,
    /// varre as janelas de nível superior procurando quem contém o SHELLDLL_DefView.
    /// </summary>
    public static IntPtr FindHost()
    {
        var progman = NativeMethods.FindWindow(ProgmanClass, null);

        if (progman != IntPtr.Zero)
        {
            // Duas variantes conhecidas da mensagem não documentada 0x052C.
            NativeMethods.SendMessageTimeout(
                progman, NativeMethods.WM_SPAWN_WORKERW, new IntPtr(0xD), new IntPtr(0x1),
                NativeMethods.SMTO_NORMAL | NativeMethods.SMTO_ABORTIFHUNG, 1000, out _);

            NativeMethods.SendMessageTimeout(
                progman, NativeMethods.WM_SPAWN_WORKERW, IntPtr.Zero, IntPtr.Zero,
                NativeMethods.SMTO_NORMAL | NativeMethods.SMTO_ABORTIFHUNG, 1000, out _);

            if (NativeMethods.FindWindowEx(progman, IntPtr.Zero, DefViewClass, null) != IntPtr.Zero)
            {
                return progman;
            }
        }

        var found = IntPtr.Zero;

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, DefViewClass, null) == IntPtr.Zero)
            {
                return true;
            }

            var className = NativeMethods.GetClassNameOf(hwnd);
            if (className is ProgmanClass or WorkerWClass)
            {
                found = hwnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found != IntPtr.Zero ? found : progman;
    }
}
