using HojeWidget.Domain;
using HojeWidget.Domain.Planning;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.Database;

namespace HojeWidget.Tests;

/// <summary>
/// V0.5 — projetos, entregas, subtarefas, progresso e distribuição automática.
/// </summary>
public class ContainerTests
{
    private static readonly DateTime Reference = new(2026, 9, 5, 9, 0, 0, DateTimeKind.Local);

    private static RoutineFixture NewFixture() => new(Reference);

    // ---------- Projeto e entrega ----------

    [Fact]
    public void ProjetoEEntregaSaoConceitosSeparados()
    {
        using var fixture = NewFixture();

        fixture.Containers.Create(ContainerKind.Project, "Preparação para mestrado");
        fixture.Containers.Create(ContainerKind.Delivery, "Relatório de laboratório");

        var projects = fixture.Containers.GetByKind(ContainerKind.Project);
        var deliveries = fixture.Containers.GetByKind(ContainerKind.Delivery);

        Assert.Equal("Preparação para mestrado", Assert.Single(projects).Title);
        Assert.Equal("Relatório de laboratório", Assert.Single(deliveries).Title);
        Assert.Equal("Projeto", projects[0].KindLabel);
        Assert.Equal("Entrega", deliveries[0].KindLabel);
    }

    [Fact]
    public void SubtarefaSoTemUmNivel()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(ContainerKind.Project, "Preparação para mestrado")!;
        var subtask = fixture.Containers.AddSubtask(project.Id, "Pesquisar programas")!;

        // O pai é sempre um container: apontar para uma tarefa não é possível.
        Assert.False(fixture.Containers.MoveTaskInto(subtask.Id, "id-que-nao-existe"));

        var stored = fixture.Tasks.GetAll().First(t => t.Id == subtask.Id);
        Assert.Equal(project.Id, stored.ParentId);
    }

    [Fact]
    public void ProgressoContaConcluidasSobreTotal()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(ContainerKind.Project, "Preparação para mestrado")!;

        foreach (var title in new[]
                 {
                     "Pesquisar programas", "Escolher orientadores", "Atualizar currículo",
                     "Ler editais", "Preparar proposta", "Revisar carta", "Enviar inscrição",
                 })
        {
            fixture.Containers.AddSubtask(project.Id, title);
        }

        var subtasks = fixture.Containers.GetSubtasks(project.Id);
        for (var i = 0; i < 3; i++)
        {
            fixture.Tasks.SetCompleted(subtasks[i].Id, true);
        }

        var progress = fixture.Containers.GetBoard(project.Id)!.Progress;

        Assert.Equal(3, progress.Completed);
        Assert.Equal(7, progress.Total);
        Assert.Equal(43, progress.Percent);
        Assert.Equal("3 de 7 concluídas", progress.Text);
        Assert.Equal("████░░░░░░ 43%", progress.Bar());
    }

    [Fact]
    public void UltimaSubtarefaNaoConcluiOProjetoSozinha()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(ContainerKind.Project, "Preparação para mestrado")!;
        var a = fixture.Containers.AddSubtask(project.Id, "Pesquisar programas")!;
        var b = fixture.Containers.AddSubtask(project.Id, "Ler editais")!;

        fixture.Tasks.SetCompleted(a.Id, true);
        Assert.False(fixture.Containers.IsFullyChecked(project.Id));

        fixture.Tasks.SetCompleted(b.Id, true);

        // Todas marcadas: é hora de perguntar — mas o projeto continua aberto.
        Assert.True(fixture.Containers.IsFullyChecked(project.Id));
        Assert.False(fixture.Containers.GetById(project.Id)!.IsCompleted);

        fixture.Containers.SetCompleted(project.Id, true);
        Assert.True(fixture.Containers.GetById(project.Id)!.IsCompleted);
    }

    [Fact]
    public void MoverTarefaExistentePreservaTudo()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(ContainerKind.Project, "Preparação para mestrado")!;

        var task = fixture.Tasks.Add(
            "Atualizar currículo",
            new DateOnly(2026, 9, 8),
            new DateOnly(2026, 9, 12),
            estimatedMinutes: 90,
            canSplit: false)!;

        fixture.Tasks.SetCompleted(task.Id, true);
        var orderBefore = fixture.Tasks.GetAll().First(t => t.Id == task.Id).SortOrder;

        Assert.True(fixture.Containers.MoveTaskInto(task.Id, project.Id));

        var moved = fixture.Tasks.GetAll().First(t => t.Id == task.Id);
        Assert.Equal(project.Id, moved.ParentId);
        Assert.Equal("Atualizar currículo", moved.Title);
        Assert.True(moved.IsCompleted);
        Assert.Equal(new DateOnly(2026, 9, 8), moved.PlannedDate);
        Assert.Equal(new DateOnly(2026, 9, 12), moved.DueDate);
        Assert.Equal(90, moved.EstimatedMinutes);
        Assert.False(moved.CanSplit);
        Assert.Equal(orderBefore, moved.SortOrder);
    }

    [Fact]
    public void ExcluirProjetoDevolveSubtarefasEmVezDeApagar()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(ContainerKind.Project, "Preparação para mestrado")!;
        var subtask = fixture.Containers.AddSubtask(project.Id, "Ler editais", new DateOnly(2026, 9, 7))!;

        fixture.Containers.Delete(project.Id);

        var survivor = fixture.Tasks.GetAll().Single(t => t.Id == subtask.Id);
        Assert.Null(survivor.ParentId);
        Assert.Equal(new DateOnly(2026, 9, 7), survivor.PlannedDate);
    }

    [Fact]
    public void SubtarefaNaoPoluiAlgumDia()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(ContainerKind.Project, "Preparação para mestrado")!;
        fixture.Containers.AddSubtask(project.Id, "Ler editais");
        fixture.Tasks.Add("Comprar café", plannedDate: null);

        var someday = fixture.Tasks.GetSomeday().Pending.Select(t => t.Title).ToList();

        Assert.Equal(new[] { "Comprar café" }, someday);
    }

    [Fact]
    public void SubtarefaPlanejadaApareceEmHojeComOProgressoDoProjeto()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(ContainerKind.Project, "Preparação para mestrado")!;
        var a = fixture.Containers.AddSubtask(project.Id, "Pesquisar programas", fixture.Clock.Today)!;
        fixture.Containers.AddSubtask(project.Id, "Ler editais", fixture.Clock.Today);

        fixture.Tasks.SetCompleted(a.Id, true);

        var index = fixture.Containers.GetProgressIndex();
        var pending = fixture.Tasks.GetSnapshot().Pending.Single();
        var entry = DayEntry.FromTask(pending, index[project.Id].Container, index[project.Id].Progress);

        Assert.Equal("Ler editais", entry.Title);
        Assert.Equal("Preparação para mestrado", entry.ParentTitle);
        Assert.Equal("Preparação para mestrado · 1 de 2", TaskLabels.For(entry, fixture.Clock.Today));
    }

    // ---------- Distribuição automática ----------

    [Fact]
    public void DistribuicaoEspalhaAoLongoDoPeriodoEGuardaAVespera()
    {
        // O exemplo da especificação: prazo 10/09, etapas em 06, 07 e 08, margem em 09.
        var today = new DateOnly(2026, 9, 6);
        var items = new[]
        {
            new DistributableItem("1", "Etapa 1", 60, true, 0),
            new DistributableItem("2", "Etapa 2", 60, true, 1),
            new DistributableItem("3", "Etapa 3", 60, true, 2),
        };

        var plan = Distributor.Distribute(items, today, new DateOnly(2026, 9, 10).AddDays(-1), new TestCapacity(120));

        Assert.True(plan.MarginPreserved);
        Assert.Equal(new DateOnly(2026, 9, 9), plan.MarginDate);
        Assert.Empty(plan.Conflicts);

        var days = plan.Slots.Select(s => s.Date).Distinct().ToList();
        Assert.All(days, d => Assert.InRange(d, today, new DateOnly(2026, 9, 8)));
        Assert.Equal(3, plan.Slots.Count);

        // Uma etapa por dia, na ordem em que foram escritas.
        Assert.Equal(new[] { "Etapa 1", "Etapa 2", "Etapa 3" }, plan.Slots.Select(s => s.Title));
    }

    [Fact]
    public void DistribuicaoEncostaNoPrazoSemAmontoar()
    {
        var today = new DateOnly(2026, 9, 1);
        var items = Enumerable.Range(1, 6)
            .Select(i => new DistributableItem(i.ToString(), $"Etapa {i}", 60, true, i))
            .ToArray();

        var plan = Distributor.Distribute(items, today, new DateOnly(2026, 9, 10), new TestCapacity(180));

        // O trabalho encosta no último dia útil em vez de começar hoje e deixar
        // os dias antes do prazo vazios.
        Assert.Equal(new DateOnly(2026, 9, 9), plan.Slots.Max(s => s.Date));
        Assert.NotEqual(today, plan.Slots.Min(s => s.Date));
        Assert.Equal(plan.Slots.Min(s => s.Date), plan.RecommendedStart);

        // Encostar no prazo não é amontoar: continua uma etapa por dia.
        Assert.Equal(6, plan.Slots.Select(s => s.Date).Distinct().Count());
    }

    [Fact]
    public void DiaComCapacidadeZeroNaoRecebeNada()
    {
        var today = new DateOnly(2026, 9, 6);
        var closed = new DateOnly(2026, 9, 7);

        var items = new[]
        {
            new DistributableItem("1", "Etapa 1", 90, true, 0),
            new DistributableItem("2", "Etapa 2", 90, true, 1),
        };

        var capacity = new TestCapacity(120).Closed(closed);
        var plan = Distributor.Distribute(items, today, new DateOnly(2026, 9, 12), capacity);

        Assert.DoesNotContain(plan.Slots, s => s.Date == closed);
    }

    [Fact]
    public void TarefaDivisivelViraSessoes()
    {
        // 4h totais, 2h por dia: duas sessões, uma hoje e outra amanhã.
        var today = new DateOnly(2026, 9, 6);
        var items = new[] { new DistributableItem("1", "Escrever relatório", 240, true, 0) };

        var plan = Distributor.Distribute(items, today, new DateOnly(2026, 9, 8), new TestCapacity(120));

        Assert.Equal(2, plan.Slots.Count);
        Assert.All(plan.Slots, s => Assert.Equal(120, s.Minutes));
        Assert.Equal(new[] { 1, 2 }, plan.Slots.Select(s => s.SessionIndex));
        Assert.All(plan.Slots, s => Assert.Equal(2, s.SessionCount));
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public void TarefaIndivisivelProcuraUmDiaInteiro()
    {
        var today = new DateOnly(2026, 9, 6);
        var items = new[] { new DistributableItem("1", "Levar o carro à oficina", 200, false, 0) };

        var capacity = new TestCapacity(120).On(new DateOnly(2026, 9, 9), 240);
        var plan = Distributor.Distribute(items, today, new DateOnly(2026, 9, 12), capacity);

        var slot = Assert.Single(plan.Slots);
        Assert.Equal(new DateOnly(2026, 9, 9), slot.Date);
        Assert.Equal(200, slot.Minutes);
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public void TarefaIndivisivelSemDiaInformaConflito()
    {
        var today = new DateOnly(2026, 9, 6);
        var items = new[] { new DistributableItem("1", "Levar o carro à oficina", 400, false, 0) };

        var plan = Distributor.Distribute(items, today, new DateOnly(2026, 9, 9), new TestCapacity(120));

        Assert.Empty(plan.Slots);
        var conflict = Assert.Single(plan.Conflicts);
        Assert.Contains("não pode ser dividida", conflict.Message);
    }

    [Fact]
    public void PrazoApertadoGastaAMargemEAvisa()
    {
        var today = new DateOnly(2026, 9, 6);
        var items = new[]
        {
            new DistributableItem("1", "Etapa 1", 120, true, 0),
            new DistributableItem("2", "Etapa 2", 120, true, 1),
        };

        // Dois dias úteis e trabalho para os dois: não sobra margem.
        var plan = Distributor.Distribute(items, today, new DateOnly(2026, 9, 7), new TestCapacity(120));

        Assert.False(plan.MarginPreserved);
        Assert.Null(plan.MarginDate);
        Assert.Empty(plan.Conflicts);
        Assert.Equal(2, plan.Slots.Select(s => s.Date).Distinct().Count());
    }

    [Fact]
    public void DuracaoDesconhecidaNaoViraNumeroInventado()
    {
        var today = new DateOnly(2026, 9, 6);
        var items = new[]
        {
            new DistributableItem("1", "Etapa sem estimativa", null, true, 0),
            new DistributableItem("2", "Outra sem estimativa", null, true, 1),
        };

        var plan = Distributor.Distribute(items, today, new DateOnly(2026, 9, 10), new TestCapacity(180));

        Assert.Equal(2, plan.Slots.Count);
        Assert.All(plan.Slots, s => Assert.Null(s.Minutes));
        Assert.Equal(0, plan.RequiredMinutes);
    }

    [Fact]
    public void PlanoAplicadoCriaAsSessoesComoSubtarefasIrmas()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(
            ContainerKind.Project, "Preparação para mestrado", new DateOnly(2026, 9, 12))!;

        fixture.Containers.AddSubtask(project.Id, "Escrever proposta", estimatedMinutes: 240);

        var plan = fixture.Containers.BuildDistribution(project.Id, new TestCapacity(120));
        Assert.Equal(2, plan.Slots.Count);

        fixture.Containers.ApplyDistribution(plan.Slots);

        var subtasks = fixture.Containers.GetSubtasks(project.Id).OrderBy(t => t.PlannedDate).ToList();
        Assert.Equal(2, subtasks.Count);
        Assert.Equal("Escrever proposta (1/2)", subtasks[0].Title);
        Assert.Equal("Escrever proposta (2/2)", subtasks[1].Title);
        Assert.All(subtasks, t => Assert.Equal(120, t.EstimatedMinutes));
        Assert.All(subtasks, t => Assert.Equal(project.Id, t.ParentId));
    }

    [Fact]
    public void RedistribuirNaoEmpilhaSufixosDeSessao()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(
            ContainerKind.Project, "Preparação para mestrado", new DateOnly(2026, 9, 12))!;

        var subtask = fixture.Containers.AddSubtask(project.Id, "Escrever proposta", estimatedMinutes: 240)!;

        fixture.Containers.ApplyDistribution(
            fixture.Containers.BuildDistribution(project.Id, new TestCapacity(120)).Slots);

        // Agora sobra folga: a mesma etapa cabe num dia só.
        fixture.Containers.ApplyDistribution(new[]
        {
            new PlannedSlot(subtask.Id, "Escrever proposta (1/2)", new DateOnly(2026, 9, 7), 240, 1, 1),
        });

        var updated = fixture.Tasks.GetAll().First(t => t.Id == subtask.Id);
        Assert.Equal("Escrever proposta", updated.Title);
    }

    [Fact]
    public void SemPrazoNaoHaDistribuicao()
    {
        using var fixture = NewFixture();
        var project = fixture.Containers.Create(ContainerKind.Project, "Preparação para mestrado")!;
        fixture.Containers.AddSubtask(project.Id, "Ler editais", estimatedMinutes: 60);

        var plan = fixture.Containers.BuildDistribution(project.Id, new TestCapacity());

        Assert.True(plan.IsEmpty);
        Assert.False(plan.HasConflicts);
    }

    // ---------- Migração ----------

    [Fact]
    public void MigracaoDaV04ParaV05PreservaTudo()
    {
        using var temp = new TempDatabase();
        var clock = new FakeClock(Reference);

        var tasks = new TaskListService(new TaskRepository(temp.Database), clock);
        var routines = new RoutineService(new RoutineRepository(temp.Database), clock);

        var task = tasks.Add("Fazer lista de Termodinâmica", clock.Today, new DateOnly(2026, 9, 9))!;
        tasks.SetCompleted(task.Id, true);
        routines.Create("Academia", RecurrenceType.Daily);

        temp.Reopen();

        var afterTasks = new TaskListService(new TaskRepository(temp.Database), clock);
        var reopened = afterTasks.GetAll().Single();

        Assert.Equal("Fazer lista de Termodinâmica", reopened.Title);
        Assert.True(reopened.IsCompleted);
        Assert.Equal(clock.Today, reopened.PlannedDate);
        Assert.Equal(new DateOnly(2026, 9, 9), reopened.DueDate);

        // Os campos novos nascem com um padrão que não muda nada do que existia.
        Assert.Null(reopened.ParentId);
        Assert.Null(reopened.EstimatedMinutes);
        Assert.True(reopened.CanSplit);

        Assert.Single(new RoutineService(new RoutineRepository(temp.Database), clock).GetAll());
    }

    [Fact]
    public void EstimativaEntendeOsFormatosUsados()
    {
        Assert.Equal(90, Estimate.TryParse("1h30"));
        Assert.Equal(90, Estimate.TryParse("90"));
        Assert.Equal(90, Estimate.TryParse("90 min"));
        Assert.Equal(90, Estimate.TryParse("1:30"));
        Assert.Equal(60, Estimate.TryParse("1h"));
        Assert.Null(Estimate.TryParse(""));
        Assert.Null(Estimate.TryParse("depois"));

        Assert.Equal("1h30", Estimate.Format(90));
        Assert.Equal("1h20", Estimate.Format(80));
        Assert.Equal("1h", Estimate.Format(60));
        Assert.Equal("45 min", Estimate.Format(45));
        Assert.Null(Estimate.Format((int?)null));
    }
}
