using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using HojeWidget.Models;
using HojeWidget.Services.System;

namespace HojeWidget.Services.Tray;

/// <summary>
/// Icone e menu proximos ao relogio. Nao existe tela de configuracoes: tudo o que
/// o usuario pode ajustar na V0.1 esta aqui.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _visibilityItem;
    private readonly ToolStripMenuItem _themeBlackItem;
    private readonly ToolStripMenuItem _themeTranslucentItem;
    private readonly ToolStripMenuItem _startupItem;

    private bool _suppressEvents;

    public TrayIconService()
    {
        _visibilityItem = new ToolStripMenuItem("Ocultar");
        _visibilityItem.Click += (_, _) => ToggleVisibilityRequested?.Invoke();

        _themeBlackItem = new ToolStripMenuItem("Preto") { CheckOnClick = false };
        _themeBlackItem.Click += (_, _) => RaiseTheme(AppTheme.Black);

        _themeTranslucentItem = new ToolStripMenuItem("Translúcido") { CheckOnClick = false };
        _themeTranslucentItem.Click += (_, _) => RaiseTheme(AppTheme.Translucent);

        var themeItem = new ToolStripMenuItem("Tema");
        themeItem.DropDownItems.Add(_themeBlackItem);
        themeItem.DropDownItems.Add(_themeTranslucentItem);

        _startupItem = new ToolStripMenuItem("Iniciar com o Windows") { CheckOnClick = false };
        _startupItem.Click += (_, _) =>
        {
            if (!_suppressEvents)
            {
                StartupToggled?.Invoke(!_startupItem.Checked);
            }
        };

        var exitItem = new ToolStripMenuItem("Sair");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        // V0.2: com o widget oculto, a bandeja é o único caminho para o aplicativo principal.
        var openAppItem = new ToolStripMenuItem("Abrir aplicativo");
        openAppItem.Click += (_, _) => OpenMainAppRequested?.Invoke();

        var menu = new ContextMenuStrip();
        menu.Items.Add(openAppItem);
        menu.Items.Add(_visibilityItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(themeItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Hoje Widget",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _icon.MouseClick += OnMouseClick;
    }

    public event Action? ToggleVisibilityRequested;
    public event Action? OpenMainAppRequested;
    public event Action<AppTheme>? ThemeSelected;
    public event Action<bool>? StartupToggled;
    public event Action? ExitRequested;

    /// <summary>Atualiza o rotulo Mostrar/Ocultar conforme o estado atual.</summary>
    public void SetVisibilityState(bool isVisible)
        => _visibilityItem.Text = isVisible ? "Ocultar" : "Mostrar";

    public void SetTheme(AppTheme theme)
    {
        _themeBlackItem.Checked = theme == AppTheme.Black;
        _themeTranslucentItem.Checked = theme == AppTheme.Translucent;
    }

    public void SetStartupEnabled(bool enabled)
    {
        _suppressEvents = true;
        _startupItem.Checked = enabled;
        _suppressEvents = false;
    }

    /// <summary>Mensagem curta pelo icone da bandeja. Usada quando a hotkey esta ocupada.</summary>
    public void ShowMessage(string title, string text)
    {
        try
        {
            _icon.ShowBalloonTip(8000, title, text, ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Falha ao exibir mensagem na bandeja.", ex);
        }
    }

    /// <summary>Reexibe o icone apos o Explorer ser reiniciado.</summary>
    public void Refresh()
    {
        _icon.Visible = false;
        _icon.Visible = true;
    }

    private void RaiseTheme(AppTheme theme)
    {
        if (!_suppressEvents)
        {
            ThemeSelected?.Invoke(theme);
        }
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleVisibilityRequested?.Invoke();
        }
    }

    private static Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/HojeWidget;component/Assets/app.ico", UriKind.Absolute);
            var resource = global::System.Windows.Application.GetResourceStream(uri);
            if (resource is not null)
            {
                using var stream = resource.Stream;
                return new Icon(stream);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Falha ao carregar o icone da bandeja; usando o icone padrao.", ex);
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.MouseClick -= OnMouseClick;
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
