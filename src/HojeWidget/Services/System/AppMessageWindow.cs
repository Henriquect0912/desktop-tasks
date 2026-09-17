using System.Windows.Interop;
using HojeWidget.Interop;

namespace HojeWidget.Services.System;

/// <summary>
/// Janela oculta de nivel superior que recebe as mensagens do processo:
/// hotkey global, pedido de exibicao vindo de uma segunda instancia e
/// reinicializacao do Explorer.
/// Precisa ser de nivel superior (e nao message-only) para receber broadcasts.
/// </summary>
public sealed class AppMessageWindow : IDisposable
{
    /// <summary>Mensagem privada usada pela segunda instancia para acordar a primeira.</summary>
    public const string ShowRequestMessageName = "HojeWidget.ShowRequest.v1";

    private readonly HwndSource _source;
    private readonly uint _showRequestMessage;
    private readonly uint _taskbarCreatedMessage;

    public AppMessageWindow()
    {
        var parameters = new HwndSourceParameters("HojeWidget.Messages")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            ParentWindow = IntPtr.Zero,
            WindowStyle = 0, // WS_OVERLAPPED, sem WS_VISIBLE
            ExtendedWindowStyle = NativeMethods.WS_EX_TOOLWINDOW,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        _showRequestMessage = NativeMethods.RegisterWindowMessage(ShowRequestMessageName);
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    }

    public IntPtr Handle => _source.Handle;

    /// <summary>Uma segunda execucao pediu para mostrar o widget.</summary>
    public event Action? ShowRequested;

    /// <summary>Ctrl + Shift + Q foi pressionado.</summary>
    public event Action<int>? HotkeyPressed;

    /// <summary>O Explorer foi reiniciado; e preciso reanexar ao desktop.</summary>
    public event Action? ShellRestarted;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            HotkeyPressed?.Invoke(wParam.ToInt32());
            handled = true;
            return IntPtr.Zero;
        }

        if (_showRequestMessage != 0 && msg == (int)_showRequestMessage)
        {
            ShowRequested?.Invoke();
            handled = true;
            return IntPtr.Zero;
        }

        if (_taskbarCreatedMessage != 0 && msg == (int)_taskbarCreatedMessage)
        {
            ShellRestarted?.Invoke();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
