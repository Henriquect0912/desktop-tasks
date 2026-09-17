using HojeWidget.Domain.Study;
using HojeWidget.Models;
using HojeWidget.Services;

namespace HojeWidget.Tests;

/// <summary>
/// V0.7.1 — o exemplo inteiro da especificação (§55), montado pelos serviços de
/// verdade e não por grafos de mentira: árvore, dependências automáticas,
/// limites de estudo e o plano que sai disso.
/// </summary>
public class ExamGraphIntegrationTests
{
    private static readonly DateTime Reference = new(2026, 9, 6, 9, 0, 0, DateTimeKind.Local);

    /// <summary>Monta a MPP-31 do enunciado, com os três assuntos e as dependências.</summary>
    private static (RoutineFixture Fixture, Exam Exam) BuildMpp31()
    {
        var fixture = new RoutineFixture(Reference);

        var exam = fixture.Exams.Create(
            "MPP-31", string.Empty, new DateOnly(2026, 9, 10),
            new ExamStudyPolicy(120, 180, 90))!;

        var torcao = fixture.Graph.AddSubject(exam.Id, "Torção")!;
        fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, torcao.Id, 60);
        fixture.Graph.AddExerciseList(exam.Id, "Lista 1", torcao.Id, 60, 10);

        var flexao = fixture.Graph.AddSubject(exam.Id, "Flexão Bi-Axial")!;
        fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, flexao.Id, 60);
        var lista2 = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", flexao.Id, 45, 8)!;

        var paredes = fixture.Graph.AddSubject(exam.Id, "Paredes Finas")!;
        fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, paredes.Id, 120);
        var lista3 = fixture.Graph.AddExerciseList(exam.Id, "Lista 3", paredes.Id, 180, 15)!;

        // A dependência cruzada do enunciado: Lista 3 também depende da Lista 2.
        Assert.True(fixture.Graph.AddDependency(exam.Id, lista3.Id, lista2.Id).Accepted);

        return (fixture, exam);
    }

    [Fact]
    public void ArvoreDoEnunciadoFicaComAOrdemCerta()
    {
        var (fixture, exam) = BuildMpp31();
        using var _ = fixture;

        var graph = fixture.Graph.GetGraph(exam.Id);

        // Tudo entra no planejamento: as teorias primeiro, as listas depois delas.
        Assert.Equal(
            new[] { "Teoria", "Teoria", "Teoria", "Lista 1", "Lista 2", "Lista 3" },
            graph.PlannableNodes().Select(n => n.Title));

        Assert.Equal(
            new[] { "Lista 1", "Lista 2", "Lista 3" },
            graph.WaitingNodes().Select(n => n.Title));

        // A Lista 3 espera duas coisas: a teoria do assunto e a Lista 2.
        var lista3 = graph.Nodes.Single(n => n.Title == "Lista 3");
        Assert.Equal(
            new[] { "Teoria", "Lista 2" },
            graph.PendingPrerequisites(lista3.Id).Select(n => n.Title).OrderByDescending(t => t == "Teoria"));
    }

    [Fact]
    public void OPlanoColocaTudoMasNuncaLista3AntesDaLista2()
    {
        var (fixture, exam) = BuildMpp31();
        using var _ = fixture;

        var graph = fixture.Graph.GetGraph(exam.Id);
        var lista2 = graph.Nodes.Single(n => n.Title == "Lista 2");
        var lista3 = graph.Nodes.Single(n => n.Title == "Lista 3");
        var paredes = graph.ChildrenOf(graph.Nodes.Single(n => n.Title == "Paredes Finas").Id)
            .Single(n => n.NodeType == StudyNodeType.Theory);

        var plan = fixture.ExamPlanning.BuildPlan(exam.Id);

        // A Lista 3 é planejada — não fica de fora por depender de outras.
        Assert.Contains(plan.Sessions, s => s.NodeId == lista3.Id);

        // Mas nunca antes dos dois pré-requisitos dela.
        var inicio = plan.Sessions.Where(s => s.NodeId == lista3.Id).Min(s => s.Date);

        foreach (var prerequisito in new[] { lista2.Id, paredes.Id })
        {
            var fim = plan.Sessions.Where(s => s.NodeId == prerequisito).Max(s => s.Date);
            Assert.True(inicio >= fim, $"Lista 3 em {inicio}, pré-requisito até {fim}");
        }
    }

    [Fact]
    public void PlanoAceitoRespeitaOsLimitesEViraSessoesDoDia()
    {
        var (fixture, exam) = BuildMpp31();
        using var _ = fixture;

        var plan = fixture.ExamPlanning.BuildPlan(exam.Id);
        fixture.ExamPlanning.ApplyPlan(exam.Id, plan.Sessions);

        var sessions = fixture.Exams.GetSessions(exam.Id);
        Assert.NotEmpty(sessions);

        // Nenhuma sessão passa do máximo contínuo, nenhum dia passa do máximo diário.
        Assert.All(sessions, s => Assert.True(s.EstimatedMinutes <= exam.Policy.MaxSessionMinutes));
        Assert.All(
            sessions.GroupBy(s => s.PlannedDate),
            day => Assert.True(day.Sum(s => s.EstimatedMinutes ?? 0) <= exam.Policy.MaxDailyMinutes));

        // E o dia da prova não recebe conteúdo novo.
        Assert.DoesNotContain(sessions, s => s.PlannedDate >= exam.ExamDate);

        // O título carrega a disciplina, como aparece em Hoje.
        Assert.All(sessions, s => Assert.StartsWith("MPP-31 — ", s.Title));
    }

    [Fact]
    public void CapacidadeGeralGrandeNaoViraEstudoSemFim()
    {
        var (fixture, exam) = BuildMpp31();
        using var _ = fixture;

        // 24h de capacidade geral em todos os dias.
        for (var i = 0; i < 7; i++)
        {
            fixture.Capacity.SetWeekly(i, 24 * 60);
        }

        var plan = fixture.ExamPlanning.BuildPlan(exam.Id);

        Assert.All(
            plan.Sessions.GroupBy(s => s.Date),
            day => Assert.True(
                day.Sum(s => s.Minutes) <= exam.Policy.MaxDailyMinutes,
                $"{day.Key} recebeu {day.Sum(s => s.Minutes)} min de MPP-31"));

        Assert.All(plan.Sessions, s => Assert.True(s.Minutes <= exam.Policy.MaxSessionMinutes));
    }

    [Fact]
    public void OutrasCoisasDoDiaReduzemOEspacoDaProva()
    {
        var (fixture, exam) = BuildMpp31();
        using var _ = fixture;

        var today = fixture.Clock.Today;

        // O dia tem 3h; 2h30 já estão tomadas por uma tarefa comum.
        for (var i = 0; i < 7; i++)
        {
            fixture.Capacity.SetWeekly(i, 180);
        }

        fixture.Tasks.Add("Relatório", today, estimatedMinutes: 150);

        var capacities = fixture.ExamPlanning.DayCapacities(exam.Id, today, today.AddDays(2));
        var first = capacities.Single(c => c.Date == today);

        Assert.Equal(180, first.GeneralMinutes);
        Assert.Equal(150, first.AlreadyUsedMinutes);
        Assert.Equal(30, first.GeneralRemaining);

        var plan = fixture.ExamPlanning.BuildPlan(exam.Id);
        Assert.True(plan.Sessions.Where(s => s.Date == today).Sum(s => s.Minutes) <= 30);
    }

    [Fact]
    public void OWidgetRecebeTambemOQueEsperaAVezDeOutro()
    {
        var (fixture, exam) = BuildMpp31();
        using var _ = fixture;

        fixture.ExamPlanning.ApplyPlan(exam.Id, fixture.ExamPlanning.BuildPlan(exam.Id).Sessions);

        var graph = fixture.Graph.GetGraph(exam.Id);
        var waiting = graph.WaitingNodes().Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(waiting);

        // Nada é escondido do dia: o que espera a vez também vira sessão.
        var sessions = fixture.Exams.GetSessionsBetween(fixture.Clock.Today, fixture.Clock.Today.AddDays(30));
        Assert.Contains(sessions, s => s.Session.NodeId is { } id && waiting.Contains(id));
    }

    [Fact]
    public void SessaoNaoConcluidaVemParaHojeSozinha()
    {
        var (fixture, exam) = BuildMpp31();
        using var _ = fixture;

        var session = fixture.Exams.AddSession(
            exam.Id, "MPP-31 — Teoria", null, 60, fixture.Clock.Today.AddDays(-1))!;

        Assert.Equal(1, fixture.Planning.CarryForwardOverdue());

        Assert.Equal(
            fixture.Clock.Today,
            fixture.Exams.GetSessions(exam.Id).Single(s => s.Id == session.Id).PlannedDate);
    }

    [Fact]
    public void MudarDependenciaNaoInvalidaSessaoJaPlanejada()
    {
        var (fixture, exam) = BuildMpp31();
        using var _ = fixture;

        var graph = fixture.Graph.GetGraph(exam.Id);
        var teoriaTorcao = graph.ChildrenOf(graph.Nodes.Single(n => n.Title == "Torção").Id)
            .Single(n => n.NodeType == StudyNodeType.Theory);

        // Uma sessão já planejada para a teoria de Torção.
        var session = fixture.Exams.AddSession(
            exam.Id, "MPP-31 — Teoria", teoriaTorcao.Id, 60, fixture.Clock.Today.AddDays(1))!;

        // Agora ela passa a depender da teoria de Paredes Finas, que está pendente.
        var teoriaParedes = graph.ChildrenOf(graph.Nodes.Single(n => n.Title == "Paredes Finas").Id)
            .Single(n => n.NodeType == StudyNodeType.Theory);

        Assert.True(fixture.Graph.AddDependency(exam.Id, teoriaTorcao.Id, teoriaParedes.Id).Accepted);

        var board = fixture.Exams.GetBoard(exam.Id)!;
        var waiting = board.Graph.WaitingNodes().Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(teoriaTorcao.Id, waiting);

        // A sessão continua válida: a ordem nova vale para o próximo plano, e
        // nada é marcado nem apagado só porque a dependência mudou.
        Assert.Equal(0, fixture.Exams.RevalidateSessions(exam.Id));
        Assert.False(fixture.Exams.GetSessions(exam.Id).Single(s => s.Id == session.Id).NeedsReview);
    }
}
