using HojeWidget.Domain;
using HojeWidget.Domain.Study;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.Database;

namespace HojeWidget.Tests;

/// <summary>
/// V0.6 / V0.7.1 — provas, árvore de estudo, domínio, listas e sessões.
/// O grafo de dependências e o planejador têm testes próprios em
/// <see cref="StudyGraphTests"/> e <see cref="StudyPlannerTests"/>.
/// </summary>
public class ExamTests
{
    private static readonly DateTime Reference = new(2026, 9, 6, 9, 0, 0, DateTimeKind.Local);

    private static RoutineFixture NewFixture() => new(Reference);

    [Fact]
    public void ProvaEhCriadaComOsTresLimites()
    {
        using var fixture = NewFixture();

        var exam = fixture.Exams.Create(
            "MPP-31", "P1", new DateOnly(2026, 9, 10),
            new ExamStudyPolicy(120, 180, 90))!;

        Assert.Equal("MPP-31 — P1", exam.DisplayName);
        Assert.Equal(120, exam.Policy.ComfortableDailyMinutes);
        Assert.Equal(180, exam.Policy.MaxDailyMinutes);
        Assert.Equal(90, exam.Policy.MaxSessionMinutes);

        var reloaded = fixture.Exams.GetById(exam.Id)!;
        Assert.Equal(exam.Policy, reloaded.Policy);
    }

    [Fact]
    public void LimitesIncoerentesSaoRecusadosComMensagem()
    {
        Assert.Null(new ExamStudyPolicy(120, 180, 90).Validate());

        Assert.Equal(
            "A meta confortável não pode ser maior que o máximo diário.",
            new ExamStudyPolicy(240, 180, 90).Validate());

        Assert.Equal(
            "O máximo por sessão não pode ser maior que o máximo diário.",
            new ExamStudyPolicy(60, 90, 120).Validate());

        Assert.Equal(
            "Os três limites de estudo precisam ser maiores que zero.",
            new ExamStudyPolicy(0, 180, 90).Validate());

        using var fixture = NewFixture();
        Assert.Null(fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 10), new ExamStudyPolicy(240, 180, 90)));
    }

    [Fact]
    public void ArvoreGuardaAssuntosEFilhos()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 10))!;

        var torcao = fixture.Graph.AddSubject(exam.Id, "Torção")!;
        var teoria = fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, torcao.Id, 60)!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", torcao.Id, 60, 12, dependOnTheory: false)!;

        var graph = fixture.Graph.GetGraph(exam.Id);

        Assert.Equal(new[] { "Torção" }, graph.ChildrenOf(null).Select(n => n.Title));
        Assert.Equal(new[] { "Teoria", "Lista 1" }, graph.ChildrenOf(torcao.Id).Select(n => n.Title));

        Assert.True(torcao.IsSubject);
        Assert.False(torcao.IsPlannable);
        Assert.True(teoria.IsPlannable);
        Assert.Equal(12, lista.QuestionCount);
    }

    [Fact]
    public void AssuntoNaoTemDominioNemDuracao()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 10))!;

        var subject = fixture.Graph.AddNode(exam.Id, "Torção", StudyNodeType.Topic, null, 120)!;

        Assert.Null(subject.EstimatedMinutes);
        Assert.Null(subject.MasteryLevel);
        Assert.False(fixture.Graph.SetMastery(subject.Id, Mastery.Good));
    }

    [Fact]
    public void ConteudoNasceNaoEstudadoEMudaDeNivel()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 10))!;
        var topic = fixture.Graph.AddNode(exam.Id, "Teoria X", StudyNodeType.Theory)!;

        Assert.Equal(Mastery.NotStudied, topic.MasteryLevel);

        foreach (var level in new[] { Mastery.Weak, Mastery.Fair, Mastery.Good })
        {
            Assert.True(fixture.Graph.SetMastery(topic.Id, level));
            Assert.Equal(level, fixture.Graph.GetNode(topic.Id)!.MasteryLevel);
        }
    }

    [Fact]
    public void ProgressoSeparaConteudosDeListas()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 10))!;

        var teoria = fixture.Graph.AddNode(exam.Id, "Teoria X", StudyNodeType.Theory)!;
        fixture.Graph.AddNode(exam.Id, "Capítulo Y", StudyNodeType.Theory);
        var l1 = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", null, 90, 12, false)!;
        var l2 = fixture.Graph.AddExerciseList(exam.Id, "Lista 2", null, 60, 10, false)!;
        fixture.Graph.AddExerciseList(exam.Id, "Lista 3", null, 60, null, false);

        fixture.Graph.SetMastery(teoria.Id, Mastery.Fair);
        fixture.Graph.SetCompleted(l1.Id, true);
        fixture.Graph.SetCompleted(l2.Id, true);

        var board = fixture.Exams.GetBoard(exam.Id)!;

        Assert.Equal(new Progress(1, 2), board.TopicProgress);
        Assert.Equal(new Progress(2, 3), board.ListProgress);
    }

    [Fact]
    public void SessaoNoDiaMostraDuracaoEProximidadeDaProva()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 10))!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", null, 90, 12, false)!;

        fixture.Exams.AddSession(exam.Id, "MPP-31 — estudar Lista 1", lista.Id, 90, fixture.Clock.Today);

        var view = Assert.Single(fixture.Exams.GetSessionsForDay(fixture.Clock.Today));
        var entry = DayEntry.FromSession(view);

        Assert.Equal(EntryKind.StudySession, entry.Kind);
        Assert.Equal("MPP-31 — P1 · 1h30 · prova em 4 dias", TaskLabels.For(entry, fixture.Clock.Today));
    }

    [Fact]
    public void ConcluirTodasAsSessoesConcluiONo()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 12))!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 3", null, 180, 20, false)!;

        var a = fixture.Exams.AddSession(exam.Id, "Lista 3 (1/2)", lista.Id, 90, fixture.Clock.Today)!;
        var b = fixture.Exams.AddSession(exam.Id, "Lista 3 (2/2)", lista.Id, 90, fixture.Clock.Today.AddDays(1))!;

        // Concluir a primeira de duas não conclui o nó: ele fica parcial.
        fixture.Exams.SetSessionCompleted(a.Id, true);
        Assert.False(fixture.Graph.GetNode(lista.Id)!.IsCompleted);
        Assert.True(fixture.Exams.HasCompletedSessions(exam.Id, lista.Id));

        fixture.Exams.SetSessionCompleted(b.Id, true);
        Assert.True(fixture.Graph.GetNode(lista.Id)!.IsCompleted);

        // Reabrir uma sessão devolve o nó ao estado pendente.
        fixture.Exams.SetSessionCompleted(b.Id, false);
        Assert.False(fixture.Graph.GetNode(lista.Id)!.IsCompleted);
    }

    [Fact]
    public void EstadoParcialApareceNaArvore()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 12))!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 3", null, 180, 20, false)!;

        var a = fixture.Exams.AddSession(exam.Id, "Lista 3 (1/2)", lista.Id, 90, fixture.Clock.Today)!;
        fixture.Exams.AddSession(exam.Id, "Lista 3 (2/2)", lista.Id, 90, fixture.Clock.Today.AddDays(1));
        fixture.Exams.SetSessionCompleted(a.Id, true);

        var board = fixture.Exams.GetBoard(exam.Id)!;
        var node = board.Graph.Find(lista.Id)!;

        Assert.Equal(
            StudyNodeState.Partial,
            board.Graph.StateOf(node, fixture.Exams.HasCompletedSessions(exam.Id, lista.Id)));
    }

    [Fact]
    public void RemoverNoLevaSoAsSessoesPendentes()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 12))!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", null, 90, 12, false)!;

        var pendente = fixture.Exams.AddSession(exam.Id, "Estudar Lista 1", lista.Id, 90, fixture.Clock.Today)!;
        var concluida = fixture.Exams.AddSession(exam.Id, "Revisar Lista 1", lista.Id, 30, fixture.Clock.Today)!;
        fixture.Exams.SetSessionCompleted(concluida.Id, true);

        Assert.Equal(1, fixture.Exams.CountDependentSessions(lista.Id));

        fixture.Exams.DeleteNode(lista.Id);

        var remaining = fixture.Exams.GetSessionViews(exam.Id);
        Assert.Single(remaining);
        Assert.Equal(concluida.Id, remaining[0].Session.Id);
        Assert.DoesNotContain(remaining, v => v.Session.Id == pendente.Id);
    }

    [Fact]
    public void ExcluirProvaLevaArvoreESessoes()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 12))!;
        var subject = fixture.Graph.AddSubject(exam.Id, "Torção")!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", subject.Id, 60, 12, false)!;
        fixture.Exams.AddSession(exam.Id, "Estudar Lista 1", lista.Id, 60, fixture.Clock.Today);

        fixture.Exams.Delete(exam.Id);

        Assert.Empty(fixture.Exams.GetAll());
        Assert.Empty(fixture.Exams.GetAllSessions());
        Assert.Empty(fixture.Graph.GetGraph(exam.Id).Nodes);
    }

    [Fact]
    public void MoverNoNaArvoreNaoMexeNoGrafo()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 12))!;

        var torcao = fixture.Graph.AddSubject(exam.Id, "Torção")!;
        var flexao = fixture.Graph.AddSubject(exam.Id, "Flexão")!;
        var teoria = fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, torcao.Id, 60)!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", torcao.Id, 60, 12)!;

        var before = fixture.Graph.GetGraph(exam.Id).DependenciesOf(lista.Id).ToList();
        Assert.Equal(new[] { teoria.Id }, before);

        Assert.True(fixture.Graph.MoveNode(lista.Id, flexao.Id));

        var graph = fixture.Graph.GetGraph(exam.Id);
        Assert.Equal(flexao.Id, graph.Find(lista.Id)!.ParentNodeId);
        Assert.Equal(before, graph.DependenciesOf(lista.Id));
    }

    [Fact]
    public void CalendarioDistingueProvaDeTarefa()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", new DateOnly(2026, 9, 10))!;
        fixture.Exams.AddSession(exam.Id, "Estudar Lista 1", null, 90, new DateOnly(2026, 9, 8));
        fixture.Tasks.Add("Entregar formulário", new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 9));

        var sessions = fixture.Exams.GetSessionsBetween(new DateOnly(2026, 8, 24), new DateOnly(2026, 10, 5));
        var month = fixture.Tasks.GetMonth(2026, 9, null, sessions, fixture.Exams.GetAll());

        var examDay = month.Single(d => d.Date == new DateOnly(2026, 9, 10));
        var sessionDay = month.Single(d => d.Date == new DateOnly(2026, 9, 8));
        var dueDay = month.Single(d => d.Date == new DateOnly(2026, 9, 9));

        Assert.Single(examDay.Exams);
        Assert.Empty(examDay.Planned);

        Assert.Empty(sessionDay.Exams);
        Assert.Single(sessionDay.Planned);
        Assert.Equal(EntryKind.StudySession, sessionDay.Planned[0].Kind);

        Assert.Single(dueDay.Due);
    }

    [Fact]
    public void ApertarLimiteMarcaSessoesParaRevisao_SemApagar()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create(
            "MPP-31", "P1", new DateOnly(2026, 9, 20), new ExamStudyPolicy(120, 240, 120))!;

        fixture.Exams.AddSession(exam.Id, "Bloco longo", null, 120, fixture.Clock.Today.AddDays(1));
        fixture.Exams.AddSession(exam.Id, "Outro bloco", null, 120, fixture.Clock.Today.AddDays(1));

        // Máximo diário cai de 4h para 2h: o plano existente passa do novo limite.
        fixture.Exams.Update(exam.Id, "MPP-31", "P1", new DateOnly(2026, 9, 20), new ExamStudyPolicy(60, 120, 60));

        var beyond = fixture.Exams.SessionsBeyondPolicy(exam.Id);
        Assert.Equal(2, beyond.Count);

        fixture.Exams.FlagForReview(beyond);

        var sessions = fixture.Exams.GetSessions(exam.Id);
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s => Assert.True(s.NeedsReview));
    }

    [Fact]
    public void MigracaoDaV07ParaV071TrazConteudosParaAArvore()
    {
        // Um banco de verdade parado na V0.7, para a migração real rodar em cima.
        using var temp = new TempDatabase(maxVersion: 7);
        var clock = new FakeClock(Reference);

        temp.Database.Execute(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO exams (id, subject, title, exam_date, notes, created_at_utc, updated_at_utc)
                VALUES ('exam1', 'MPP-31', 'P1', '2026-09-12', NULL,
                        '2026-09-06T09:00:00.000Z', '2026-09-06T09:00:00.000Z');

                INSERT INTO exam_topics (id, exam_id, kind, title, mastery, question_count, estimated_minutes,
                                         is_completed, is_active, sort_order)
                VALUES ('t1', 'exam1', 0, 'Teoria X', 2, NULL, 60, 0, 1, 0),
                       ('t2', 'exam1', 1, 'Lista 1', 1, 12, 90, 1, 1, 1);

                INSERT INTO study_sessions (id, exam_id, topic_id, title, estimated_minutes, planned_date, can_split,
                                            is_completed, completed_at_utc, sort_order, created_at_utc, updated_at_utc,
                                            priority, manual_planning)
                VALUES ('s1', 'exam1', 't2', 'MPP-31 — estudar Lista 1', 90, '2026-09-08', 1, 0, NULL, 0,
                        '2026-09-06T09:00:00.000Z', '2026-09-06T09:00:00.000Z', 1, 0);
                """;
            command.ExecuteNonQuery();
        });

        // Reabrir sem teto roda a migração V0.7 → V0.7.1 de verdade.
        temp.Reopen();

        var repository = new ExamRepository(temp.Database);
        var graph = new StudyGraphService(new StudyNodeRepository(temp.Database), repository, clock);
        var exams = new ExamService(repository, graph, clock);

        var board = exams.GetBoard("exam1")!;

        // Prova, títulos, datas, estimativas, domínio, listas e conclusões preservados.
        Assert.Equal("MPP-31 — P1", board.Exam.DisplayName);
        Assert.Equal(new DateOnly(2026, 9, 12), board.Exam.ExamDate);
        Assert.Equal(ExamStudyPolicy.Default, board.Exam.Policy);

        var nodes = board.Graph.Nodes.OrderBy(n => n.SortOrder).ToList();
        Assert.Equal(2, nodes.Count);

        Assert.Equal("Teoria X", nodes[0].Title);
        Assert.Equal(StudyNodeType.Theory, nodes[0].NodeType);
        Assert.Equal(Mastery.Fair, nodes[0].MasteryLevel);
        Assert.Equal(60, nodes[0].EstimatedMinutes);

        Assert.Equal("Lista 1", nodes[1].Title);
        Assert.Equal(StudyNodeType.ExerciseList, nodes[1].NodeType);
        Assert.Equal(12, nodes[1].QuestionCount);
        Assert.True(nodes[1].IsCompleted);

        // Itens ambíguos ficam na raiz e nenhuma dependência é inventada.
        Assert.All(nodes, n => Assert.Null(n.ParentNodeId));
        Assert.Empty(board.Graph.Dependencies);

        // A sessão continua ligada ao mesmo conteúdo, agora pelo nó.
        var session = Assert.Single(board.Sessions);
        Assert.Equal("t2", session.NodeId);
        Assert.Equal(new DateOnly(2026, 9, 8), session.PlannedDate);
    }

    [Fact]
    public void RevalidarMarcaSessoesQueNaoCabemMaisNasRegras()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create(
            "MPP-31", "P1", new DateOnly(2026, 9, 20), new ExamStudyPolicy(60, 120, 60))!;

        var teoria = fixture.Graph.AddNode(exam.Id, "Teoria", StudyNodeType.Theory, null, 60)!;
        var lista = fixture.Graph.AddExerciseList(exam.Id, "Lista 1", null, 60, 12, false)!;

        // Uma sessão maior que o máximo por sessão, e outra ligada a um item que
        // passa a vir depois de outro.
        var grande = fixture.Exams.AddSession(exam.Id, "Bloco de 3h", null, 180, fixture.Clock.Today.AddDays(1))!;
        var daLista = fixture.Exams.AddSession(exam.Id, "Lista 1", lista.Id, 60, fixture.Clock.Today.AddDays(2))!;

        fixture.Graph.AddDependency(exam.Id, lista.Id, teoria.Id);

        // Só o limite invalida uma sessão. Ordem não invalida: ela vale para o
        // próximo plano, e a sessão que já existe continua boa.
        Assert.Equal(1, fixture.Exams.RevalidateSessions(exam.Id));

        var sessions = fixture.Exams.GetSessions(exam.Id).ToDictionary(s => s.Id);
        Assert.True(sessions[grande.Id].NeedsReview);
        Assert.False(sessions[daLista.Id].NeedsReview);

        // Nada foi apagado: as duas continuam lá.
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public void SessaoJaConcluidaNaoEhMarcadaParaRevisao()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create(
            "MPP-31", "P1", new DateOnly(2026, 9, 20), new ExamStudyPolicy(60, 120, 60))!;

        var feita = fixture.Exams.AddSession(exam.Id, "Bloco antigo de 3h", null, 180, fixture.Clock.Today.AddDays(1))!;
        fixture.Exams.SetSessionCompleted(feita.Id, true);

        Assert.Equal(0, fixture.Exams.RevalidateSessions(exam.Id));
        Assert.False(fixture.Exams.GetSessions(exam.Id).Single().NeedsReview);
    }

    [Fact]
    public void SessaoFuturaIncompativelDepoisDaMigracaoEhMarcada()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create(
            "MPP-31", "P1", new DateOnly(2026, 9, 20), new ExamStudyPolicy(60, 120, 60))!;

        // Uma sessão herdada de antes, maior que o máximo por sessão atual.
        fixture.Exams.AddSession(exam.Id, "Bloco herdado de 3h", null, 180, fixture.Clock.Today.AddDays(2));

        var beyond = fixture.Exams.SessionsBeyondPolicy(exam.Id);
        var session = Assert.Single(beyond);
        Assert.Equal(180, session.EstimatedMinutes);

        fixture.Exams.FlagForReview(beyond);
        Assert.True(fixture.Exams.GetSessions(exam.Id).Single().NeedsReview);
    }
}
