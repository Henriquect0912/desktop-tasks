using HojeWidget.Domain;
using HojeWidget.Domain.Study;
using HojeWidget.Models;

namespace HojeWidget.Tests;

/// <summary>
/// V0.7.1 — o planejador de estudos: restrições duras contra preferências suaves.
///
/// Os dois casos que a especificação exige estão aqui: a Lista 3 que nunca vem
/// antes da Lista 2, e as 24h de capacidade geral que não viram 24h da mesma
/// prova.
///
/// Desde o pedido do usuário, a dependência **ordena** em vez de trancar: a
/// Lista 3 é planejada normalmente, só nunca antes da Lista 2.
/// </summary>
public class StudyPlannerTests
{
    private static readonly DateOnly Today = new(2026, 9, 6);

    private static StudyNode Node(
        string id,
        string title,
        int? minutes,
        bool canSplit = true,
        bool completed = false,
        Mastery mastery = Mastery.NotStudied,
        StudyNodeType type = StudyNodeType.Theory,
        long order = 0)
        => new()
        {
            Id = id,
            ExamId = "exam",
            NodeType = type,
            Title = title,
            EstimatedMinutes = minutes,
            CanSplit = canSplit,
            IsCompleted = completed,
            MasteryLevel = mastery,
            SortOrder = order,
        };

    private static StudyDependency Edge(string node, string dependsOn)
        => new() { Id = node + dependsOn, ExamId = "exam", NodeId = node, DependsOnNodeId = dependsOn };

    private static List<DayCapacity> Capacity(int days, int generalMinutes, int used = 0)
        => Enumerable.Range(0, days)
            .Select(i => new DayCapacity(Today.AddDays(i), generalMinutes, used))
            .ToList();

    // ---------- O caso obrigatório da especificação ----------

    [Fact]
    public void Lista3EhPlanejadaMasNuncaAntesDaLista2()
    {
        var graph = new StudyGraph(
            new[]
            {
                Node("lista2", "Lista 2", 45, order: 0),
                Node("lista3", "Lista 3", 180, order: 1, type: StudyNodeType.ExerciseList),
                Node("flexao", "Flexão Bi-Axial — teoria", 60, order: 2),
            },
            new[] { Edge("lista3", "lista2") });

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(8), Capacity(8, 480));

        // As duas entram no plano: depender de algo não tira ninguém da lista.
        Assert.Contains(plan.Sessions, s => s.NodeId == "lista2");
        Assert.Contains(plan.Sessions, s => s.NodeId == "lista3");

        // E a Lista 3 nunca começa antes de a Lista 2 terminar.
        var fimDaLista2 = plan.Sessions.Where(s => s.NodeId == "lista2").Max(s => s.Date);
        var inicioDaLista3 = plan.Sessions.Where(s => s.NodeId == "lista3").Min(s => s.Date);
        Assert.True(inicioDaLista3 >= fimDaLista2, $"Lista 3 em {inicioDaLista3}, Lista 2 até {fimDaLista2}");

        // A ordem aparece escrita, sem falar em bloqueio.
        Assert.Contains(plan.Waiting, w => w.NodeId == "lista3");
        Assert.Equal("Vem depois de Lista 2.", plan.Waiting.Single(w => w.NodeId == "lista3").Explanation);
    }

    [Fact]
    public void ComPoucoTempoAVezEhDeQuemVemAntes()
    {
        // Uma hora livre só: ela vai para o que vem antes na ordem.
        var graph = new StudyGraph(
            new[]
            {
                Node("lista2", "Lista 2", 60, order: 0),
                Node("lista3", "Lista 3", 180, order: 1, type: StudyNodeType.ExerciseList),
            },
            new[] { Edge("lista3", "lista2") });

        var capacities = new List<DayCapacity>
        {
            new(Today, 60, 0),
            new(Today.AddDays(1), 0, 0),
        };

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(60, 60, 60), Today, Today.AddDays(2), capacities);

        var session = Assert.Single(plan.Sessions);
        Assert.Equal("lista2", session.NodeId);
    }

    [Fact]
    public void QuemEsperaTambemEhPlanejado()
    {
        var graph = new StudyGraph(
            new[]
            {
                Node("a", "Livre", 60, order: 0),
                Node("b", "Pré-requisito", 60, order: 1),
                Node("c", "Depois de B", 120, order: 2),
            },
            new[] { Edge("c", "b") });

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(6), Capacity(6, 480));

        // O trabalho todo entra no plano.
        Assert.Equal(240, plan.Sessions.Sum(s => s.Minutes));

        var fimDeB = plan.Sessions.Where(s => s.NodeId == "b").Max(s => s.Date);
        var inicioDeC = plan.Sessions.Where(s => s.NodeId == "c").Min(s => s.Date);
        Assert.True(inicioDeC >= fimDeB);
    }

    // ---------- Capacidade geral contra capacidade da matéria ----------

    [Fact]
    public void VinteEQuatroHorasDeCapacidadeNaoViramVinteEQuatroHorasDaProva()
    {
        // O teste obrigatório da §27: capacidade geral de 24h, prova com
        // meta 2h, máximo 3h/dia e sessão de no máximo 1h30.
        var graph = new StudyGraph(
            new[] { Node("tudo", "Conteúdo enorme", 1200, order: 0) },
            Array.Empty<StudyDependency>());

        var policy = new ExamStudyPolicy(120, 180, 90);
        var plan = StudyPlanner.Plan(graph, policy, Today, Today.AddDays(5), Capacity(5, 24 * 60));

        foreach (var day in plan.Sessions.GroupBy(s => s.Date))
        {
            Assert.True(day.Sum(s => s.Minutes) <= policy.MaxDailyMinutes,
                $"{day.Key} recebeu {day.Sum(s => s.Minutes)} min desta prova");
        }

        Assert.All(plan.Sessions, s => Assert.True(s.Minutes <= policy.MaxSessionMinutes));

        // E o dia não é preenchido à força só porque sobra capacidade geral.
        Assert.True(plan.Sessions.Sum(s => s.Minutes) < 24 * 60);

        // O que não coube dentro dos limites aparece escrito, não some.
        Assert.NotEmpty(plan.Conflicts);
    }

    [Fact]
    public void MaximoDiarioNuncaEhUltrapassado()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Bloco", 900, order: 0) },
            Array.Empty<StudyDependency>());

        var policy = new ExamStudyPolicy(120, 180, 90);
        var plan = StudyPlanner.Plan(graph, policy, Today, Today.AddDays(6), Capacity(6, 600));

        Assert.All(
            plan.Sessions.GroupBy(s => s.Date),
            day => Assert.True(day.Sum(s => s.Minutes) <= 180));
    }

    [Fact]
    public void MaximoPorSessaoNuncaEhUltrapassado()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Lista 3", 180, order: 0) },
            Array.Empty<StudyDependency>());

        // Máximo diário de 4h caberia numa tacada, mas a sessão para em 1h30.
        var policy = new ExamStudyPolicy(120, 240, 90);
        var plan = StudyPlanner.Plan(graph, policy, Today, Today.AddDays(4), Capacity(4, 480));

        Assert.All(plan.Sessions, s => Assert.True(s.Minutes <= 90));
        Assert.Equal(180, plan.Sessions.Sum(s => s.Minutes));
        Assert.Equal(2, plan.Sessions.Count);
    }

    [Fact]
    public void MetaConfortavelEhSeguidaComProvaDistante()
    {
        var graph = new StudyGraph(
            new[]
            {
                Node("a", "A", 120, order: 0),
                Node("b", "B", 120, order: 1),
                Node("c", "C", 120, order: 2),
            },
            Array.Empty<StudyDependency>());

        var policy = new ExamStudyPolicy(120, 180, 90);
        var plan = StudyPlanner.Plan(graph, policy, Today, Today.AddDays(10), Capacity(10, 480));

        Assert.All(
            plan.Sessions.GroupBy(s => s.Date),
            day => Assert.True(day.Sum(s => s.Minutes) <= policy.ComfortableDailyMinutes,
                $"{day.Key} passou da meta confortável com prova distante"));
    }

    [Fact]
    public void ProvaProximaPassaDaMetaMasNaoDoMaximo()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 480, order: 0) },
            Array.Empty<StudyDependency>());

        var policy = new ExamStudyPolicy(120, 180, 90);

        // Três dias úteis e 8h de conteúdo: não cabe no ritmo confortável.
        var plan = StudyPlanner.Plan(graph, policy, Today, Today.AddDays(4), Capacity(4, 480));

        var heaviest = plan.Sessions.GroupBy(s => s.Date).Max(g => g.Sum(s => s.Minutes));

        Assert.True(heaviest > policy.ComfortableDailyMinutes, "a meta deveria ter subido");
        Assert.True(heaviest <= policy.MaxDailyMinutes, "o máximo diário foi ultrapassado");
        Assert.Contains(plan.Notes, n => n.Contains("meta confortável"));
    }

    [Fact]
    public void CapacidadeGeralJaTomadaLimitaAProva()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 300, order: 0) },
            Array.Empty<StudyDependency>());

        // O dia tem 3h no total, mas 2h30 já estão ocupadas por outras coisas.
        var capacities = new List<DayCapacity>
        {
            new(Today, 180, 150),
            new(Today.AddDays(1), 180, 150),
            new(Today.AddDays(2), 180, 150),
        };

        var plan = StudyPlanner.Plan(graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(3), capacities);

        Assert.All(
            plan.Sessions.GroupBy(s => s.Date),
            day => Assert.True(day.Sum(s => s.Minutes) <= 30, $"{day.Key} passou do que sobrava do dia"));
    }

    [Fact]
    public void DiaDeDescansoNaoRecebeSessao()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 120, order: 0) },
            Array.Empty<StudyDependency>());

        var capacities = new List<DayCapacity>
        {
            new(Today, 0, 0),
            new(Today.AddDays(1), 180, 0),
            new(Today.AddDays(2), 180, 0),
        };

        var plan = StudyPlanner.Plan(graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(3), capacities);

        Assert.DoesNotContain(plan.Sessions, s => s.Date == Today);
        Assert.NotEmpty(plan.Sessions);
    }

    // ---------- Divisibilidade ----------

    [Fact]
    public void ItemDivisivelViraSessoesDentroDoMaximo()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Lista 3", 180, canSplit: true, order: 0) },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(90, 90, 90), Today, Today.AddDays(4), Capacity(4, 480));

        Assert.Equal(2, plan.Sessions.Count);
        Assert.All(plan.Sessions, s => Assert.Equal(90, s.Minutes));
        Assert.Equal(new[] { 1, 2 }, plan.Sessions.Select(s => s.SessionIndex));
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public void ItemNaoDivisivelNaoEhQuebrado()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Oficina", 90, canSplit: false, order: 0) },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(60, 180, 90), Today, Today.AddDays(4), Capacity(4, 480));

        var session = Assert.Single(plan.Sessions);
        Assert.Equal(90, session.Minutes);
        Assert.Equal(1, session.SessionCount);
    }

    [Fact]
    public void ItemNaoDivisivelMaiorQueASessaoInformaConflito()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Prova simulada", 180, canSplit: false, order: 0) },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(90, 180, 90), Today, Today.AddDays(5), Capacity(5, 480));

        Assert.Empty(plan.Sessions);
        var conflict = Assert.Single(plan.Conflicts);
        Assert.Contains("não pode ser dividido", conflict.Message);
        Assert.Contains("1h30", conflict.Message);
    }

    // ---------- Margem e plano impossível ----------

    [Fact]
    public void VesperaFicaParaRevisaoQuandoDa()
    {
        // Prova em 10/09: conteúdo até 08/09 e 09/09 de margem.
        var graph = new StudyGraph(
            new[]
            {
                Node("a", "Lista 1", 60, order: 0),
                Node("b", "Lista 2", 60, order: 1),
                Node("c", "Lista 3", 60, order: 2),
            },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, new DateOnly(2026, 9, 10), Capacity(4, 180));

        Assert.Equal(new DateOnly(2026, 9, 9), plan.MarginDate);
        Assert.All(plan.Sessions, s => Assert.True(s.Date <= new DateOnly(2026, 9, 8), $"{s.Date} invadiu a margem"));
        Assert.DoesNotContain(plan.Sessions, s => s.Date == new DateOnly(2026, 9, 10));
    }

    [Fact]
    public void VesperaRecebeConteudoSoQuandoPrecisa()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 420, order: 0) },
            Array.Empty<StudyDependency>());

        // Dois dias úteis e 7h de conteúdo com máximo de 3h/dia: não cabe, e a
        // véspera precisa receber conteúdo novo.
        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(3), Capacity(3, 480));

        Assert.Null(plan.MarginDate);
        Assert.Contains(plan.Notes, n => n.Contains("véspera"));
        Assert.Equal(420, plan.Sessions.Sum(s => s.Minutes));
        Assert.Equal(3, plan.Sessions.Select(s => s.Date).Distinct().Count());
    }

    [Fact]
    public void PlanoImpossivelMostraODeficit()
    {
        // §28: prova em 2 dias, 10h de trabalho, máximo diário de 3h.
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 600, order: 0) },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(2), Capacity(2, 480));

        Assert.True(plan.IsImpossible);
        Assert.Equal(600, plan.RequiredMinutes);
        Assert.Equal(360, plan.PossibleMinutes);
        Assert.Equal(240, plan.DeficitMinutes);
        Assert.Equal("4h", Estimate.Format(plan.DeficitMinutes));
    }

    [Fact]
    public void TrabalhoQueEsperaContaNoDeficit()
    {
        var graph = new StudyGraph(
            new[]
            {
                Node("b", "Pré-requisito", 60, order: 0),
                Node("c", "Depois de B", 600, order: 1),
            },
            new[] { Edge("c", "b") });

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(2), Capacity(2, 480));

        Assert.Equal(660, plan.RequiredMinutes);
        Assert.True(plan.IsImpossible);
    }

    // ---------- Preferências suaves ----------

    [Fact]
    public void ConteudoNaoEstudadoVemAntesDoDominado()
    {
        var graph = new StudyGraph(
            new[]
            {
                Node("bom", "Já dominado", 60, mastery: Mastery.Fair, order: 0),
                Node("cru", "Nunca visto", 60, mastery: Mastery.NotStudied, order: 1),
            },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(60, 60, 60), Today, Today.AddDays(5), Capacity(5, 480));

        Assert.Equal("cru", plan.Sessions.OrderBy(s => s.Date).First().NodeId);
    }

    [Fact]
    public void EntreIguaisPrefereOQueDestravaOutros()
    {
        var graph = new StudyGraph(
            new[]
            {
                Node("solto", "Item solto", 60, order: 0),
                Node("chave", "Abre caminho", 60, order: 1),
                Node("depois", "Depende da chave", 60, order: 2),
            },
            new[] { Edge("depois", "chave") });

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(60, 60, 60), Today, Today.AddDays(6), Capacity(6, 480));

        Assert.Equal("chave", plan.Sessions.OrderBy(s => s.Date).First().NodeId);
    }

    [Fact]
    public void CadaSessaoExplicaPorQueFoiEscolhida()
    {
        var graph = new StudyGraph(
            new[]
            {
                Node("teoria", "Flexão Bi-Axial — teoria", 60, order: 0),
                Node("lista", "Lista 2", 45, order: 1, type: StudyNodeType.ExerciseList),
            },
            new[] { Edge("lista", "teoria") });

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(120, 180, 90), Today, Today.AddDays(5), Capacity(5, 480));

        var teoria = plan.Sessions.First(s => s.NodeId == "teoria");
        Assert.Contains("prepara Lista 2", teoria.Explanation);
        Assert.Contains("ainda não estudada", teoria.Explanation);

        var lista = plan.Sessions.First(s => s.NodeId == "lista");
        Assert.Contains("colocada depois de Flexão Bi-Axial — teoria", lista.Explanation);
    }

    [Fact]
    public void SessaoLimitadaExplicaOMaximoContinuo()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Lista 3", 180, order: 0) },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(180, 180, 90), Today, Today.AddDays(5), Capacity(5, 480));

        Assert.Contains(plan.Sessions, s => s.Explanation.Contains("máximo contínuo"));
    }

    [Fact]
    public void PlanoEhDeterministico()
    {
        var nodes = new[]
        {
            Node("a", "A", 60, order: 0),
            Node("b", "B", 60, order: 1),
            Node("c", "C", 60, order: 2),
        };

        var policy = new ExamStudyPolicy(60, 120, 60);

        var first = StudyPlanner.Plan(
            new StudyGraph(nodes, Array.Empty<StudyDependency>()), policy, Today, Today.AddDays(5), Capacity(5, 480));

        var second = StudyPlanner.Plan(
            new StudyGraph(nodes.Reverse().ToArray(), Array.Empty<StudyDependency>()),
            policy, Today, Today.AddDays(5), Capacity(5, 480));

        Assert.Equal(
            first.Sessions.Select(s => (s.NodeId, s.Date, s.Minutes)),
            second.Sessions.Select(s => (s.NodeId, s.Date, s.Minutes)));
    }

    [Fact]
    public void SemDiaUtilAntesDaProvaOPlanoDizIsso()
    {
        var graph = new StudyGraph(
            new[] { Node("a", "Conteúdo", 60, order: 0) },
            Array.Empty<StudyDependency>());

        var plan = StudyPlanner.Plan(
            graph, new ExamStudyPolicy(60, 120, 60), Today, Today,
            new List<DayCapacity> { new(Today, 0, 0) });

        Assert.Empty(plan.Sessions);
        Assert.Contains(plan.Notes, n => n.Contains("prova é hoje") || n.Contains("capacidade livre"));
    }
}
