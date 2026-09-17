using HojeWidget.Domain;
using HojeWidget.Models;

namespace HojeWidget.Tests;

public class TaskArrangerTests
{
    private static readonly DateOnly Today = new(2026, 3, 10);

    private static TaskItem Task(
        string id,
        long sortOrder,
        bool completed = false,
        string? completedDate = null,
        int completedMinute = 0,
        DateOnly? planned = null,
        bool someday = false)
        => new()
        {
            Id = id,
            Title = id,
            SortOrder = sortOrder,
            IsCompleted = completed,
            CompletedLocalDate = completedDate,
            CompletedAtUtc = completed ? new DateTime(2026, 3, 10, 10, completedMinute, 0, DateTimeKind.Utc) : null,
            PlannedDate = someday ? null : planned ?? Today,
            CreatedAtUtc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
        };

    [Fact]
    public void PendentesVemAntesDasConcluidas()
    {
        var items = new[]
        {
            Task("concluida", 0, completed: true, completedDate: "2026-03-10"),
            Task("pendente", 1),
        };

        var snapshot = TaskArranger.Arrange(items, Today);

        Assert.Equal(new[] { "pendente", "concluida" }, snapshot.Display.Select(t => t.Id));
    }

    [Fact]
    public void ConcluidasDeOutrasDatasSaoEscondidas()
    {
        var items = new[]
        {
            Task("hoje", 0, completed: true, completedDate: "2026-03-10"),
            Task("ontem", 1, completed: true, completedDate: "2026-03-09"),
        };

        var snapshot = TaskArranger.Arrange(items, Today);

        Assert.Equal(new[] { "hoje" }, snapshot.CompletedToday.Select(t => t.Id));
        Assert.Equal(ListState.AllDone, snapshot.State);
    }

    [Fact]
    public void PendentesSeguemAOrdemManual()
    {
        var items = new[] { Task("c", 30), Task("a", 10), Task("b", 20) };

        var snapshot = TaskArranger.Arrange(items, Today);

        Assert.Equal(new[] { "a", "b", "c" }, snapshot.Pending.Select(t => t.Id));
    }

    [Fact]
    public void ConcluidasDeHojeSeguemAOrdemDeConclusao()
    {
        var items = new[]
        {
            Task("segunda", 10, completed: true, completedDate: "2026-03-10", completedMinute: 30),
            Task("primeira", 20, completed: true, completedDate: "2026-03-10", completedMinute: 5),
        };

        var snapshot = TaskArranger.Arrange(items, Today);

        Assert.Equal(new[] { "primeira", "segunda" }, snapshot.CompletedToday.Select(t => t.Id));
    }

    // ---------- V0.2: o que entra em Hoje ----------

    [Fact]
    public void TarefaPlanejadaParaHojeApareceEmHoje()
    {
        var snapshot = TaskArranger.Arrange(new[] { Task("hoje", 0, planned: Today) }, Today);

        Assert.Equal(new[] { "hoje" }, snapshot.Pending.Select(t => t.Id));
    }

    [Fact]
    public void TarefaPlanejadaParaODiaSeguinteNaoApareceEmHoje()
    {
        var snapshot = TaskArranger.Arrange(new[] { Task("amanha", 0, planned: Today.AddDays(1)) }, Today);

        Assert.Empty(snapshot.Pending);
        Assert.Equal(ListState.Empty, snapshot.State);
    }

    [Fact]
    public void TarefaAtrasadaNoPlanejamentoContinuaEmHoje()
    {
        var snapshot = TaskArranger.Arrange(new[] { Task("ontem", 0, planned: Today.AddDays(-1)) }, Today);

        Assert.Equal(new[] { "ontem" }, snapshot.Pending.Select(t => t.Id));
    }

    [Fact]
    public void TarefaSemDataPlanejadaFicaForaDeHoje()
    {
        var items = new[] { Task("algum dia", 0, someday: true) };

        Assert.Empty(TaskArranger.Arrange(items, Today).Pending);
        Assert.Equal(new[] { "algum dia" }, TaskArranger.Someday(items, Today).Pending.Select(t => t.Id));
    }

    [Fact]
    public void AlgumDiaNaoMostraTarefasPlanejadas()
    {
        var items = new[] { Task("planejada", 0, planned: Today), Task("solta", 1, someday: true) };

        Assert.Equal(new[] { "solta" }, TaskArranger.Someday(items, Today).Pending.Select(t => t.Id));
    }

    [Fact]
    public void SemanaColocaCadaTarefaNoSeuDia()
    {
        var monday = new DateOnly(2026, 3, 9);
        var items = new[]
        {
            Task("segunda", 0, planned: monday),
            Task("quarta", 1, planned: monday.AddDays(2)),
            Task("solta", 2, someday: true),
            Task("outra semana", 3, planned: monday.AddDays(10)),
        };

        var week = TaskArranger.Week(items, monday);

        Assert.Equal(7, week.Count);
        Assert.Equal(new[] { "segunda" }, week[0].All.Select(t => t.Id));
        Assert.Empty(week[1].All);
        Assert.Equal(new[] { "quarta" }, week[2].All.Select(t => t.Id));
        Assert.All(week, day => Assert.DoesNotContain(day.All, t => t.Id is "solta" or "outra semana"));
    }

    [Fact]
    public void SemanaComecaNaSegundaFeira()
    {
        // 2026-03-10 é uma terça-feira.
        Assert.Equal(new DateOnly(2026, 3, 9), LocalDate.StartOfWeek(Today));
        Assert.Equal(new DateOnly(2026, 3, 9), LocalDate.StartOfWeek(new DateOnly(2026, 3, 15))); // domingo
    }

    // ---------- Reordenação ----------

    [Fact]
    public void ReordenarReaproveitaAsMesmasPosicoes()
    {
        var pending = new[] { Task("a", 5), Task("b", 9), Task("c", 12) };

        var assignments = TaskArranger.BuildReorder(pending, new[] { "c", "a", "b" });

        Assert.Equal(new (string, long)[] { ("c", 5), ("a", 9), ("b", 12) }, assignments);
    }

    [Fact]
    public void ReordenarIgnoraIdsDesconhecidosEMantemOsOmitidos()
    {
        var pending = new[] { Task("a", 0), Task("b", 1), Task("c", 2) };

        var assignments = TaskArranger.BuildReorder(pending, new[] { "c", "inexistente" });

        // "a" e "b" nao citados permanecem, na ordem atual, depois de "c".
        Assert.Equal(new (string, long)[] { ("c", 0), ("a", 1), ("b", 2) }, assignments);
    }

    [Fact]
    public void ListaSemNadaEEstadoVazio()
    {
        var snapshot = TaskArranger.Arrange(Array.Empty<TaskItem>(), Today);

        Assert.Equal(ListState.Empty, snapshot.State);
        Assert.Equal("0 pendentes", snapshot.PendingCountText);
    }

    [Fact]
    public void ConcluidaDeOntemSozinhaContinuaSendoListaVazia()
    {
        var items = new[] { Task("ontem", 0, completed: true, completedDate: "2026-03-09") };

        var snapshot = TaskArranger.Arrange(items, Today);

        Assert.Equal(ListState.Empty, snapshot.State);
        Assert.Empty(snapshot.Display);
    }

    // ---------- Rótulos ----------

    [Fact]
    public void RotuloDeAtrasoUsaOntemQuandoForOntem()
        => Assert.Equal("planejada para ontem", TaskLabels.PlanningDelay(Today.AddDays(-1), Today));

    [Fact]
    public void RotuloDeAtrasoUsaADataQuandoForMaisAntiga()
        => Assert.Equal("planejada para 05/03", TaskLabels.PlanningDelay(new DateOnly(2026, 3, 5), Today));

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void SemAtrasoNaoHaRotulo(int daysAhead)
        => Assert.Null(TaskLabels.PlanningDelay(Today.AddDays(daysAhead), Today));

    [Fact]
    public void TarefaSemDataPlanejadaNaoTemRotuloDeAtraso()
        => Assert.Null(TaskLabels.PlanningDelay(null, Today));
}
