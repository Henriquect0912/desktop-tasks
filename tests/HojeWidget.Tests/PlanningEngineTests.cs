using HojeWidget.Domain;
using HojeWidget.Domain.Planning;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.Database;

namespace HojeWidget.Tests;

/// <summary>
/// V0.7 — prioridade, capacidade, sobrecarga, replanejamento e explicações.
///
/// O motor é determinístico de propósito: estes testes fixam o comportamento que
/// o usuário vai ver, não o número interno da nota.
/// </summary>
public class PlanningEngineTests
{
    private static readonly DateOnly Today = new(2026, 9, 6);
    private static readonly DateTime Reference = new(2026, 9, 6, 9, 0, 0, DateTimeKind.Local);

    private static PlannableItem Item(
        string title,
        DateOnly? planned = null,
        DateOnly? due = null,
        int? minutes = null,
        bool canSplit = true,
        Priority priority = Priority.Normal,
        long order = 0,
        DateOnly? exam = null,
        Mastery? mastery = null,
        bool manual = false)
        => new(title, PlannableKind.Task, title, planned ?? Today, due, minutes, canSplit, priority, manual, false, order)
        {
            ExamDate = exam,
            TopicMastery = mastery,
        };

    // ---------- Prioridade ----------

    [Fact]
    public void PrazoAmanhaVemAntesDePrazoDistante()
    {
        var amanha = Item("Relatório", due: Today.AddDays(1));
        var longe = Item("Ler artigo", due: Today.AddDays(20));

        var ranked = PriorityEngine.Rank(new[] { longe, amanha }, Today);

        Assert.Equal("Relatório", ranked[0].Item.Title);
        Assert.Contains("prazo amanhã", ranked[0].Explanation);
    }

    [Fact]
    public void VencidoVemNaFrenteDeTudo()
    {
        var vencido = Item("Entregar formulário", due: Today.AddDays(-2));
        var hoje = Item("Relatório", due: Today);

        var ranked = PriorityEngine.Rank(new[] { hoje, vencido }, Today);

        Assert.Equal("Entregar formulário", ranked[0].Item.Title);
        Assert.Contains("vencido", ranked[0].Explanation);
    }

    [Fact]
    public void PrioridadeManualDesempataEAparece()
    {
        var alta = Item("Relatório", due: Today.AddDays(3), priority: Priority.High, order: 1);
        var normal = Item("Ler artigo", due: Today.AddDays(3), order: 0);
        var baixa = Item("Organizar fotos", due: Today.AddDays(3), priority: Priority.Low, order: 2);

        var ranked = PriorityEngine.Rank(new[] { normal, baixa, alta }, Today);

        Assert.Equal("Relatório", ranked[0].Item.Title);
        Assert.Equal("Organizar fotos", ranked[^1].Item.Title);
        Assert.Contains("prioridade alta", ranked[0].Explanation);
    }

    [Fact]
    public void AtrasoNoPlanejamentoPesaESeExplica()
    {
        var atrasada = Item("Fazer lista", planned: Today.AddDays(-3));
        var deHoje = Item("Comprar pão", planned: Today);

        var ranked = PriorityEngine.Rank(new[] { deHoje, atrasada }, Today);

        Assert.Equal("Fazer lista", ranked[0].Item.Title);
        Assert.Contains("planejada há 3 dias", ranked[0].Explanation);
    }

    [Fact]
    public void ProvaProximaEDominioFracoEmpurramOEstudoParaCima()
    {
        var estudo = new PlannableItem(
            "s1", PlannableKind.StudySession, "MPP-31 — Lista 3", Today, null, 90, true,
            Priority.Normal, false, false, 0)
        {
            ExamDate = Today.AddDays(3),
            TopicMastery = Mastery.Weak,
            ParentTitle = "MPP-31",
        };

        var comum = Item("Ler artigo", due: Today.AddDays(6));

        var ranked = PriorityEngine.Rank(new[] { comum, estudo }, Today);

        Assert.Equal("MPP-31 — Lista 3", ranked[0].Item.Title);
        Assert.Contains("prova em 3 dias", ranked[0].Explanation);
        Assert.Contains(ranked[0].Reasons, r => r.Factor == PlanningFactor.Mastery);
    }

    [Fact]
    public void ConteudoNaoEstudadoPesaMaisQueDominioRazoavel()
    {
        var nao = Item("Capítulo Y", exam: Today.AddDays(5), mastery: Mastery.NotStudied, order: 1);
        var razoavel = Item("Teoria X", exam: Today.AddDays(5), mastery: Mastery.Fair, order: 0);

        var ranked = PriorityEngine.Rank(new[] { razoavel, nao }, Today);

        Assert.Equal("Capítulo Y", ranked[0].Item.Title);
    }

    [Fact]
    public void ExplicacaoMostraMotivosENaoUmNumero()
    {
        var item = Item("Relatório", due: Today.AddDays(1), priority: Priority.High);
        var scored = PriorityEngine.Score(item, Today);

        Assert.Equal("prazo amanhã · prioridade alta", scored.Explanation);
        Assert.DoesNotContain(scored.Score.ToString(), scored.Explanation);
    }

    [Fact]
    public void OrdemEhDeterministica()
    {
        var items = new[]
        {
            Item("A", due: Today.AddDays(2), order: 0),
            Item("B", due: Today.AddDays(2), order: 1),
            Item("C", due: Today.AddDays(2), order: 2),
        };

        var first = PriorityEngine.Rank(items, Today).Select(s => s.Item.Title).ToList();
        var second = PriorityEngine.Rank(items.Reverse().ToList(), Today).Select(s => s.Item.Title).ToList();

        Assert.Equal(new[] { "A", "B", "C" }, first);
        Assert.Equal(first, second);
    }

    // ---------- Capacidade e sobrecarga ----------

    [Fact]
    public void SobrecargaEhDetectada()
    {
        var input = new PlanningInput(Today, new[]
        {
            Item("Relatório", minutes: 180, order: 0),
            Item("Lista 5", minutes: 150, order: 1),
            Item("Academia", minutes: 120, order: 2),
        }, new TestCapacity(180));

        var result = PlanningEngine.Plan(input);

        Assert.Equal(450, result.TodayPlannedMinutes);
        Assert.Equal(180, result.TodayCapacityMinutes);
        Assert.True(result.Days[0].IsOverloaded);
        Assert.Equal(270, result.Days[0].OverloadMinutes);
    }

    [Fact]
    public void DiaDeDescansoNaoContaComoSobrecarregado()
    {
        var domingo = new DateOnly(2026, 9, 6);
        Assert.Equal(DayOfWeek.Sunday, domingo.DayOfWeek);

        var input = new PlanningInput(domingo, new[] { Item("Descansar", planned: domingo) },
            new TestCapacity(180).ClosedOn(DayOfWeek.Sunday));

        var result = PlanningEngine.Plan(input);

        Assert.True(result.Days[0].IsRestDay);
        Assert.False(result.Days[0].IsOverloaded);
    }

    [Fact]
    public void RedistribuirTiraOExcessoESemDiaDeCapacidadeZero()
    {
        var closed = Today.AddDays(1);

        var input = new PlanningInput(Today, new[]
        {
            Item("Relatório", minutes: 180, due: Today.AddDays(10), order: 0),
            Item("Ler artigo", minutes: 120, due: Today.AddDays(10), order: 1),
        }, new TestCapacity(180).Closed(closed));

        var moves = PlanningEngine.Redistribute(input);

        var move = Assert.Single(moves);
        Assert.Equal("Ler artigo", move.Title);
        Assert.NotEqual(closed, move.To);
        Assert.True(move.To > Today);
        Assert.Contains("capacidade", move.Reason);
    }

    [Fact]
    public void RedistribuirNaoMexeNoQueFoiPlanejadoAMao()
    {
        var input = new PlanningInput(Today, new[]
        {
            Item("Relatório", minutes: 180, due: Today.AddDays(10), order: 0, manual: true),
            Item("Ler artigo", minutes: 180, due: Today.AddDays(10), order: 1, manual: true),
        }, new TestCapacity(180));

        Assert.Empty(PlanningEngine.Redistribute(input));
    }

    [Fact]
    public void RedistribuirRespeitaAMargemAntesDoPrazo()
    {
        // Prazo em 09/09: a véspera (08/09) é margem, então nada pode ir além de 07/09.
        var due = Today.AddDays(3);

        var input = new PlanningInput(Today, new[]
        {
            Item("Relatório", minutes: 120, due: due, order: 0),
            Item("Anexos", minutes: 120, due: due, order: 1),
        }, new TestCapacity(120));

        var moves = PlanningEngine.Redistribute(input);

        Assert.All(moves, m => Assert.True(m.To <= due.AddDays(-2), $"{m.To} invadiu a margem"));
    }

    [Fact]
    public void RotinaNaoEhRedistribuida()
    {
        var routine = new PlannableItem(
            "r1", PlannableKind.Routine, "Academia", Today, null, null, false,
            Priority.Normal, true, false, long.MaxValue);

        var input = new PlanningInput(Today, new[]
        {
            routine,
            Item("Relatório", minutes: 400, due: Today.AddDays(10), order: 0),
        }, new TestCapacity(120));

        Assert.DoesNotContain(PlanningEngine.Redistribute(input), m => m.Title == "Academia");
    }

    [Fact]
    public void ConflitoSemSolucaoEhInformadoComOsNumeros()
    {
        var input = new PlanningInput(Today, new[]
        {
            Item("Relatório", minutes: 660, due: Today.AddDays(4), order: 0),
        }, new TestCapacity(105));

        var result = PlanningEngine.Plan(input);

        var conflict = Assert.Single(result.Conflicts);
        Assert.Contains("11h", conflict.Message);
        Assert.Contains("7h", conflict.Message);
    }

    [Fact]
    public void HorizonteEhCurto()
    {
        var input = new PlanningInput(Today, Array.Empty<PlannableItem>(), new TestCapacity());
        var result = PlanningEngine.Plan(input);

        Assert.Equal(PlanningEngine.DefaultHorizonDays + 1, result.Days.Count);
        Assert.Equal(Today, result.Days[0].Date);
        Assert.Equal(Today.AddDays(PlanningEngine.DefaultHorizonDays), result.Days[^1].Date);
    }

    [Fact]
    public void AtrasadoEntraNoPlanoDeHoje()
    {
        var input = new PlanningInput(Today, new[]
        {
            Item("Ficou de ontem", planned: Today.AddDays(-1), minutes: 30),
        }, new TestCapacity());

        var result = PlanningEngine.Plan(input);

        Assert.Single(result.TodayPlan);
        Assert.Equal(30, result.TodayPlannedMinutes);
    }

    // ---------- Serviço, capacidade e persistência ----------

    [Fact]
    public void CapacidadeSemanalEExcecaoPorDataFuncionam()
    {
        using var fixture = new RoutineFixture(Reference);
        var domingo = new DateOnly(2026, 9, 6);

        Assert.Equal(FixedCapacityProvider.DefaultDailyMinutes, fixture.Capacity.MinutesFor(domingo));

        fixture.Capacity.SetWeekly(CapacityService.DayIndex(domingo), 0);
        Assert.Equal(0, fixture.Capacity.MinutesFor(domingo));

        fixture.Capacity.SetOverride(domingo, 120);
        Assert.Equal(120, fixture.Capacity.MinutesFor(domingo));

        fixture.Capacity.RemoveOverride(domingo);
        Assert.Equal(0, fixture.Capacity.MinutesFor(domingo));
    }

    [Fact]
    public void CapacidadeSobreviveAoReinicio()
    {
        using var fixture = new RoutineFixture(Reference);
        fixture.Capacity.SetWeekly(6, 0);
        fixture.Capacity.SetOverride(new DateOnly(2026, 9, 12), 120);

        fixture.Restart();

        Assert.Equal(0, fixture.Capacity.MinutesFor(new DateOnly(2026, 9, 13)));
        Assert.Equal(120, fixture.Capacity.MinutesFor(new DateOnly(2026, 9, 12)));
    }

    [Fact]
    public void PlanoDoDiaMisturaTarefasRotinasESessoes()
    {
        using var fixture = new RoutineFixture(Reference);

        fixture.Tasks.Add("Relatório", fixture.Clock.Today, fixture.Clock.Today.AddDays(1), 90);
        fixture.Routines.Create("Academia", RecurrenceType.Daily);

        var exam = fixture.Exams.Create("MPP-31", "P1", fixture.Clock.Today.AddDays(4))!;
        fixture.Exams.AddSession(exam.Id, "MPP-31 — estudar Lista 1", null, 90, fixture.Clock.Today);


        var plan = fixture.Planning.Plan().TodayPlan;

        Assert.Equal(3, plan.Count);
        Assert.Contains(plan, s => s.Item.Kind == PlannableKind.Routine);
        Assert.Contains(plan, s => s.Item.Kind == PlannableKind.StudySession);
        Assert.Contains(plan, s => s.Item.Kind == PlannableKind.Task);
    }

    [Fact]
    public void OrdemManualDoDiaEhPreservada()
    {
        using var fixture = new RoutineFixture(Reference);

        fixture.Tasks.Add("Urgente", fixture.Clock.Today, fixture.Clock.Today, 30);
        fixture.Tasks.Add("Tranquila", fixture.Clock.Today, fixture.Clock.Today.AddDays(20), 30);

        var suggested = fixture.Planning.TodayPlan();
        Assert.Equal("Urgente", suggested[0].Item.Title);

        // O usuário reordena à mão: o motor não desfaz.
        fixture.Planning.SaveTodayOrder(suggested
            .OrderBy(s => s.Item.Title == "Tranquila" ? 0 : 1)
            .Select(s => (s.Item.Kind, s.Item.Id))
            .ToList());

        Assert.Equal("Tranquila", fixture.Planning.TodayPlan()[0].Item.Title);
    }

    [Fact]
    public void OQueFicouParaTrasEhDetectadoEDepoisTrazido()
    {
        using var fixture = new RoutineFixture(Reference);
        var task = fixture.Tasks.Add("Relatório", fixture.Clock.Today.AddDays(-1), estimatedMinutes: 60)!;

        Assert.Single(fixture.Planning.Unfinished());

        fixture.Planning.CarryForwardOverdue();

        var moved = fixture.Tasks.GetAll().Single();
        Assert.Equal(task.Id, moved.Id);
        Assert.Equal(fixture.Clock.Today, moved.PlannedDate);
        Assert.Empty(fixture.Planning.Unfinished());
    }

    [Fact]
    public void ArrastarMarcaODiaComoDecisaoManual()
    {
        using var fixture = new RoutineFixture(Reference);
        var task = fixture.Tasks.Add("Relatório", plannedDate: null)!;

        Assert.False(fixture.Tasks.GetAll().Single().ManualPlanning);

        fixture.Tasks.SetPlannedDate(task.Id, fixture.Clock.Today);
        Assert.True(fixture.Tasks.GetAll().Single().ManualPlanning);

        // O motor, ao aplicar uma proposta, devolve a decisão para si.
        fixture.Planning.ApplyMoves(new[]
        {
            new ProposedMove(task.Id, PlannableKind.Task, "Relatório",
                fixture.Clock.Today, fixture.Clock.Today.AddDays(1), "teste"),
        });

        var moved = fixture.Tasks.GetAll().Single();
        Assert.Equal(fixture.Clock.Today.AddDays(1), moved.PlannedDate);
        Assert.False(moved.ManualPlanning);
    }

    [Fact]
    public void TerminarTudoNaoGeraTrabalhoExtra()
    {
        using var fixture = new RoutineFixture(Reference);
        var hoje = fixture.Tasks.Add("Relatório", fixture.Clock.Today, estimatedMinutes: 60)!;
        fixture.Tasks.Add("De amanhã", fixture.Clock.Today.AddDays(1), estimatedMinutes: 60);

        fixture.Tasks.SetCompleted(hoje.Id, true);

        var result = fixture.Planning.Plan();

        Assert.Empty(result.TodayPlan);
        Assert.Equal(0, result.TodayPlannedMinutes);

        // A de amanhã continua sendo de amanhã.
        Assert.Equal(fixture.Clock.Today.AddDays(1),
            fixture.Tasks.GetAll().First(t => t.Title == "De amanhã").PlannedDate);
    }

    [Fact]
    public void MigracaoDaV06ParaV07PreservaEProtegeOPlanejamentoManual()
    {
        using var temp = new TempDatabase();
        var clock = new FakeClock(Reference);

        var repository = new TaskRepository(temp.Database);
        var tasks = new TaskListService(repository, clock);

        var planejada = tasks.Add("Relatório", clock.Today, clock.Today.AddDays(2))!;
        var solta = tasks.Add("Comprar café", plannedDate: null)!;

        temp.Reopen();

        var after = new TaskRepository(temp.Database).GetAll().ToDictionary(t => t.Id);

        Assert.Equal("Relatório", after[planejada.Id].Title);
        Assert.Equal(clock.Today, after[planejada.Id].PlannedDate);
        Assert.Equal(Priority.Normal, after[planejada.Id].Priority);

        // Tudo o que já tinha dia foi escolhido à mão antes da V0.7: o motor respeita.
        Assert.True(after[planejada.Id].ManualPlanning);
        Assert.False(after[solta.Id].ManualPlanning);

        var capacity = new CapacityService(new CapacityRepository(temp.Database));
        Assert.Equal(FixedCapacityProvider.DefaultDailyMinutes, capacity.MinutesFor(clock.Today));
        Assert.All(capacity.WeeklyMinutes(), m => Assert.Equal(FixedCapacityProvider.DefaultDailyMinutes, m));
    }
}
