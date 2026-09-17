using HojeWidget.Domain;
using HojeWidget.Services.Database;
using Microsoft.Data.Sqlite;

namespace HojeWidget.Tests;

/// <summary>V0.2 — data planejada, seções Hoje / Esta semana / Algum dia e migração da V0.1.</summary>
public class PlanningTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Local);

    [Fact]
    public void TarefaCriadaPeloWidgetNasceParaHoje()
    {
        using var fixture = new TaskFixture(Now);

        var created = fixture.Tasks.AddForToday("Academia");

        Assert.Equal(fixture.Clock.Today, created!.PlannedDate);
        Assert.Equal(new[] { "Academia" }, fixture.PendingTitles());
    }

    [Fact]
    public void TarefaFuturaNaoApareceEmHojeMasApareceNaSemana()
    {
        using var fixture = new TaskFixture(Now);
        var today = fixture.Clock.Today;

        fixture.Tasks.Add("Prova", today.AddDays(2));

        Assert.Empty(fixture.Tasks.GetSnapshot().Pending);

        var week = fixture.Tasks.GetWeek(LocalDate.StartOfWeek(today));
        Assert.Contains(week.SelectMany(d => d.All), t => t.Title == "Prova");
    }

    [Fact]
    public void TarefaAtrasadaPermaneceVisivelComRotulo()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.AddForToday("Fazer relatório");

        fixture.Clock.AdvanceDays(1);

        var snapshot = fixture.Tasks.GetSnapshot();
        var item = Assert.Single(snapshot.Pending);
        Assert.Equal("Fazer relatório", item.Title);
        Assert.Equal("planejada para ontem", TaskLabels.PlanningDelay(item.PlannedDate, snapshot.Today));
    }

    [Fact]
    public void TarefaSemDataPlanejadaVaiParaAlgumDia()
    {
        using var fixture = new TaskFixture(Now);

        fixture.Tasks.Add("Aprender violão", null);

        Assert.Empty(fixture.Tasks.GetSnapshot().Pending);
        Assert.Equal(new[] { "Aprender violão" }, fixture.Tasks.GetSomeday().Pending.Select(t => t.Title));
    }

    [Fact]
    public void ArrastarEntreDiasAlteraEPersisteADataPlanejada()
    {
        using var fixture = new TaskFixture(Now);
        var today = fixture.Clock.Today;
        fixture.Tasks.AddForToday("Academia");
        var id = fixture.IdOf("Academia");

        var destino = today.AddDays(2);
        Assert.True(fixture.Tasks.SetPlannedDate(id, destino));

        fixture.Restart();

        var moved = fixture.Tasks.GetAll().Single();
        Assert.Equal(destino, moved.PlannedDate);
        Assert.Empty(fixture.Tasks.GetSnapshot().Pending);
    }

    [Fact]
    public void ArrastarParaHojeFazATarefaVoltarAoWidget()
    {
        using var fixture = new TaskFixture(Now);
        var today = fixture.Clock.Today;
        fixture.Tasks.Add("Relatório", today.AddDays(3));
        var id = fixture.IdOf("Relatório");

        Assert.Empty(fixture.Tasks.GetSnapshot().Pending);

        fixture.Tasks.SetPlannedDate(id, today);

        Assert.Equal(new[] { "Relatório" }, fixture.PendingTitles());
    }

    [Fact]
    public void MoverParaAlgumDiaTiraATarefaDoPlanejamento()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.AddForToday("Academia");

        fixture.Tasks.SetPlannedDate(fixture.IdOf("Academia"), null);

        Assert.Empty(fixture.Tasks.GetSnapshot().Pending);
        Assert.Single(fixture.Tasks.GetSomeday().Pending);
    }

    [Fact]
    public void ConclusaoSegueAMesmaRegraDaVersaoAnterior()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.AddForToday("Academia");
        var id = fixture.IdOf("Academia");

        fixture.Tasks.SetCompleted(id, true);
        var snapshot = fixture.Tasks.GetSnapshot();
        Assert.Empty(snapshot.Pending);
        Assert.Single(snapshot.CompletedToday);

        fixture.Clock.AdvanceDays(1);
        Assert.Empty(fixture.Tasks.GetSnapshot().Display);
    }

    [Fact]
    public void ConcluidaDeHojeApareceMesmoPlanejadaParaOutroDia()
    {
        using var fixture = new TaskFixture(Now);
        var today = fixture.Clock.Today;
        fixture.Tasks.Add("Adiantado", today.AddDays(4));

        fixture.Tasks.SetCompleted(fixture.IdOf("Adiantado"), true);

        Assert.Equal(new[] { "Adiantado" }, fixture.CompletedTitles());
    }

    // ---------- Migração V0.1 → V0.2 ----------

    [Fact]
    public void MigracaoDaV01PreservaTarefasEAsMantemVisiveis()
    {
        using var temp = new TempDatabaseFile();

        // Banco no formato exato da V0.1, sem a coluna planned_date.
        CreateVersion1Database(temp.FilePath, tasks: new[]
        {
            ("t1", "Fazer lista de Termodinâmica", 0, (string?)null),
            ("t2", "Academia", 1, "2026-03-08"),
        });

        using var database = new AppDatabase(temp.FilePath);
        var repository = new TaskRepository(database);
        var all = repository.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Equal(new[] { "Fazer lista de Termodinâmica", "Academia" }, all.Select(t => t.Title));

        // Nenhuma tarefa migrada cai em "Algum dia": todas ganham data planejada.
        Assert.All(all, t => Assert.NotNull(t.PlannedDate));

        // A concluída herda a data da conclusão; a pendente recebe a data da migração.
        Assert.Equal(new DateOnly(2026, 3, 8), all.Single(t => t.Id == "t2").PlannedDate);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), all.Single(t => t.Id == "t1").PlannedDate);
    }

    [Fact]
    public void MigracaoPreservaConfiguracoesELayouts()
    {
        using var temp = new TempDatabaseFile();
        CreateVersion1Database(temp.FilePath, tasks: Array.Empty<(string, string, int, string?)>());

        using (var connection = Open(temp.FilePath))
        {
            using var seed = connection.CreateCommand();
            seed.CommandText = """
                INSERT INTO settings (key, value_json) VALUES ('theme', '1');
                INSERT INTO display_layouts (display_signature, monitor_device, x_dip, y_dip, width_dip, height_dip, updated_at_utc)
                VALUES ('abc', '\\.\DISPLAY1', 10, 20, 410, 300, '2026-03-01T10:00:00.000Z');
                INSERT INTO daily_review (local_date, dismissed_at_utc) VALUES ('2026-03-01', '2026-03-01T10:00:00.000Z');
                """;
            seed.ExecuteNonQuery();
        }

        using var database = new AppDatabase(temp.FilePath);

        Assert.Equal(1, new SettingsRepository(database).GetInt("theme", 0));
        Assert.NotNull(new DisplayLayoutRepository(database).Get("abc"));
        Assert.True(new DailyReviewRepository(database).IsDismissed("2026-03-01"));
    }

    [Fact]
    public void MigracaoRodaUmaVezSoEEIdempotente()
    {
        using var temp = new TempDatabaseFile();
        CreateVersion1Database(temp.FilePath, tasks: new[] { ("t1", "Academia", 0, (string?)null) });

        for (var i = 0; i < 3; i++)
        {
            using var database = new AppDatabase(temp.FilePath);
            Assert.Single(new TaskRepository(database).GetAll());
        }
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());

        connection.Open();
        return connection;
    }

    /// <summary>Reproduz o esquema da V0.1, sem planned_date, com schema_info = 1.</summary>
    private static void CreateVersion1Database(string path, IReadOnlyList<(string Id, string Title, int Order, string? CompletedDate)> tasks)
    {
        using var connection = Open(path);

        using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                CREATE TABLE tasks (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    is_completed INTEGER NOT NULL DEFAULT 0,
                    completed_local_date TEXT NULL,
                    completed_at_utc TEXT NULL,
                    sort_order INTEGER NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );
                CREATE TABLE settings (key TEXT PRIMARY KEY, value_json TEXT NOT NULL);
                CREATE TABLE display_layouts (
                    display_signature TEXT PRIMARY KEY,
                    monitor_device TEXT NOT NULL,
                    x_dip REAL NOT NULL, y_dip REAL NOT NULL,
                    width_dip REAL NOT NULL, height_dip REAL NOT NULL,
                    updated_at_utc TEXT NOT NULL
                );
                CREATE TABLE daily_review (local_date TEXT PRIMARY KEY, dismissed_at_utc TEXT NOT NULL);
                CREATE TABLE schema_info (version INTEGER NOT NULL);
                INSERT INTO schema_info (version) VALUES (1);
                """;
            create.ExecuteNonQuery();
        }

        foreach (var (id, title, order, completedDate) in tasks)
        {
            using var insert = connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO tasks (id, title, is_completed, completed_local_date, completed_at_utc, sort_order, created_at_utc, updated_at_utc)
                VALUES ($id, $title, $done, $date, $at, $order, '2026-03-01T08:00:00.000Z', '2026-03-01T08:00:00.000Z');
                """;
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$title", title);
            insert.Parameters.AddWithValue("$done", completedDate is null ? 0 : 1);
            insert.Parameters.AddWithValue("$date", (object?)completedDate ?? DBNull.Value);
            insert.Parameters.AddWithValue("$at", completedDate is null ? DBNull.Value : "2026-03-08T18:00:00.000Z");
            insert.Parameters.AddWithValue("$order", order);
            insert.ExecuteNonQuery();
        }
    }
}

/// <summary>Caminho de banco temporário sem abrir o AppDatabase.</summary>
public sealed class TempDatabaseFile : IDisposable
{
    private readonly string _directory;

    public TempDatabaseFile()
    {
        _directory = Path.Combine(Path.GetTempPath(), "HojeWidgetTests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_directory);
        FilePath = Path.Combine(_directory, "app.db");
    }

    public string FilePath { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
