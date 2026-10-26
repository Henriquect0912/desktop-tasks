using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using HojeWidget.Interop;
using HojeWidget.Models;
using HojeWidget.Services.DesktopHost;
using HojeWidget.ViewModels;

// Limites fisicos da janela usam System.Drawing (Rectangle, Size);
// Point aqui e o do WPF, vindo dos eventos de mouse.
using Point = System.Windows.Point;
using Size = System.Drawing.Size;

namespace HojeWidget.Views;

/// <summary>
/// Widget. Sem barra de titulo, sem bordas do sistema e sem botoes padrao.
/// Mover e redimensionar sao feitos manualmente em pixels fisicos, porque no modo
/// area de trabalho a janela e filha do shell e o laco de movimentacao do sistema
/// nao pode ser usado.
/// </summary>
public partial class MainWindow : Window
{
    private const int DragThresholdPixels = 5;

    /// <summary>Raio dos cantos arredondados, em DIPs.</summary>
    private const double CornerRadiusDip = 12;

    /// <summary>Opacidade do tema Translucido: ~84%, dentro da faixa pedida de 80% a 85%.</summary>
    private const byte TranslucentAlpha = 214;

    private IntPtr _handle = IntPtr.Zero;
    private Size _lastRegionSize = Size.Empty;
    private AppTheme _theme = AppTheme.Black;
    private IntPtr _desktopHost = IntPtr.Zero;
    private bool _pinToDesktop;

    // Movimentacao pelo cabecalho
    private bool _isMovingWindow;
    private NativeMethods.POINT _moveStartCursor;
    private Rectangle _moveStartBounds;

    // Redimensionamento pelas alcas
    private string? _resizeEdge;
    private NativeMethods.POINT _resizeStartCursor;
    private Rectangle _resizeStartBounds;

    // Reordenacao manual
    private TaskItemViewModel? _reorderCandidate;
    private Point _reorderStartPoint;
    private bool _isReordering;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>HWND da janela, disponivel apos SourceInitialized.</summary>
    public IntPtr Handle => _handle;

    /// <summary>Disparado ao terminar um movimento ou redimensionamento.</summary>
    public event Action? BoundsChangeCommitted;

    private MainViewModel? Model => DataContext as MainViewModel;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _handle = new WindowInteropHelper(this).Handle;
        ApplyToolWindowStyles();
        ApplyTheme(_theme);
        UpdateCornerRegion();

        HwndSource.FromHwnd(_handle)?.AddHook(WndProc);
    }

    /// <summary>
    /// Ancora o widget logo acima da área de trabalho e o mantém lá.
    /// Sem isso, qualquer clique no widget o traria para a frente dos aplicativos.
    /// </summary>
    public void PinBelowApplications(IntPtr desktopHost)
    {
        _desktopHost = desktopHost;
        _pinToDesktop = desktopHost != IntPtr.Zero;
    }

    /// <summary>Libera a ancoragem, para o modo Primeiro plano.</summary>
    public void UnpinFromDesktop()
    {
        _pinToDesktop = false;
        _desktopHost = IntPtr.Zero;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Reescreve a ordem Z pedida pelo Windows para logo acima da área de trabalho.
        if (msg == NativeMethods.WM_WINDOWPOSCHANGING
            && _pinToDesktop
            && _desktopHost != IntPtr.Zero
            && NativeMethods.IsWindow(_desktopHost))
        {
            var position = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);
            position.hwndInsertAfter = _desktopHost;
            position.flags &= ~(uint)NativeMethods.SWP_NOZORDER;
            Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Aplica os efeitos de janela do tema.
    ///
    /// A transparencia do tema Translucido usa <c>SetLayeredWindowAttributes</c>, e nao
    /// o <c>AllowsTransparency</c> do WPF. O caminho do WPF desenha por
    /// <c>UpdateLayeredWindow</c>, que nao funciona em janela filha — e no modo area de
    /// trabalho o widget e filho do shell. Com AllowsTransparency ligado, o widget
    /// existia, respondia ao mouse e ficava por cima, mas nao pintava nada.
    /// </summary>
    public void ApplyTheme(AppTheme theme)
    {
        _theme = theme;

        if (_handle == IntPtr.Zero)
        {
            return;
        }

        var exStyle = NativeMethods.GetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE).ToInt64();

        if (theme == AppTheme.Translucent)
        {
            NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle | NativeMethods.WS_EX_LAYERED));
            NativeMethods.SetLayeredWindowAttributes(_handle, 0, TranslucentAlpha, NativeMethods.LWA_ALPHA);
        }
        else
        {
            NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle & ~(long)NativeMethods.WS_EX_LAYERED));
        }

        NativeMethods.RedrawWindow(
            _handle,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_ERASE | NativeMethods.RDW_FRAME | NativeMethods.RDW_ALLCHILDREN);
    }

    /// <summary>
    /// Cantos arredondados por regiao de janela. Os cantos do DWM nao valem aqui:
    /// efeitos do DWM nao se aplicam a janela filha.
    /// </summary>
    private void UpdateCornerRegion()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        var bounds = GetPhysicalBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var size = new Size(bounds.Width, bounds.Height);
        if (size == _lastRegionSize)
        {
            return;
        }

        var diameter = (int)Math.Round(CornerRadiusDip * DpiScale * 2);
        var region = NativeMethods.CreateRoundRectRgn(0, 0, bounds.Width + 1, bounds.Height + 1, diameter, diameter);
        if (region == IntPtr.Zero)
        {
            return;
        }

        // SetWindowRgn assume a posse da regiao quando tem sucesso.
        if (NativeMethods.SetWindowRgn(_handle, region, true) == 0)
        {
            NativeMethods.DeleteObject(region);
            return;
        }

        _lastRegionSize = size;
    }

    /// <summary>
    /// Fora da barra de tarefas e fora do Alt + Tab, e sem nenhuma moldura do sistema.
    ///
    /// Mesmo com WindowStyle="None", o WPF mantem WS_THICKFRAME para permitir
    /// redimensionar pelas bordas. Sem AllowsTransparency essa moldura passa a ser
    /// desenhada e aparece como uma faixa clara no topo. O widget redimensiona pelas
    /// proprias alcas, entao a moldura e removida por completo.
    /// </summary>
    private void ApplyToolWindowStyles()
    {
        if (_handle == IntPtr.Zero)
        {
            return;
        }

        var exStyle = NativeMethods.GetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        exStyle &= ~(long)NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));

        var style = NativeMethods.GetWindowLongPtr(_handle, NativeMethods.GWL_STYLE).ToInt64();
        style &= ~(long)(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME);
        NativeMethods.SetWindowLongPtr(_handle, NativeMethods.GWL_STYLE, new IntPtr(style));

        NativeMethods.SetWindowPos(
            _handle,
            IntPtr.Zero,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
    }

    // ---------------- Posicao e tamanho em pixels fisicos ----------------

    public Rectangle GetPhysicalBounds() => DesktopHostService.GetBounds(_handle);

    public void SetPhysicalBounds(Rectangle bounds)
    {
        DesktopHostService.SetBounds(_handle, bounds);
        UpdateCornerRegion();
    }

    private double DpiScale
    {
        get
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return dpi.DpiScaleX <= 0 ? 1.0 : dpi.DpiScaleX;
        }
    }

    // ---------------- Mover pelo cabecalho ----------------

    private void HeaderPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<ButtonBase>(source) is not null)
        {
            return;
        }

        if (!NativeMethods.GetCursorPos(out _moveStartCursor))
        {
            return;
        }

        _moveStartBounds = GetPhysicalBounds();
        _isMovingWindow = true;
        HeaderPanel.CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isMovingWindow)
        {
            MoveWithCursor();
            return;
        }

        if (_resizeEdge is not null)
        {
            ResizeWithCursor();
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndWindowGesture();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndWindowGesture();
    }

    private void EndWindowGesture()
    {
        if (!_isMovingWindow && _resizeEdge is null)
        {
            return;
        }

        _isMovingWindow = false;
        _resizeEdge = null;

        if (HeaderPanel.IsMouseCaptured)
        {
            HeaderPanel.ReleaseMouseCapture();
        }

        if (ResizeGrips.IsMouseCaptured)
        {
            ResizeGrips.ReleaseMouseCapture();
        }

        BoundsChangeCommitted?.Invoke();
    }

    private void MoveWithCursor()
    {
        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        var bounds = new Rectangle(
            _moveStartBounds.X + (cursor.X - _moveStartCursor.X),
            _moveStartBounds.Y + (cursor.Y - _moveStartCursor.Y),
            _moveStartBounds.Width,
            _moveStartBounds.Height);

        SetPhysicalBounds(bounds);
    }

    // ---------------- Redimensionar pelas alcas ----------------

    private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string edge })
        {
            return;
        }

        if (!NativeMethods.GetCursorPos(out _resizeStartCursor))
        {
            return;
        }

        _resizeStartBounds = GetPhysicalBounds();
        _resizeEdge = edge;
        ResizeGrips.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeWithCursor()
    {
        if (_resizeEdge is null || !NativeMethods.GetCursorPos(out var cursor))
        {
            return;
        }

        var scale = DpiScale;
        var minWidth = (int)Math.Round(MinWidth * scale);
        var minHeight = (int)Math.Round(MinHeight * scale);

        var deltaX = cursor.X - _resizeStartCursor.X;
        var deltaY = cursor.Y - _resizeStartCursor.Y;

        var left = _resizeStartBounds.Left;
        var top = _resizeStartBounds.Top;
        var right = _resizeStartBounds.Right;
        var bottom = _resizeStartBounds.Bottom;

        if (_resizeEdge.Contains("Left", StringComparison.Ordinal))
        {
            left = Math.Min(left + deltaX, right - minWidth);
        }

        if (_resizeEdge.Contains("Right", StringComparison.Ordinal))
        {
            right = Math.Max(right + deltaX, left + minWidth);
        }

        if (_resizeEdge.Contains("Top", StringComparison.Ordinal))
        {
            top = Math.Min(top + deltaY, bottom - minHeight);
        }

        if (_resizeEdge.Contains("Bottom", StringComparison.Ordinal))
        {
            bottom = Math.Max(bottom + deltaY, top + minHeight);
        }

        SetPhysicalBounds(new Rectangle(left, top, right - left, bottom - top));
    }

    // ---------------- Teclado ----------------

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key != Key.Escape || Model is null)
        {
            return;
        }

        if (Model.IsDeleteConfirmationVisible)
        {
            Model.CancelDelete();
            e.Handled = true;
            return;
        }

        var editing = Model.AllRows.FirstOrDefault(i => i.IsEditing);
        if (editing is not null)
        {
            MainViewModel.CancelEdit(editing);
            e.Handled = true;
            return;
        }

        var withMenu = Model.AllRows.FirstOrDefault(i => i.IsMenuOpen);
        if (withMenu is not null)
        {
            withMenu.IsMenuOpen = false;
            e.Handled = true;
            return;
        }

        if (Model.IsAddMenuOpen)
        {
            Model.ToggleAddMenu();
            e.Handled = true;
            return;
        }

        if (Model.IsAdding)
        {
            Model.CancelAdd();
            e.Handled = true;
            return;
        }

        if (Model.IsAddingReminder)
        {
            Model.CancelAddReminder();
            e.Handled = true;
        }
    }

    private void NewTaskBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (Model is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                Model.CommitAdd();
                e.Handled = true;
                break;

            case Key.Escape:
                Model.CancelAdd();
                e.Handled = true;
                break;
        }
    }

    private void NewReminderBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (Model is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                Model.CommitAddReminder();
                ScrollRemindersToEnd();
                e.Handled = true;
                break;

            case Key.Escape:
                Model.CancelAddReminder();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Coloca o cursor no campo de criacao mesmo quando ele ja estava visivel.</summary>
    public void FocusNewTaskField()
    {
        Dispatcher.BeginInvoke(
            new Action(() => FocusBehavior.Focus(NewTaskBox)),
            global::System.Windows.Threading.DispatcherPriority.Input);
    }

    // ---------------- Itens da lista ----------------

    private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskItemViewModel item } && Model is not null)
        {
            Model.ToggleCompleted(item, item.IsCompleted);
        }
    }

    private void RowMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskItemViewModel item } || Model is null)
        {
            return;
        }

        var open = !item.IsMenuOpen;
        foreach (var other in Model.AllRows)
        {
            other.IsMenuOpen = false;
        }

        item.IsMenuOpen = open;
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskItemViewModel item } && Model is not null)
        {
            item.IsMenuOpen = false;
            Model.BeginEdit(item);
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskItemViewModel item })
        {
            Model?.RequestDelete(item);
        }
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskItemViewModel item } || Model is null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                Model.CommitEdit(item);
                e.Handled = true;
                break;

            case Key.Escape:
                MainViewModel.CancelEdit(item);
                e.Handled = true;
                break;
        }
    }

    private void EditBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Sair do campo sem confirmar equivale a cancelar: nada e alterado.
        if (sender is FrameworkElement { DataContext: TaskItemViewModel item } && item.IsEditing)
        {
            MainViewModel.CancelEdit(item);
        }
    }

    // ---------------- Lembretes ----------------

    /// <summary>
    /// Os lembretes não se reordenam por arrasto; um clique na lista só fecha o
    /// menu de outra linha que tenha ficado aberto, como acontece em Hoje.
    /// </summary>
    private void RemindersHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            CloseMenusExcept(FindItemViewModel(source, RemindersHost));
        }
    }

    /// <summary>
    /// Leva a lista até o fim, onde o lembrete recém-criado entrou. Roda depois do
    /// layout, quando ele já ocupa a sua linha.
    /// </summary>
    private void ScrollRemindersToEnd()
        => Dispatcher.BeginInvoke(
            new Action(() => RemindersScroll.ScrollToEnd()),
            global::System.Windows.Threading.DispatcherPriority.Loaded);

    private void CloseMenusExcept(TaskItemViewModel? keep)
    {
        if (Model is null)
        {
            return;
        }

        foreach (var other in Model.AllRows.Where(i => i.IsMenuOpen && !ReferenceEquals(i, keep)))
        {
            other.IsMenuOpen = false;
        }
    }

    // ---------------- Reordenacao manual ----------------

    private void TasksHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _reorderCandidate = null;
        _isReordering = false;

        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var item = FindItemViewModel(source, TasksHost);
        CloseMenusExcept(item);

        if (item is null || !item.CanReorder || item.IsEditing)
        {
            return;
        }

        // Cliques em controles (checkbox, botoes, campo) nao iniciam arrasto.
        if (FindAncestor<ButtonBase>(source) is not null || FindAncestor<TextBoxBase>(source) is not null)
        {
            return;
        }

        _reorderCandidate = item;
        _reorderStartPoint = e.GetPosition(TasksHost);
    }

    private void TasksHost_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_reorderCandidate is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(TasksHost);

        if (!_isReordering)
        {
            if (Math.Abs(position.Y - _reorderStartPoint.Y) < DragThresholdPixels
                && Math.Abs(position.X - _reorderStartPoint.X) < DragThresholdPixels)
            {
                return;
            }

            _isReordering = true;
            _reorderCandidate.IsDragging = true;
            TasksHost.CaptureMouse();
        }

        UpdateDropIndicators(position);
        AutoScrollDuringReorder(e.GetPosition(ListScroll));
    }

    private void TasksHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_reorderCandidate is null)
        {
            return;
        }

        var item = _reorderCandidate;
        var wasReordering = _isReordering;
        var position = e.GetPosition(TasksHost);

        _reorderCandidate = null;
        _isReordering = false;
        item.IsDragging = false;

        if (TasksHost.IsMouseCaptured)
        {
            TasksHost.ReleaseMouseCapture();
        }

        ClearDropIndicators();

        if (!wasReordering || Model is null)
        {
            return;
        }

        var pending = Model.Items.Where(i => !i.IsCompleted).ToList();
        var insertIndex = ComputeInsertIndex(pending, position.Y);
        var currentIndex = pending.IndexOf(item);

        if (currentIndex < 0)
        {
            return;
        }

        var target = insertIndex > currentIndex ? insertIndex - 1 : insertIndex;
        Model.MovePending(item, target);
    }

    private void UpdateDropIndicators(Point position)
    {
        if (Model is null)
        {
            return;
        }

        ClearDropIndicators();

        var pending = Model.Items.Where(i => !i.IsCompleted).ToList();
        if (pending.Count == 0)
        {
            return;
        }

        var insertIndex = ComputeInsertIndex(pending, position.Y);

        if (insertIndex < pending.Count)
        {
            pending[insertIndex].IsDropTargetBefore = true;
        }
        else
        {
            pending[^1].IsDropTargetAfter = true;
        }
    }

    private void ClearDropIndicators()
    {
        if (Model is null)
        {
            return;
        }

        foreach (var item in Model.Items)
        {
            item.IsDropTargetBefore = false;
            item.IsDropTargetAfter = false;
        }
    }

    /// <summary>Quantas pendentes ficam acima do ponto do cursor.</summary>
    private int ComputeInsertIndex(IReadOnlyList<TaskItemViewModel> pending, double y)
    {
        var index = 0;

        foreach (var item in pending)
        {
            if (TasksHost.ItemContainerGenerator.ContainerFromItem(item) is not FrameworkElement container)
            {
                index++;
                continue;
            }

            var top = container.TransformToAncestor(TasksHost).Transform(new Point(0, 0)).Y;
            var middle = top + container.ActualHeight / 2;

            if (y < middle)
            {
                break;
            }

            index++;
        }

        return index;
    }

    private void AutoScrollDuringReorder(Point positionInScroll)
    {
        const double edge = 20;
        const double step = 12;

        if (ListScroll.ScrollableHeight <= 0)
        {
            return;
        }

        if (positionInScroll.Y < edge)
        {
            ListScroll.ScrollToVerticalOffset(ListScroll.VerticalOffset - step);
        }
        else if (positionInScroll.Y > ListScroll.ActualHeight - edge)
        {
            ListScroll.ScrollToVerticalOffset(ListScroll.VerticalOffset + step);
        }
    }

    // ---------------- Utilitarios de arvore visual ----------------

    private static TaskItemViewModel? FindItemViewModel(DependencyObject source, DependencyObject host)
    {
        var current = source;

        while (current is not null && !ReferenceEquals(current, host))
        {
            if (current is FrameworkElement { DataContext: TaskItemViewModel item })
            {
                return item;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        var current = source;

        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject current)
        => current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
}
