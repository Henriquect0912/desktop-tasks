using HojeWidget.Domain;
using HojeWidget.Models;
using HojeWidget.ViewModels;

namespace HojeWidget.Tests;

/// <summary>
/// Lembretes: as tarefas sem data, no terço de baixo do widget.
///
/// Não são um conceito novo — são as mesmas tarefas de "Algum dia". O que estes
/// testes guardam é que as duas listas do widget não se misturam: o que tem data
/// fica em Hoje, o que não tem fica nos lembretes, e riscar um não mexe no outro.
/// </summary>
public class ReminderTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 9, 0, 0, DateTimeKind.Local);

    private static MainViewModel Widget(RoutineFixture fixture)
        => new(fixture.Tasks, fixture.Routines, fixture.Exams, fixture.Planning, fixture.Review, fixture.Clock);

    private static void AddReminder(MainViewModel widget, string text)
    {
        widget.BeginAddReminder();
        widget.NewReminderText = text;
        widget.CommitAddReminder();
    }

    [Fact]
    public void LembreteNasceSemDataEFicaForaDeHoje()
    {
        using var fixture = new RoutineFixture(Now);
        fixture.Tasks.AddForToday("Academia");
        var widget = Widget(fixture);

        AddReminder(widget, "Renovar passaporte");

        var task = fixture.Tasks.GetAll().Single(t => t.Title == "Renovar passaporte");
        Assert.Null(task.PlannedDate);

        Assert.Equal(new[] { "Renovar passaporte" }, widget.Reminders.Select(r => r.Title));
        Assert.Equal(new[] { "Academia" }, widget.Items.Select(i => i.Title));
    }

    [Fact]
    public void OCampoFicaAbertoParaOProximoLembrete()
    {
        using var fixture = new RoutineFixture(Now);
        var widget = Widget(fixture);

        AddReminder(widget, "Renovar passaporte");

        Assert.True(widget.IsAddingReminder);
        Assert.Equal(string.Empty, widget.NewReminderText);

        widget.NewReminderText = "Comprar pilha";
        widget.CommitAddReminder();

        Assert.Equal(new[] { "Renovar passaporte", "Comprar pilha" }, widget.Reminders.Select(r => r.Title));
    }

    [Fact]
    public void TextoVazioNaoCriaLembrete()
    {
        using var fixture = new RoutineFixture(Now);
        var widget = Widget(fixture);

        AddReminder(widget, "   ");

        Assert.Empty(fixture.Tasks.GetAll());
        Assert.Empty(widget.Reminders);
        Assert.True(widget.IsAddingReminder);
    }

    [Fact]
    public void RiscarLembreteNaoDizTudoConcluidoPorHoje()
    {
        using var fixture = new RoutineFixture(Now);
        var widget = Widget(fixture);
        AddReminder(widget, "Renovar passaporte");

        widget.ToggleCompleted(widget.Reminders.Single(), completed: true);

        // Hoje não tinha nada e continua sem nada: o lembrete não é de hoje.
        Assert.True(widget.IsEmpty);
        Assert.False(widget.IsAllDone);
        Assert.Empty(widget.Items);

        // O lembrete continua visível, riscado, até o dia virar.
        var reminder = Assert.Single(widget.Reminders);
        Assert.True(reminder.IsCompleted);
    }

    [Fact]
    public void LembreteConcluidoSaiNaViradaDoDia()
    {
        using var fixture = new RoutineFixture(Now);
        var widget = Widget(fixture);
        AddReminder(widget, "Renovar passaporte");
        widget.ToggleCompleted(widget.Reminders.Single(), completed: true);

        fixture.Clock.AdvanceDays(1);
        widget.RefreshForDayChange();

        Assert.Empty(widget.Reminders);
    }

    [Fact]
    public void LembretePendenteNaoEArrastadoParaHoje()
    {
        using var fixture = new RoutineFixture(Now);
        var widget = Widget(fixture);
        AddReminder(widget, "Renovar passaporte");

        // Passam três dias e o que ficou para trás é trazido para hoje — mas um
        // lembrete nunca ficou para trás, porque nunca teve dia.
        fixture.Clock.AdvanceDays(3);
        fixture.Planning.CarryForwardOverdue();
        widget.RefreshForDayChange();

        Assert.Null(fixture.Tasks.GetAll().Single().PlannedDate);
        Assert.Single(widget.Reminders);
        Assert.Empty(widget.Items);
    }

    [Fact]
    public void SaoAsMesmasTarefasDeAlgumDia()
    {
        using var fixture = new RoutineFixture(Now);

        // Criada no aplicativo principal, sem data.
        fixture.Tasks.Add("Aprender violão", plannedDate: null);
        var widget = Widget(fixture);

        Assert.Equal(new[] { "Aprender violão" }, widget.Reminders.Select(r => r.Title));
        Assert.Equal(
            fixture.Tasks.GetSomeday().Pending.Select(t => t.Id),
            widget.Reminders.Select(r => r.Id));
    }

    [Fact]
    public void EtapaDeProjetoSemDataNaoViraLembrete()
    {
        using var fixture = new RoutineFixture(Now);
        var projeto = fixture.Containers.Create(ContainerKind.Project, "Mestrado")!;
        fixture.Containers.AddSubtask(projeto.Id, "Ler editais");

        var widget = Widget(fixture);

        // A etapa já tem lugar: o projeto. Repeti-la aqui seria ruído.
        Assert.Empty(widget.Reminders);
    }

    [Fact]
    public void RodapeContaOsLembretesPendentes()
    {
        using var fixture = new RoutineFixture(Now);
        fixture.Tasks.AddForToday("Academia");
        var widget = Widget(fixture);

        // Sem lembrete, o rodapé é o de sempre.
        Assert.Equal("1 pendente", widget.FooterText);

        AddReminder(widget, "Renovar passaporte");
        Assert.Equal("1 pendente · 1 lembrete", widget.FooterText);

        widget.NewReminderText = "Comprar pilha";
        widget.CommitAddReminder();
        Assert.Equal("1 pendente · 2 lembretes", widget.FooterText);

        // Riscado não conta.
        widget.ToggleCompleted(widget.Reminders.First(), completed: true);
        Assert.Equal("1 pendente · 1 lembrete", widget.FooterText);
    }

    [Fact]
    public void AbrirUmCampoFechaOOutro()
    {
        using var fixture = new RoutineFixture(Now);
        var widget = Widget(fixture);

        widget.BeginAdd();
        widget.BeginAddReminder();
        Assert.False(widget.IsAdding);
        Assert.True(widget.IsAddingReminder);

        widget.BeginAdd();
        Assert.True(widget.IsAdding);
        Assert.False(widget.IsAddingReminder);
    }

    [Fact]
    public void EditarEExcluirLembrete()
    {
        using var fixture = new RoutineFixture(Now);
        var widget = Widget(fixture);
        AddReminder(widget, "Renovar pasaporte");

        var reminder = widget.Reminders.Single();
        widget.BeginEdit(reminder);
        reminder.EditText = "Renovar passaporte";
        widget.CommitEdit(reminder);

        Assert.Equal("Renovar passaporte", widget.Reminders.Single().Title);

        widget.RequestDelete(widget.Reminders.Single());
        widget.ConfirmDelete();

        Assert.Empty(widget.Reminders);
        Assert.Empty(fixture.Tasks.GetAll());
    }

    [Fact]
    public void EditarUmaLinhaFechaAEdicaoDaOutraLista()
    {
        using var fixture = new RoutineFixture(Now);
        fixture.Tasks.AddForToday("Academia");
        var widget = Widget(fixture);
        AddReminder(widget, "Renovar passaporte");

        widget.BeginEdit(widget.Items.Single());
        widget.BeginEdit(widget.Reminders.Single());

        Assert.False(widget.Items.Single().IsEditing);
        Assert.True(widget.Reminders.Single().IsEditing);
    }

    [Fact]
    public void HojeNaoListaConcluidaSemData()
    {
        var today = DateOnly.FromDateTime(Now);
        var todayText = LocalDate.From(today);

        var tasks = new[]
        {
            new TaskItem
            {
                Id = "a", Title = "Academia", PlannedDate = today,
                IsCompleted = true, CompletedLocalDate = todayText,
            },
            new TaskItem
            {
                Id = "b", Title = "Renovar passaporte", PlannedDate = null,
                IsCompleted = true, CompletedLocalDate = todayText,
            },
        };

        var hoje = TaskArranger.Arrange(tasks, today);
        var algumDia = TaskArranger.Someday(tasks, today);

        Assert.Equal(new[] { "Academia" }, hoje.CompletedToday.Select(t => t.Title));
        Assert.Equal(new[] { "Renovar passaporte" }, algumDia.CompletedToday.Select(t => t.Title));
    }

    [Theory]
    [InlineData(0, 0, "0 pendentes")]
    [InlineData(1, 0, "1 pendente")]
    [InlineData(1, 1, "1 pendente · 1 lembrete")]
    [InlineData(3, 2, "3 pendentes · 2 lembretes")]
    [InlineData(0, 4, "0 pendentes · 4 lembretes")]
    public void TextoDoRodape(int pending, int reminders, string expected)
        => Assert.Equal(expected, PendingCounter.WithReminders(pending, reminders));
}
