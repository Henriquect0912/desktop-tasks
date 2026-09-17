using HojeWidget.Domain.Study;
using HojeWidget.Models;

namespace HojeWidget.Tests;

/// <summary>
/// A data-limite para começar a estudar.
///
/// O plano não começa hoje só porque hoje existe: ele ocupa os últimos dias
/// necessários no ritmo confortável, e o primeiro deles é o limite. Ninguém
/// começa hoje a estudar para uma prova daqui a dois meses.
/// </summary>
public class StudyStartDateTests
{
    private static readonly DateOnly Today = new(2026, 9, 7);

    private static StudyNode Node(string id, string title, int? minutes, long order = 0)
        => new()
        {
            Id = id,
            ExamId = "exam",
            NodeType = StudyNodeType.Theory,
            Title = title,
            EstimatedMinutes = minutes,
            CanSplit = true,
            MasteryLevel = Mastery.NotStudied,
            SortOrder = order,
        };

    private static List<DayCapacity> Capacity(int days, int minutes = 480)
        => Enumerable.Range(0, days)
            .Select(i => new DayCapacity(Today.AddDays(i), minutes, 0))
            .ToList();

    [Fact]
    public void ProvaDistanteNaoComecaHoje()
    {
        // 4h de conteúdo, meta de 2h/dia, prova daqui a dois meses.
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 240) },
            Array.Empty<StudyDependency>());

        var exam = Today.AddDays(60);
        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, exam, Capacity(60));

        Assert.NotNull(plan.RecommendedStart);
        Assert.True(plan.RecommendedStart > Today.AddDays(30),
            $"recomendou começar em {plan.RecommendedStart}, cedo demais para uma prova em {exam}");

        // Nada é marcado para hoje.
        Assert.DoesNotContain(plan.Sessions, s => s.Date == Today);
        Assert.Equal(plan.RecommendedStart, plan.Sessions.Min(s => s.Date));
    }

    [Fact]
    public void AConta_DoisDiasDeConteudoNoRitmoConfortavel()
    {
        // 4h no total, 2h por dia confortáveis: dois dias de trabalho.
        // Prova em 17/09 → margem em 16/09 → trabalho em 14/09 e 15/09.
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 240) },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, new DateOnly(2026, 9, 17), Capacity(12));

        Assert.Equal(new DateOnly(2026, 9, 14), plan.RecommendedStart);
        Assert.Equal(new DateOnly(2026, 9, 16), plan.MarginDate);
        Assert.Equal(new[] { new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 15) },
            plan.Sessions.Select(s => s.Date).Distinct().OrderBy(d => d));
    }

    [Fact]
    public void UmDiaDepoisDoLimiteJaApertaria()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 240) },
            Array.Empty<StudyDependency>());

        var policy = new ExamStudyPolicy(120, 180, 90);
        var exam = new DateOnly(2026, 9, 17);

        var plan = StudyPlanner.Plan(graph, policy, Today, exam, Capacity(12));
        var limite = plan.RecommendedStart!.Value;

        // Começando no limite, nenhum dia passa da meta confortável.
        Assert.All(
            plan.Sessions.GroupBy(s => s.Date),
            day => Assert.True(day.Sum(s => s.Minutes) <= policy.ComfortableDailyMinutes));

        // Um dia depois do limite, o mesmo conteúdo já não cabe no ritmo
        // confortável: sobram menos dias do que o trabalho pede.
        var diasRestantes = exam.AddDays(-2).DayNumber - limite.AddDays(1).DayNumber + 1;
        Assert.True(diasRestantes * policy.ComfortableDailyMinutes < 240,
            "começar um dia depois deveria estourar a meta confortável");
    }

    [Fact]
    public void ProvaProximaComecaHoje()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 300) },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(4), Capacity(4));

        Assert.Equal(Today, plan.RecommendedStart);
        Assert.Contains(plan.Sessions, s => s.Date == Today);
    }

    [Fact]
    public void DiaDeDescansoNaoContaNaConta()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 240) },
            Array.Empty<StudyDependency>());

        // O penúltimo dia útil está zerado: a janela precisa recuar mais.
        var capacities = Capacity(12);
        capacities[9] = capacities[9] with { GeneralMinutes = 0 };

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, new DateOnly(2026, 9, 18), capacities);

        Assert.DoesNotContain(plan.Sessions, s => s.Date == Today.AddDays(9));
        Assert.Equal(240, plan.Sessions.Sum(s => s.Minutes));
    }

    [Fact]
    public void OQueJaEstaTomadoNoDiaEncurtaAJanela()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 240) },
            Array.Empty<StudyDependency>());

        // Todos os dias já têm 7h30 tomadas de 8h: sobra meia hora por dia.
        var capacities = Enumerable.Range(0, 20)
            .Select(i => new DayCapacity(Today.AddDays(i), 480, 450))
            .ToList();

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(20), capacities);

        // Meia hora por dia: são precisos oito dias, não dois.
        Assert.Equal(8, plan.Sessions.Select(s => s.Date).Distinct().Count());
        Assert.All(plan.Sessions, s => Assert.True(s.Minutes <= 30));
    }

    [Fact]
    public void ARecomendacaoVemNoPlanoEnaoNasNotas()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 120) },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(30), Capacity(30));

        // A data-limite é o resultado principal, não um recado escondido no fim.
        Assert.NotNull(plan.RecommendedStart);
        Assert.Equal(plan.RecommendedStart, plan.Sessions.Min(s => s.Date));

        // E no ritmo confortável não há nada a avisar.
        Assert.Empty(plan.Notes);
    }

    [Fact]
    public void ADependenciaContinuaSendoRespeitadaDentroDaJanela()
    {
        var graph = new StudyGraph(
            new[]
            {
                Node("teoria", "Teoria", 120, order: 0),
                Node("lista", "Lista", 120, order: 1),
            },
            new[]
            {
                new StudyDependency
                {
                    Id = "e1", ExamId = "exam", NodeId = "lista", DependsOnNodeId = "teoria",
                },
            });

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(40), Capacity(40));

        var fimDaTeoria = plan.Sessions.Where(s => s.NodeId == "teoria").Max(s => s.Date);
        var inicioDaLista = plan.Sessions.Where(s => s.NodeId == "lista").Min(s => s.Date);

        Assert.True(inicioDaLista >= fimDaTeoria);
        Assert.True(plan.RecommendedStart > Today, "a janela deveria ter recuado para perto da prova");
    }
}
