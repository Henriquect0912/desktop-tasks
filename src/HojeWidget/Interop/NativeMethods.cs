using System.Runtime.InteropServices;
using System.Text;

namespace HojeWidget.Interop;

/// <summary>
/// Camada isolada de P/Invoke. Nenhum outro arquivo deve declarar DllImport.
/// </summary>
internal static class NativeMethods
{
    // ---------- Estruturas ----------

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>Carga de WM_WINDOWPOSCHANGING. Reescrever hwndInsertAfter fixa a ordem Z.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    // ---------- Constantes ----------

    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;

    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int WS_EX_APPWINDOW = 0x00040000;
    internal const int WS_EX_NOACTIVATE = 0x08000000;
    internal const int WS_EX_LAYERED = 0x00080000;

    internal const int WS_BORDER = 0x00800000;
    internal const int WS_DLGFRAME = 0x00400000;
    internal const int WS_CAPTION = WS_BORDER | WS_DLGFRAME;
    internal const int WS_THICKFRAME = 0x00040000;

    /// <summary>SetLayeredWindowAttributes: usar o alfa constante da janela inteira.</summary>
    internal const int LWA_ALPHA = 0x00000002;

    internal const uint RDW_INVALIDATE = 0x0001;
    internal const uint RDW_ERASE = 0x0004;
    internal const uint RDW_FRAME = 0x0400;
    internal const uint RDW_ALLCHILDREN = 0x0080;

    internal const int SWP_NOSIZE = 0x0001;
    internal const int SWP_NOMOVE = 0x0002;
    internal const int SWP_NOZORDER = 0x0004;
    internal const int SWP_NOACTIVATE = 0x0010;
    internal const int SWP_SHOWWINDOW = 0x0040;
    internal const int SWP_FRAMECHANGED = 0x0020;

    internal static readonly IntPtr HWND_TOP = new(0);
    internal static readonly IntPtr HWND_BOTTOM = new(1);
    internal static readonly IntPtr HWND_TOPMOST = new(-1);
    internal static readonly IntPtr HWND_NOTOPMOST = new(-2);
    internal static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    internal const int SW_SHOW = 5;
    internal const int SW_SHOWNA = 8;
    internal const int SW_HIDE = 0;

    internal const uint SMTO_NORMAL = 0x0000;
    internal const uint SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>Mensagem nao documentada que faz o Progman criar/instanciar o WorkerW do papel de parede.</summary>
    internal const uint WM_SPAWN_WORKERW = 0x052C;

    internal const int MOD_ALT = 0x0001;
    internal const int MOD_CONTROL = 0x0002;
    internal const int MOD_SHIFT = 0x0004;
    internal const int MOD_NOREPEAT = 0x4000;

    internal const int WM_HOTKEY = 0x0312;
    internal const int WM_WINDOWPOSCHANGING = 0x0046;
    internal const int WM_DPICHANGED = 0x02E0;
    internal const int WM_DISPLAYCHANGE = 0x007E;

    internal const int MONITOR_DEFAULTTONEAREST = 0x00000002;
    internal const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;

    internal const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>GetAncestor: janela pai real, ignorando o proprietario.</summary>
    internal const uint GA_PARENT = 1;

    // ---------- Janelas ----------

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    internal static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    internal static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    // ---------- Hotkey global ----------

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ---------- Transparencia e cantos ----------

    /// <summary>
    /// Alfa constante da janela inteira. Ao contrario de UpdateLayeredWindow (usado pelo
    /// AllowsTransparency do WPF), funciona em janela filha, que e o caso do widget
    /// quando ele esta hospedado na area de trabalho.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, int dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    // ---------- Console ----------

    /// <summary>Janela de console associada ao processo, ou IntPtr.Zero se não houver.</summary>
    [DllImport("kernel32.dll")]
    internal static extern IntPtr GetConsoleWindow();

    /// <summary>
    /// Solta o console do processo. Quando nenhum processo continua associado, o
    /// Windows fecha a janela de console.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FreeConsole();

    // ---------- Monitores / DPI ----------

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromPoint(POINT pt, int dwFlags);

    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    internal static string GetClassNameOf(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        var length = GetClassName(hWnd, sb, sb.Capacity);
        return length > 0 ? sb.ToString(0, length) : string.Empty;
    }
}
