using System.Diagnostics;
using System.Text;
using System.Windows;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.ViewModels;
using HojeWidget.Views;

namespace HojeWidget.Tests;

/// <summary>
/// Um binding quebrado no WPF não estoura exceção: ele falha calado e a tela fica
/// sutilmente errada — uma seção que não some, um texto que nunca aparece. Este
/// teste liga o rastreamento de binding e falha se alguma seção produzir erro.
/// </summary>
[Collection(WpfCollection.Name)]
public class BindingDiagnosticsTests
{
    [Fact]
    public void NenhumaSecaoProduzErroDeBinding()
    {
        var errors = RunOnStaThread(() =>
        {
            if (Application.Current is null)
            {
                var app = new App();
                app.InitializeComponent();
            }

            var listener = new CollectingListener();
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;

            try
            {
                using var fixture = new RoutineFixture(new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Local));
                fixture.Tasks.AddForToday("Fazer lista de Termodinâmica");

                var projeto = fixture.Containers.Create(
                    ContainerKind.Project, "Preparação para mestrado", fixture.Clock.Today.AddDays(20))!;
                fixture.Containers.AddSubtask(projeto.Id, "Pesquisar programas", fixture.Clock.Today, estimatedMinutes: 60);

                var entrega = fixture.Containers.Create(
                    ContainerKind.Delivery, "Relatório de laboratório", fixture.Clock.Today.AddDays(6))!;
                fixture.Containers.AddSubtask(entrega.Id, "Organizar dados", estimatedMinutes: 45);

                var prova = fixture.Exams.Create("MPP-31", "P1", fixture.Clock.Today.AddDays(4))!;
                var torcao = fixture.Graph.AddSubject(prova.Id, "Torção")!;
                fixture.Graph.AddNode(prova.Id, "Teoria", StudyNodeType.Theory, torcao.Id, 60);
                fixture.Graph.AddExerciseList(prova.Id, "Lista 1", torcao.Id, 90, 12);

                ThemeManager.Apply(AppTheme.Black);

                var model = new MainAppViewModel(
                    fixture.Tasks, fixture.Routines, fixture.Containers, fixture.Exams,
                    fixture.ExamPlanning, fixture.Planning, fixture.Clock);

                var window = new MainAppWindow { DataContext = model };
                var content = (FrameworkElement)window.Content;

                foreach (var section in Enum.GetValues<AppSection>())
                {
                    model.SelectedSection = section;
                    content.Measure(new Size(980, 640));
                    content.Arrange(new Rect(0, 0, 980, 640));
                    content.UpdateLayout();
                }

                return listener.Messages;
            }
            finally
            {
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
            }
        });

        Assert.True(errors.Count == 0, "erros de binding:\n" + string.Join("\n", errors));
    }

    private sealed class CollectingListener : TraceListener
    {
        private readonly StringBuilder _current = new();

        public List<string> Messages { get; } = new();

        public override void Write(string? message) => _current.Append(message);

        public override void WriteLine(string? message)
        {
            _current.Append(message);
            var text = _current.ToString();
            _current.Clear();

            if (text.Contains("BindingExpression", StringComparison.Ordinal))
            {
                Messages.Add(text);
            }
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
            throw new InvalidOperationException("Falha ao montar a janela.", failure);
        }

        return result;
    }
}
