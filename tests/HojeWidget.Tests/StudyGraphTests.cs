using HojeWidget.Domain.Study;
using HojeWidget.Models;

namespace HojeWidget.Tests;

/// <summary>
/// V0.7.1 — o grafo de dependências: ordem, ciclos e a dependência automática
/// teoria → lista.
///
/// Dependência aqui **ordena**, não tranca: um item com pré-requisito pendente
/// continua na lista do que há para fazer, só vem depois do que vem antes.
/// </summary>
public class StudyGraphTests
{
    private static readonly DateTime Reference = new(2026, 9, 6, 9, 0, 0, DateTimeKind.Local);

    private static RoutineFixture NewFixture() => new(Reference);

    // ---------- Ordem ----------

    [Fact]
    public void DependenciaSimplesDizAOrdem_SemTrancar()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var lista2 = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", null, 45, 10, false)!;
        var lista3 = fixture.Graph.AddExerciseList(exam.Id, "Lista 3", null, 180, 20, false)!;

        Assert.True(fixture.Graph.AddDependency(exam.Id, lista3.Id, lista2.Id).Accepted);

        var graph = fixture.Graph.GetGraph(exam.Id);
        Assert.True(graph.IsReady(lista2.Id));
        Assert.False(graph.IsReady(lista3.Id));
        Assert.Equal(new[] { "Lista 2" }, graph.PendingPrerequisites(lista3.Id).Select(n => n.Title));

        // A Lista 3 espera a vez, mas continua na lista do que há para fazer.
        Assert.Equal(new[] { "Lista 2", "Lista 3" }, graph.PlannableNodes().Select(n => n.Title));

        fixture.Graph.SetCompleted(lista2.Id, true);

        graph = fixture.Graph.GetGraph(exam.Id);
        Assert.True(graph.IsReady(lista3.Id));
        Assert.Empty(graph.PendingPrerequisites(lista3.Id));
    }

    [Fact]
    public void DependenciaMultiplaSoLiberaComTodasConcluidas()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var lista2 = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", null, 45, 10, false)!;
        var teoria = fixture.Graph.AddNode(exam.Id, "Paredes Finas — Teoria", StudyNodeType.Theory, null, 120)!;
        var lista3 = fixture.Graph.AddExerciseList(exam.Id, "Lista 3", null, 180, 20, false)!;

        fixture.Graph.AddDependency(exam.Id, lista3.Id, lista2.Id);
        fixture.Graph.AddDependency(exam.Id, lista3.Id, teoria.Id);

        fixture.Graph.SetCompleted(lista2.Id, true);
        Assert.False(fixture.Graph.GetGraph(exam.Id).IsReady(lista3.Id));

        fixture.Graph.SetCompleted(teoria.Id, true);
        Assert.True(fixture.Graph.GetGraph(exam.Id).IsReady(lista3.Id));
    }

    [Fact]
    public void DependenciaTransitivaMantemONetoNoFimDaFila()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var a = fixture.Graph.AddNode(exam.Id, "A", StudyNodeType.Theory, null, 60)!;
        var b = fixture.Graph.AddNode(exam.Id, "B", StudyNodeType.Theory, null, 60)!;
        var c = fixture.Graph.AddNode(exam.Id, "C", StudyNodeType.Theory, null, 60)!;

        fixture.Graph.AddDependency(exam.Id, b.Id, a.Id);
        fixture.Graph.AddDependency(exam.Id, c.Id, b.Id);

        var graph = fixture.Graph.GetGraph(exam.Id);
        Assert.True(graph.IsReady(a.Id));
        Assert.False(graph.IsReady(b.Id));
        Assert.False(graph.IsReady(c.Id));

        // Concluir A libera B, mas C continua esperando B.
        fixture.Graph.SetCompleted(a.Id, true);
        graph = fixture.Graph.GetGraph(exam.Id);
        Assert.True(graph.IsReady(b.Id));
        Assert.False(graph.IsReady(c.Id));

        Assert.Equal(2, graph.UnlockPotential(a.Id));
    }

    [Fact]
    public void DataPlanejadaNaoContaComoConclusao()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var lista2 = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", null, 45, 10, false)!;
        var lista3 = fixture.Graph.AddExerciseList(exam.Id, "Lista 3", null, 180, 20, false)!;
        fixture.Graph.AddDependency(exam.Id, lista3.Id, lista2.Id);

        // Sessão criada e com dia no passado: nada disso conta como conclusão.
        var session = fixture.Exams.AddSession(
            exam.Id, "Estudar Lista 2", lista2.Id, 45, fixture.Clock.Today.AddDays(-2))!;

        Assert.False(fixture.Graph.GetGraph(exam.Id).IsReady(lista3.Id));

        // Nem uma sessão parcialmente executada de um nó com duas sessões.
        fixture.Exams.AddSession(exam.Id, "Estudar Lista 2 (2/2)", lista2.Id, 45, fixture.Clock.Today);
        fixture.Exams.SetSessionCompleted(session.Id, true);

        Assert.False(fixture.Graph.GetGraph(exam.Id).IsReady(lista3.Id));
    }

    [Fact]
    public void NoQueEsperaContinuaNaFila_SoQueDepois()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var lista2 = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", null, 45, 10, false)!;
        var lista3 = fixture.Graph.AddExerciseList(exam.Id, "Lista 3", null, 180, 20, false)!;
        fixture.Graph.AddDependency(exam.Id, lista3.Id, lista2.Id);

        var graph = fixture.Graph.GetGraph(exam.Id);

        // Os dois entram no planejamento; o que muda é a ordem.
        Assert.Equal(new[] { "Lista 2", "Lista 3" }, graph.PlannableNodes().Select(n => n.Title));
        Assert.Equal(new[] { "Lista 3" }, graph.WaitingNodes().Select(n => n.Title));
        Assert.Equal(StudyNodeState.Waiting, graph.StateOf(graph.Find(lista3.Id)!));

        Assert.Equal(0, graph.DependencyDepth(graph.Find(lista2.Id)!));
        Assert.Equal(1, graph.DependencyDepth(graph.Find(lista3.Id)!));
    }

    [Fact]
    public void ProfundidadeDaCadeiaDaAOrdemTopologica()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var a = fixture.Graph.AddNode(exam.Id, "A", StudyNodeType.Theory, null, 60)!;
        var b = fixture.Graph.AddNode(exam.Id, "B", StudyNodeType.Theory, null, 60)!;
        var c = fixture.Graph.AddNode(exam.Id, "C", StudyNodeType.Theory, null, 60)!;

        fixture.Graph.AddDependency(exam.Id, c.Id, b.Id);
        fixture.Graph.AddDependency(exam.Id, b.Id, a.Id);

        Assert.Equal(
            new[] { "A", "B", "C" },
            fixture.Graph.GetGraph(exam.Id).PlannableNodes().Select(n => n.Title));
    }

    // ---------- Integridade do grafo ----------

    [Fact]
    public void DependerDeSiMesmoEhRecusado()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;
        var node = fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, null, 60)!;

        var result = fixture.Graph.AddDependency(exam.Id, node.Id, node.Id);

        Assert.False(result.Accepted);
        Assert.Equal(DependencyRejection.SelfDependency, result.Rejection);
        Assert.Equal("Um item não pode depender de si mesmo.", result.Message);
        Assert.Empty(fixture.Graph.GetGraph(exam.Id).Dependencies);
    }

    [Fact]
    public void DependenciaRepetidaEhRecusada()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;
        var a = fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, null, 60)!;
        var b = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", null, 60, 12, false)!;

        Assert.True(fixture.Graph.AddDependency(exam.Id, b.Id, a.Id).Accepted);

        var second = fixture.Graph.AddDependency(exam.Id, b.Id, a.Id);
        Assert.False(second.Accepted);
        Assert.Equal(DependencyRejection.Duplicate, second.Rejection);
        Assert.Single(fixture.Graph.GetGraph(exam.Id).Dependencies);
    }

    [Fact]
    public void CicloEhImpedido()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var l1 = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", null, 60, 10, false)!;
        var l2 = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", null, 60, 10, false)!;
        var l3 = fixture.Graph.AddExerciseList(exam.Id, "Lista 3", null, 60, 10, false)!;

        Assert.True(fixture.Graph.AddDependency(exam.Id, l2.Id, l1.Id).Accepted);
        Assert.True(fixture.Graph.AddDependency(exam.Id, l3.Id, l2.Id).Accepted);

        // Fechar o ciclo travaria os três para sempre.
        var closing = fixture.Graph.AddDependency(exam.Id, l1.Id, l3.Id);

        Assert.False(closing.Accepted);
        Assert.Equal(DependencyRejection.Cycle, closing.Rejection);
        Assert.Contains("travados para sempre", closing.Message);
        Assert.Equal(2, fixture.Graph.GetGraph(exam.Id).Dependencies.Count);
    }

    [Fact]
    public void RemoverDependenciaDesbloqueia()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;
        var a = fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, null, 60)!;
        var b = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", null, 60, 12, false)!;

        fixture.Graph.AddDependency(exam.Id, b.Id, a.Id);
        Assert.False(fixture.Graph.GetGraph(exam.Id).IsReady(b.Id));

        fixture.Graph.RemoveDependency(b.Id, a.Id);
        Assert.True(fixture.Graph.GetGraph(exam.Id).IsReady(b.Id));
    }

    // ---------- Dependência automática teoria → lista ----------

    [Fact]
    public void ListaNasceDependendoDaTeoriaDoAssunto()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var flexao = fixture.Graph.AddSubject(exam.Id, "Flexão Bi-Axial")!;
        var teoria = fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, flexao.Id, 60)!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", flexao.Id, 45, 10)!;

        var graph = fixture.Graph.GetGraph(exam.Id);

        Assert.Equal(new[] { teoria.Id }, graph.DependenciesOf(lista.Id));
        Assert.False(graph.IsReady(lista.Id));
    }

    [Fact]
    public void DependenciaAutomaticaPodeSerRecusadaNaCriacao()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var flexao = fixture.Graph.AddSubject(exam.Id, "Flexão Bi-Axial")!;
        fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, flexao.Id, 60);
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", flexao.Id, 45, 10, dependOnTheory: false)!;

        var graph = fixture.Graph.GetGraph(exam.Id);

        Assert.Empty(graph.DependenciesOf(lista.Id));
        Assert.True(graph.IsReady(lista.Id));
    }

    [Fact]
    public void DependenciaAutomaticaPodeSerRemovidaDepois()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var torcao = fixture.Graph.AddSubject(exam.Id, "Torção")!;
        var teoria = fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, torcao.Id, 60)!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", torcao.Id, 60, 12)!;

        Assert.Single(fixture.Graph.GetGraph(exam.Id).DependenciesOf(lista.Id));

        fixture.Graph.RemoveDependency(lista.Id, teoria.Id);

        Assert.Empty(fixture.Graph.GetGraph(exam.Id).DependenciesOf(lista.Id));
        Assert.True(fixture.Graph.GetGraph(exam.Id).IsReady(lista.Id));
    }

    [Fact]
    public void SemTeoriaNoAssuntoAListaNasceLivre()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var subject = fixture.Graph.AddSubject(exam.Id, "Torção")!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", subject.Id, 60, 12)!;

        Assert.Empty(fixture.Graph.GetGraph(exam.Id).DependenciesOf(lista.Id));
    }

    [Fact]
    public void ConcluirOPreRequisitoNaoCriaSessaoSozinho()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 20))!;

        var lista2 = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", null, 45, 10, false)!;
        var lista3 = fixture.Graph.AddExerciseList(exam.Id, "Lista 3", null, 60, 20, false)!;
        fixture.Graph.AddDependency(exam.Id, lista3.Id, lista2.Id);

        fixture.Graph.SetCompleted(lista2.Id, true);

        // Ficou elegível, mas nenhuma sessão nasceu sozinha.
        Assert.True(fixture.Graph.GetGraph(exam.Id).IsReady(lista3.Id));
        Assert.Empty(fixture.Exams.GetSessionsForDay(fixture.Clock.Today));
    }
}
