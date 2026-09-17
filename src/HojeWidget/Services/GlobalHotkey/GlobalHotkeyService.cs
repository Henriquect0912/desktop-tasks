using System.ComponentModel;
using HojeWidget.Interop;
using HojeWidget.Services.System;

namespace HojeWidget.Services.GlobalHotkey;

/// <summary>
/// Registra o atalho global fixo Ctrl + Shift + Q.
/// Se a combinacao estiver ocupada, nao troca por outra: reporta a falha e
/// o restante do aplicativo continua funcionando.
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0xB0DE;
    private const int VkQ = 0x51;

    private readonly IntPtr _handle;
    private bool _registered;

    public GlobalHotkeyService(IntPtr messageWindowHandle) => _handle = messageWindowHandle;

    public int Id => HotkeyId;

    public bool IsRegistered => _registered;

    /// <summary>Descricao do erro quando o registro falha, ou null.</summary>
    public string? FailureReason { get; private set; }

    public bool TryRegister()
    {
        if (_registered)
        {
            return true;
        }

        var modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT;
        if (NativeMethods.RegisterHotKey(_handle, HotkeyId, modifiers, VkQ))
        {
            _registered = true;
            FailureReason = null;
            AppLog.Info("Atalho global Ctrl + Shift + Q registrado.");
            return true;
        }

        var error = new Win32Exception(global::System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        FailureReason = error.Message;
        AppLog.Warn("Nao foi possivel registrar Ctrl + Shift + Q.", error);
        return false;
    }

    public void Dispose()
    {
        if (!_registered)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_handle, HotkeyId);
        _registered = false;
    }
}
