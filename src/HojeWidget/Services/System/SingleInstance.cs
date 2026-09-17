using HojeWidget.Interop;

namespace HojeWidget.Services.System;

/// <summary>
/// Garante uma unica instancia por usuario. A segunda execucao apenas sinaliza
/// a instancia existente para mostrar o widget e encerra.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\HojeWidget.SingleInstance.v1";

    private readonly Mutex? _mutex;

    private SingleInstanceGuard(Mutex? mutex, bool isFirst)
    {
        _mutex = mutex;
        IsFirstInstance = isFirst;
    }

    public bool IsFirstInstance { get; }

    public static SingleInstanceGuard Acquire()
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            if (createdNew)
            {
                return new SingleInstanceGuard(mutex, isFirst: true);
            }

            mutex.Dispose();
            return new SingleInstanceGuard(null, isFirst: false);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Falha ao adquirir o mutex de instancia unica; seguindo como primeira instancia.", ex);
            return new SingleInstanceGuard(null, isFirst: true);
        }
    }

    /// <summary>Pede a instancia existente que mostre o widget.</summary>
    public static void SignalExistingInstance()
    {
        var message = NativeMethods.RegisterWindowMessage(AppMessageWindow.ShowRequestMessageName);
        if (message != 0)
        {
            NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, message, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public void Dispose()
    {
        if (_mutex is null)
        {
            return;
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Mutex ja liberado por outro caminho de encerramento.
        }

        _mutex.Dispose();
    }
}
