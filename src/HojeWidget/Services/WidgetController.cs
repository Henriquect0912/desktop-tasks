using System.Drawing;
using System.Windows.Threading;
using HojeWidget.Interop;
using HojeWidget.Models;
using HojeWidget.Services.Database;
using HojeWidget.Services.DesktopHost;
using HojeWidget.Services.DisplayLayout;
using HojeWidget.Services.GlobalHotkey;
using HojeWidget.Services.Startup;
using HojeWidget.Services.System;
using HojeWidget.Services.Tray;
using HojeWidget.ViewModels;
using HojeWidget.Views;
using Microsoft.Win32;

namespace HojeWidget.Services;

/// <summary>
/// Orquestra o ciclo de vida do widget: modos de janela, bandeja, atalho global,
/// mudanca de dia, mudanca de monitores e persistencia de layout.
/// </summary>
public sealed class WidgetController : IDisposable
{
    private static readonly TimeSpan DayCheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan AttachCheckInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(400);

    private readonly AppDatabase _database;
    private readonly SettingsRepository _settings;
    private readonly DisplayLayoutRepository _layouts;
    private readonly IClock _clock;
    private readonly IMonitorProvider _monitors;
    private readonly IStartupRegistration _startup;
    private readonly RoutineService _routines;
    private readonly PlanningService _planning;
    private readonly DesktopHostService _desktopHost = new();

    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;
    private readonly MainAppWindow _appWindow;
    private readonly MainAppViewModel _appViewModel;
    private readonly AppMessageWindow _messageWindow;
    private readonly GlobalHotkeyService _hotkey;
    private readonly TrayIconService _tray;

    private readonly DispatcherTimer _dayTimer;
    private readonly DispatcherTimer _attachTimer;
    private readonly DispatcherTimer _saveTimer;

    private IReadOnlyList<MonitorInfo> _cachedMonitors;
    private string _currentSignature;
    private string? _reviewEvaluatedDate;
    private WidgetMode _mode = WidgetMode.Desktop;
    private bool _isWidgetVisible;
    private bool _hotkeyWarningShown;
    private bool _disposed;

    public WidgetController(
        AppDatabase database,
        SettingsRepository settings,
        DisplayLayoutRepository layouts,
        TaskListService tasks,
        RoutineService routines,
        ContainerService containers,
        ExamService exams,
        ExamPlanningService examPlanning,
        PlanningService planning,
        DailyReviewService review,
        IClock clock,
        IMonitorProvider monitors,
        IStartupRegistration startup)
    {
        _database = database;
        _settings = settings;
        _layouts = layouts;
        _clock = clock;
        _monitors = monitors;
        _startup = startup;

        _cachedMonitors = _monitors.GetMonitors();
        _currentSignature = DisplaySignature.Compute(_cachedMonitors);

        _routines = routines;
        _planning = planning;

        _viewModel = new MainViewModel(tasks, routines, exams, planning, review, clock);
        _window = new MainWindow { DataContext = _viewModel };
        _window.BoundsChangeCommitted += OnBoundsChangeCommitted;
        _viewModel.AddFieldFocusRequested += _window.FocusNewTaskField;
        _viewModel.OpenMainAppRequested += ShowMainApp;
        _viewModel.OpenDailyPlanRequested += ShowDailyPlan;
        _viewModel.OpenClaudeChatRequested += OpenClaudeChat;

        // V0.2: o aplicativo principal organiza; o widget continua sendo só a execução do dia.
        _appViewModel = new MainAppViewModel(tasks, routines, containers, exams, examPlanning, planning, clock);
        _appWindow = new MainAppWindow { DataContext = _appViewModel };
        _appViewModel.NewTaskFocusRequested += _appWindow.FocusNewTaskField;
        _appViewModel.TasksChanged += OnTasksChangedInMainApp;

        _messageWindow = new AppMessageWindow();
        _messageWindow.ShowRequested += OnShowRequestedByOtherInstance;
        _messageWindow.ShellRestarted += OnShellRestarted;
        _messageWindow.HotkeyPressed += OnHotkeyPressed;

        _hotkey = new GlobalHotkeyService(_messageWindow.Handle);
        _tray = new TrayIconService();

        _tray.ToggleVisibilityRequested += ToggleVisibility;
        _tray.OpenMainAppRequested += ShowMainApp;
        _tray.ThemeSelected += ApplyTheme;
        _tray.StartupToggled += SetStartupEnabled;
        _tray.ExitRequested += RequestExit;

        _dayTimer = new DispatcherTimer { Interval = DayCheckInterval };
        _dayTimer.Tick += (_, _) => CheckDayChange();

        _attachTimer = new DispatcherTimer { Interval = AttachCheckInterval };
        _attachTimer.Tick += (_, _) => EnsureDesktopAttachment();

        _saveTimer = new DispatcherTimer { Interval = SaveDebounce };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveLayout();
        };
    }

    /// <summary>Disparado quando o usuario escolhe Sair.</summary>
    public event Action? ExitRequested;

    public void Start()
    {
        // Gera as ocorrências do horizonte antes de mostrar qualquer coisa.
        _routines.GenerateAll();

        // E traz o que ficou para trás, antes da primeira tela aparecer.
        CarryForward();

        ApplyTheme(LoadTheme(), persist: false);
        InitializeStartupOption();

        ShowWidget(WidgetMode.Desktop, focus: false);

        if (!_hotkey.TryRegister())
        {
            ShowHotkeyWarning();
        }

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _dayTimer.Start();
        _attachTimer.Start();
    }

    // ---------------- Visibilidade e modos ----------------

    private void ShowWidget(WidgetMode mode, bool focus)
    {
        if (!_window.IsVisible)
        {
            _window.Show();
            RestoreLayout();
        }

        ApplyMode(mode, focus);

        _isWidgetVisible = true;
        _tray.SetVisibilityState(true);

        _viewModel.Reload();
        EvaluateReviewOnActivation();
    }

    /// <summary>Abre o aplicativo principal, trazendo-o para a frente se já estiver aberto.</summary>
    private void ShowMainApp()
    {
        _appViewModel.Reload();

        if (!_appWindow.IsVisible)
        {
            _appWindow.Show();
        }

        if (_appWindow.WindowState == global::System.Windows.WindowState.Minimized)
        {
            _appWindow.WindowState = global::System.Windows.WindowState.Normal;
        }

        _appWindow.Activate();
    }

    /// <summary>Abre o aplicativo principal já na seção "Plano do dia".</summary>
    private static void OpenClaudeChat() => ClaudeChat.Open();

    private void ShowDailyPlan()
    {
        _appViewModel.SelectedSection = AppSection.DailyPlan;
        ShowMainApp();
    }

    /// <summary>O que muda no aplicativo principal precisa aparecer no widget na hora.</summary>
    private void OnTasksChangedInMainApp() => _viewModel.Reload();

    private void HideWidget()
    {
        SaveLayout();
        _window.Hide();
        _isWidgetVisible = false;
        _tray.SetVisibilityState(false);
    }

    private void ToggleVisibility()
    {
        if (_isWidgetVisible && _window.IsVisible)
        {
            HideWidget();
            return;
        }

        // Ao voltar pela bandeja, o widget retorna ao modo area de trabalho.
        ShowWidget(WidgetMode.Desktop, focus: false);
    }

    /// <summary>Ctrl + Shift + Q alterna entre area de trabalho e primeiro plano.</summary>
    private void ToggleMode()
    {
        if (!_isWidgetVisible || !_window.IsVisible)
        {
            // Oculto: o primeiro acionamento mostra direto em primeiro plano.
            ShowWidget(WidgetMode.Foreground, focus: true);
            return;
        }

        var next = _mode == WidgetMode.Desktop ? WidgetMode.Foreground : WidgetMode.Desktop;
        ApplyMode(next, focus: next == WidgetMode.Foreground);
    }

    private void ApplyMode(WidgetMode mode, bool focus)
    {
        var bounds = _window.GetPhysicalBounds();

        if (mode == WidgetMode.Desktop)
        {
            _window.Topmost = false;
            _window.PinBelowApplications(_desktopHost.ResolveHost());
            _desktopHost.Pin(_window.Handle);
            _window.SetPhysicalBounds(bounds);
        }
        else
        {
            _window.UnpinFromDesktop();
            NativeMethods.ShowWindow(_window.Handle, NativeMethods.SW_SHOW);
            _window.Topmost = true;
            _window.SetPhysicalBounds(bounds);

            if (focus)
            {
                _window.Activate();
                NativeMethods.SetForegroundWindow(_window.Handle);
            }
        }

        _mode = mode;
    }

    /// <summary>
    /// Reancora o widget na área de trabalho. Roda periodicamente porque nem toda
    /// perda de ancoragem avisa a janela: "Mostrar área de trabalho" eleva o Progman
    /// acima de nós, e um reinício do Explorer destrói a âncora anterior.
    /// </summary>
    private void EnsureDesktopAttachment()
    {
        if (_mode != WidgetMode.Desktop || !_isWidgetVisible || !_window.IsVisible)
        {
            return;
        }

        var host = _desktopHost.ResolveHost();
        if (host == IntPtr.Zero)
        {
            return;
        }

        _window.PinBelowApplications(host);
        _desktopHost.Pin(_window.Handle);
    }

    private void OnShellRestarted()
    {
        AppLog.Info("Explorer reiniciado; recriando icone e reanexando o widget.");
        _tray.Refresh();
        _tray.SetVisibilityState(_isWidgetVisible);

        if (_mode == WidgetMode.Desktop && _isWidgetVisible)
        {
            EnsureDesktopAttachment();
        }
    }

    private void OnShowRequestedByOtherInstance()
    {
        AppLog.Info("Segunda execucao detectada; mostrando o widget existente.");
        ShowWidget(WidgetMode.Foreground, focus: true);
    }

    private void OnHotkeyPressed(int id)
    {
        if (id == _hotkey.Id)
        {
            ToggleMode();
        }
    }

    private void ShowHotkeyWarning()
    {
        if (_hotkeyWarningShown)
        {
            return;
        }

        _hotkeyWarningShown = true;
        _tray.ShowMessage(
            "Hoje Widget",
            "O atalho Ctrl + Shift + Q já está em uso por outro programa. Use o ícone da bandeja para mostrar ou ocultar o widget.");
    }

    // ---------------- Tema e inicializacao ----------------

    private AppTheme LoadTheme()
        => _settings.GetInt(SettingsRepository.ThemeKey, (int)AppTheme.Black) == (int)AppTheme.Translucent
            ? AppTheme.Translucent
            : AppTheme.Black;

    private void ApplyTheme(AppTheme theme) => ApplyTheme(theme, persist: true);

    private void ApplyTheme(AppTheme theme, bool persist)
    {
        ThemeManager.Apply(theme);
        _window.ApplyTheme(theme);
        _tray.SetTheme(theme);

        if (persist)
        {
            _settings.SetInt(SettingsRepository.ThemeKey, (int)theme);
        }
    }

    private void InitializeStartupOption()
    {
        if (!_settings.GetBool(SettingsRepository.StartupInitializedKey, false))
        {
            // Ativada por padrao na primeira execucao.
            _startup.Enable();
            _settings.SetBool(SettingsRepository.StartupInitializedKey, true);
        }
        else if (_startup.IsEnabled)
        {
            // Regrava o caminho: se o executavel foi movido de pasta, a
            // inicializacao com o Windows continua apontando para o lugar certo.
            _startup.Enable();
        }

        _tray.SetStartupEnabled(_startup.IsEnabled);
    }

    private void SetStartupEnabled(bool enabled)
    {
        if (enabled)
        {
            _startup.Enable();
        }
        else
        {
            _startup.Disable();
        }

        _tray.SetStartupEnabled(_startup.IsEnabled);
    }

    // ---------------- Layout por configuracao de monitores ----------------

    private void RestoreLayout()
    {
        _cachedMonitors = _monitors.GetMonitors();
        _currentSignature = DisplaySignature.Compute(_cachedMonitors);

        var stored = _layouts.Get(_currentSignature);
        var placement = WidgetPlacement.Resolve(stored, _cachedMonitors);

        AppLog.Info(stored is null
            ? $"Layout de fábrica em {placement.Bounds} ({_cachedMonitors.Count} monitor(es))."
            : $"Layout restaurado em {placement.Bounds} a partir de {stored.MonitorDevice} ({stored.WidthDip:0.#}x{stored.HeightDip:0.#} DIPs).");

        _window.SetPhysicalBounds(placement.Bounds);
    }

    private void OnBoundsChangeCommitted()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveLayout()
    {
        if (_disposed || !_window.IsVisible)
        {
            return;
        }

        var bounds = _window.GetPhysicalBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        try
        {
            var layout = WidgetPlacement.ToLayout(bounds, _cachedMonitors, _currentSignature, _clock.UtcNow);
            _layouts.Save(layout);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Falha ao salvar o layout do widget.", ex);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Salva o layout da topologia anterior antes de trocar, para nao sobrescreve-lo.
        _saveTimer.Stop();
        SaveLayout();

        var monitors = _monitors.GetMonitors();
        var signature = DisplaySignature.Compute(monitors);

        _cachedMonitors = monitors;

        if (!string.Equals(signature, _currentSignature, StringComparison.Ordinal))
        {
            AppLog.Info("Configuracao de monitores alterada; restaurando o layout correspondente.");
            _currentSignature = signature;
        }

        ApplyStoredOrClampedLayout();
    }

    /// <summary>Aplica o layout da assinatura atual e garante que o widget continue acessivel.</summary>
    private void ApplyStoredOrClampedLayout()
    {
        var stored = _layouts.Get(_currentSignature);
        var placement = stored is not null
            ? WidgetPlacement.Resolve(stored, _cachedMonitors)
            : WidgetPlacement.Clamp(
                _window.GetPhysicalBounds(),
                _cachedMonitors,
                _cachedMonitors.FirstOrDefault(m => m.IsPrimary) ?? _cachedMonitors[0]);

        _window.SetPhysicalBounds(placement.Bounds);

        if (_mode == WidgetMode.Desktop && _isWidgetVisible)
        {
            EnsureDesktopAttachment();
        }
    }

    // ---------------- Mudanca de dia ----------------

    /// <summary>
    /// Traz para hoje as pendências que ficaram em dias anteriores. O usuário não
    /// precisa arrastar nada à mão todo dia.
    /// </summary>
    private void CarryForward()
    {
        var moved = _planning.CarryForwardOverdue();
        if (moved > 0)
        {
            AppLog.Info($"{moved} pendencia(s) de dias anteriores trazida(s) para hoje.");
        }
    }

    private void CheckDayChange()
    {
        if (_viewModel.RefreshForDayChange())
        {
            AppLog.Info("Mudanca de data local aplicada.");
            _routines.GenerateAll();
            CarryForward();
            _viewModel.Reload();
            _appViewModel.Reload();
            EvaluateReviewOnActivation();
        }
    }

    /// <summary>O aviso de revisao e avaliado no maximo uma vez por data local.</summary>
    private void EvaluateReviewOnActivation()
    {
        var today = _clock.LocalDate;
        if (string.Equals(_reviewEvaluatedDate, today, StringComparison.Ordinal))
        {
            return;
        }

        _reviewEvaluatedDate = today;
        _viewModel.EvaluateDailyReview();
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        AppLog.Info("Retomada do sistema; revalidando data, layout e ancoragem.");
        CheckDayChange();
        _cachedMonitors = _monitors.GetMonitors();
        _currentSignature = DisplaySignature.Compute(_cachedMonitors);
        ApplyStoredOrClampedLayout();
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        AppLog.Info("Relogio do sistema alterado; revalidando a data local.");
        CheckDayChange();
    }

    // ---------------- Encerramento ----------------

    private void RequestExit() => ExitRequested?.Invoke();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _dayTimer.Stop();
        _attachTimer.Stop();
        _saveTimer.Stop();

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        if (_window.IsVisible)
        {
            var bounds = _window.GetPhysicalBounds();
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                try
                {
                    _layouts.Save(WidgetPlacement.ToLayout(bounds, _cachedMonitors, _currentSignature, _clock.UtcNow));
                }
                catch (Exception ex)
                {
                    AppLog.Warn("Falha ao salvar o layout no encerramento.", ex);
                }
            }
        }

        _tray.ToggleVisibilityRequested -= ToggleVisibility;
        _tray.OpenMainAppRequested -= ShowMainApp;
        _tray.ThemeSelected -= ApplyTheme;
        _tray.StartupToggled -= SetStartupEnabled;
        _tray.ExitRequested -= RequestExit;
        _tray.Dispose();

        _hotkey.Dispose();

        _messageWindow.ShowRequested -= OnShowRequestedByOtherInstance;
        _messageWindow.ShellRestarted -= OnShellRestarted;
        _messageWindow.HotkeyPressed -= OnHotkeyPressed;
        _messageWindow.Dispose();

        _window.BoundsChangeCommitted -= OnBoundsChangeCommitted;
        _viewModel.AddFieldFocusRequested -= _window.FocusNewTaskField;
        _viewModel.OpenMainAppRequested -= ShowMainApp;
        _viewModel.OpenDailyPlanRequested -= ShowDailyPlan;
        _viewModel.OpenClaudeChatRequested -= OpenClaudeChat;
        _window.Close();

        _appViewModel.NewTaskFocusRequested -= _appWindow.FocusNewTaskField;
        _appViewModel.TasksChanged -= OnTasksChangedInMainApp;
        _appWindow.CloseForShutdown();

        _database.Dispose();

        AppLog.Info("Aplicativo encerrado.");
    }
}
