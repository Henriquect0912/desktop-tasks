global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using Xunit;

using HojeWidget.Domain.Planning;

using System.Drawing;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.Database;
using HojeWidget.Services.DisplayLayout;
using HojeWidget.Services.System;

namespace HojeWidget.Tests;

/// <summary>
/// Testes que montam janelas do WPF precisam correr um de cada vez.
/// Cada um cria a sua thread STA e mexe no <c>Application.Current</c> e nos
/// dicionários de recursos, que são globais do processo; em paralelo eles se
/// atrapalham e falham por motivo nenhum.
/// </summary>
[CollectionDefinition(WpfCollection.Name, DisableParallelization = true)]
public sealed class WpfCollection
{
    public const string Name = "WPF";
}

/// <summary>Relogio controlado pelos testes.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTime localNow) => LocalNow = localNow;

    public DateTime LocalNow { get; set; }

    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    public string LocalDate => HojeWidget.Domain.LocalDate.From(LocalNow);

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    /// <summary>Avanca a data local e o instante UTC juntos.</summary>
    public void AdvanceDays(int days)
    {
        LocalNow = LocalNow.AddDays(days);
        UtcNow = UtcNow.AddDays(days);
    }

    public void AdvanceMinutes(int minutes)
    {
        LocalNow = LocalNow.AddMinutes(minutes);
        UtcNow = UtcNow.AddMinutes(minutes);
    }
}

/// <summary>Banco SQLite em arquivo temporario, removido ao final do teste.</summary>
public sealed class TempDatabase : IDisposable
{
    private readonly string _directory;

    /// <param name="maxVersion">
    /// Cria o banco parando numa versão antiga de esquema. Só os testes de
    /// migração usam isto: eles montam o banco como ele era e depois reabrem sem
    /// o teto, deixando a migração de verdade rodar.
    /// </param>
    public TempDatabase(int? maxVersion = null)
    {
        _directory = Path.Combine(Path.GetTempPath(), "HojeWidgetTests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_directory);

        Path_ = Path.Combine(_directory, "app.db");
        Database = new AppDatabase(Path_, maxVersion);
    }

    public string Path_ { get; }

    public AppDatabase Database { get; private set; }

    /// <summary>Fecha e reabre o banco, simulando um reinicio do aplicativo.</summary>
    public void Reopen()
    {
        Database.Dispose();
        Database = new AppDatabase(Path_);
    }

    public void Dispose()
    {
        Database.Dispose();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Arquivo ainda bloqueado pelo sistema: nao afeta o resultado do teste.
        }
    }
}

/// <summary>Monta o conjunto de servicos usado pelos testes de tarefas.</summary>
public sealed class TaskFixture : IDisposable
{
    public TaskFixture(DateTime? localNow = null)
    {
        Temp = new TempDatabase();
        Clock = new FakeClock(localNow ?? new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Local));
        Tasks = new TaskListService(new TaskRepository(Temp.Database), Clock);
        Review = new DailyReviewService(new DailyReviewRepository(Temp.Database), Clock);
    }

    public TempDatabase Temp { get; }

    public FakeClock Clock { get; }

    public TaskListService Tasks { get; private set; }

    public DailyReviewService Review { get; private set; }

    /// <summary>Reabre o banco e recria os servicos, preservando o relogio.</summary>
    public void Restart()
    {
        Temp.Reopen();
        Tasks = new TaskListService(new TaskRepository(Temp.Database), Clock);
        Review = new DailyReviewService(new DailyReviewRepository(Temp.Database), Clock);
    }

    public IReadOnlyList<string> PendingTitles()
        => Tasks.GetSnapshot().Pending.Select(t => t.Title).ToList();

    public IReadOnlyList<string> CompletedTitles()
        => Tasks.GetSnapshot().CompletedToday.Select(t => t.Title).ToList();

    /// <summary>Busca em todas as tarefas, não só nas visíveis em Hoje.</summary>
    public string IdOf(string title)
        => Tasks.GetAll().First(t => t.Title == title).Id;

    public void Dispose() => Temp.Dispose();
}

/// <summary>Servicos de tarefas e rotinas sobre o mesmo banco temporario.</summary>
public sealed class RoutineFixture : IDisposable
{
    public RoutineFixture(DateTime localNow)
    {
        Temp = new TempDatabase();
        Clock = new FakeClock(localNow);
        Build();
    }

    public TempDatabase Temp { get; }

    public FakeClock Clock { get; }

    public TaskListService Tasks { get; private set; } = null!;

    public RoutineService Routines { get; private set; } = null!;

    public DailyReviewService Review { get; private set; } = null!;

    /// <summary>V0.5 — projetos e entregas.</summary>
    public ContainerService Containers { get; private set; } = null!;

    /// <summary>V0.6 / V0.7.1 — provas, árvore de estudo e sessões.</summary>
    public ExamService Exams { get; private set; } = null!;

    /// <summary>V0.7.1 — a árvore de estudo e o grafo de dependências.</summary>
    public StudyGraphService Graph { get; private set; } = null!;

    /// <summary>V0.7.1 — o planejador de estudos de uma prova.</summary>
    public ExamPlanningService ExamPlanning { get; private set; } = null!;

    /// <summary>V0.7 — capacidade configurável e motor de planejamento.</summary>
    public CapacityService Capacity { get; private set; } = null!;

    public PlanningService Planning { get; private set; } = null!;

    public TaskRepository TaskRepository { get; private set; } = null!;

    public SettingsRepository Settings { get; private set; } = null!;

    private RoutineRepository RoutineRepository { get; set; } = null!;

    public int CountOccurrences(string routineId) => RoutineRepository.CountOccurrences(routineId);

    public void Restart()
    {
        Temp.Reopen();
        Build();
    }

    /// <summary>Busca em todas as tarefas, inclusive subtarefas de projetos.</summary>
    public string IdOf(string title) => Tasks.GetAll().First(t => t.Title == title).Id;

    private void Build()
    {
        TaskRepository = new TaskRepository(Temp.Database);
        Tasks = new TaskListService(TaskRepository, Clock);
        RoutineRepository = new RoutineRepository(Temp.Database);
        Routines = new RoutineService(RoutineRepository, Clock);
        Review = new DailyReviewService(new DailyReviewRepository(Temp.Database), Clock);
        Containers = new ContainerService(new ContainerRepository(Temp.Database), TaskRepository, Clock);

        var examRepository = new ExamRepository(Temp.Database);
        Graph = new StudyGraphService(new StudyNodeRepository(Temp.Database), examRepository, Clock);
        Exams = new ExamService(examRepository, Graph, Clock);

        Capacity = new CapacityService(new CapacityRepository(Temp.Database));
        Settings = new SettingsRepository(Temp.Database);
        Planning = new PlanningService(
            TaskRepository, Routines, Exams, Containers, Capacity,
            new DailyPlanRepository(Temp.Database), Settings, Clock);
        ExamPlanning = new ExamPlanningService(Exams, Capacity, Planning, Clock);
    }

    public void Dispose() => Temp.Dispose();
}

/// <summary>
/// Capacidade fixa por dia da semana, para os testes de distribuição.
/// Um dia com zero minutos é um dia que não recebe planejamento automático.
/// </summary>
public sealed class TestCapacity : ICapacityProvider
{
    private readonly int _defaultMinutes;
    private readonly Dictionary<DateOnly, int> _exceptions = new();
    private readonly HashSet<DayOfWeek> _closedDays = new();

    public TestCapacity(int defaultMinutes = 180) => _defaultMinutes = defaultMinutes;

    public TestCapacity Closed(DateOnly date)
    {
        _exceptions[date] = 0;
        return this;
    }

    public TestCapacity On(DateOnly date, int minutes)
    {
        _exceptions[date] = minutes;
        return this;
    }

    public TestCapacity ClosedOn(DayOfWeek day)
    {
        _closedDays.Add(day);
        return this;
    }

    public int MinutesFor(DateOnly date)
    {
        if (_exceptions.TryGetValue(date, out var minutes))
        {
            return minutes;
        }

        return _closedDays.Contains(date.DayOfWeek) ? 0 : _defaultMinutes;
    }
}

/// <summary>Construtores de monitores para os testes de layout.</summary>
public static class Monitors
{
    public static MonitorInfo Laptop(double scale = 1.5) => new(
        @"\\.\DISPLAY1",
        new Rectangle(0, 0, 1920, 1080),
        new Rectangle(0, 0, 1920, 1032),
        scale,
        IsPrimary: true);

    public static MonitorInfo External(double scale = 1.0) => new(
        @"\\.\DISPLAY2",
        new Rectangle(1920, 0, 1920, 1080),
        new Rectangle(1920, 0, 1920, 1032),
        scale,
        IsPrimary: false);

    public static IReadOnlyList<MonitorInfo> Single() => new[] { Laptop() };

    public static IReadOnlyList<MonitorInfo> Dual() => new[] { Laptop(), External() };
}
