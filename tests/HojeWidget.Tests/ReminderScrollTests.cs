using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.ViewModels;
using HojeWidget.Views;

namespace HojeWidget.Tests;

/// <summary>
/// O terço de baixo do widget é pequeno: com mais lembretes do que cabem, o
/// recém-criado entraria fora de vista, no fim da lista. O campo fica no
/// cabeçalho, no lugar do título, e o Enter rola a lista até o lembrete novo.
///
/// A rolagem do WPF só se completa quando a fila de eventos roda, então aqui a
/// janela é mostrada de verdade (fora da tela) e a fila é processada, em vez do
/// desenho fora de tela usado pelos outros testes.
/// </summary>
[Collection(WpfCollection.Name)]
public class ReminderScrollTests
{
    [Fact]
    public void OCampoEOLembreteNovoFicamVisiveis()
    {
        var (result, emptyScrollBar) = RunOnStaThread(() =>
        {
            EnsureApplicationElsewhere();
            ThemeManager.Apply(AppTheme.Black);

            using var fixture = new RoutineFixture(new DateTime(2026, 9, 26, 9, 0, 0, DateTimeKind.Local));
            fixture.Tasks.AddForToday("Academia");
            for (var i = 1; i <= 6; i++)
            {
                fixture.Tasks.Add($"Lembrete {i}", plannedDate: null);
            }

            var model = new MainViewModel(
                fixture.Tasks, fixture.Routines, fixture.Exams, fixture.Planning, fixture.Review, fixture.Clock);

            var window = new MainWindow
            {
                DataContext = model,
                Left = -32000,
                Top = -32000,
                Width = 410,
                Height = 300,
                ShowActivated = false,
            };

            try
            {
                window.Show();
                DoEvents();

                var scroll = (ScrollViewer)window.FindName("RemindersScroll");
                var field = (FrameworkElement)window.FindName("NewReminderBox");
                var host = (ItemsControl)window.FindName("RemindersHost");

                // Seis lembretes não cabem no terço de baixo: a lista precisa rolar.
                var listScrolls = scroll.ScrollableHeight > 0;

                // O "+" clicado como um usuário clicaria: evento e comando.
                var button = (Button)window.FindName("AddReminderButton");
                var peer = new ButtonAutomationPeer(button);
                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
                DoEvents();

                var fieldOnOpen = IsDrawn(field, window);

                // Enter no campo cria o lembrete e o mantém à vista.
                model.NewReminderText = "Renovar passaporte";
                var enter = new KeyEventArgs(
                    Keyboard.PrimaryDevice, PresentationSource.FromVisual(field)!, 0, Key.Enter)
                {
                    RoutedEvent = Keyboard.KeyDownEvent,
                };
                field.RaiseEvent(enter);
                DoEvents();

                var created = model.Reminders.Last();
                var row = (FrameworkElement)host.ItemContainerGenerator.ContainerFromItem(created);

                var report = (
                    ListScrolls: listScrolls,
                    FieldOnOpen: fieldOnOpen,
                    CreatedTitle: created.Title,
                    RowAfterEnter: IsInsideViewport(row, scroll),
                    FieldAfterEnter: IsDrawn(field, window),
                    StillAdding: model.IsAddingReminder);

                Dump((FrameworkElement)window.Content, "preto-lembrete-campo.png");

                // Sem lembrete nenhum, a lista vazia não deixa barra de rolagem para trás.
                model.CancelAddReminder();
                foreach (var id in model.Reminders.Select(r => r.Id).ToList())
                {
                    fixture.Tasks.Delete(id);
                }

                model.Reload();
                DoEvents();
                Dump((FrameworkElement)window.Content, "preto-sem-lembretes-real.png");

                return (report, EmptyScrollBar: scroll.ComputedVerticalScrollBarVisibility);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.True(result.ListScrolls, "com seis lembretes a lista deveria rolar");
        Assert.True(result.FieldOnOpen, "o campo de lembrete não apareceu");
        Assert.Equal("Renovar passaporte", result.CreatedTitle);
        Assert.True(result.RowAfterEnter, "o lembrete recém-criado ficou fora de vista");
        Assert.True(result.FieldAfterEnter, "o campo sumiu de vista depois do Enter");
        Assert.True(result.StillAdding);
        Assert.Equal(Visibility.Collapsed, emptyScrollBar);
    }

    /// <summary>
    /// Os recursos do aplicativo (tema, estilos, conversores) vêm do <see cref="App"/>,
    /// mas o construtor de um <see cref="Application"/> agenda o <c>OnStartup</c> na
    /// fila da thread que o cria. Se fosse esta, o <see cref="DoEvents"/> rodaria a
    /// inicialização de verdade do widget: abriria o banco real e, com o widget
    /// aberto, avisaria a instância em execução e se desligaria. Por isso o App
    /// nasce numa thread à parte que nunca processa a fila.
    /// </summary>
    private static void EnsureApplicationElsewhere()
    {
        if (Application.Current is not null)
        {
            return;
        }

        var thread = new Thread(() =>
        {
            var app = new App();
            app.InitializeComponent();
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    /// <summary>Visível e inteiro dentro da janela.</summary>
    private static bool IsDrawn(FrameworkElement element, Window window)
    {
        var content = (FrameworkElement)window.Content;
        var top = element.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
        return element.IsVisible && element.ActualHeight > 0
               && top >= 0 && top + element.ActualHeight <= content.ActualHeight;
    }

    private static bool IsInsideViewport(FrameworkElement element, ScrollViewer scroll)
    {
        var top = element.TransformToAncestor(scroll).Transform(new Point(0, 0)).Y;
        return top >= -0.5 && top + element.ActualHeight <= scroll.ViewportHeight + 0.5;
    }

    /// <summary>Processa a fila de eventos até ela ficar ociosa.</summary>
    private static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new DispatcherOperationCallback(f =>
            {
                ((DispatcherFrame)f!).Continue = false;
                return null;
            }),
            frame);
        Dispatcher.PushFrame(frame);
    }

    private static void Dump(FrameworkElement content, string name)
    {
        try
        {
            var bitmap = new RenderTargetBitmap(
                (int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);

            var directory = Path.Combine(Path.GetTempPath(), "HojeWidgetRender");
            Directory.CreateDirectory(directory);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(directory, name));
            encoder.Save(stream);
        }
        catch (IOException)
        {
            // O PNG é só apoio de diagnóstico.
        }
    }

    private static T RunOnStaThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("Falha ao exercitar a janela.", failure);
        }

        return result;
    }
}
