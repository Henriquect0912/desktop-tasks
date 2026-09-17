using HojeWidget.Domain;
using HojeWidget.Services.Database;

namespace HojeWidget.Tests;

/// <summary>V0.3 — prazo, calendário mensal e próximos prazos.</summary>
public class DeadlineTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 9, 0, 0, DateTimeKind.Local);
    private static readonly DateOnly Today = new(2026, 9, 16);

    [Fact]
    public void PrazoEDataPlanejadaSaoIndependentes()
    {
        using var fixture = new TaskFixture(Now);

        fixture.Tasks.Add("Fazer relatório", new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 18));

        var item = fixture.Tasks.GetAll().Single();
        Assert.Equal(new DateOnly(2026, 9, 16), item.PlannedDate);
        Assert.Equal(new DateOnly(2026, 9, 18), item.DueDate);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void TodasAsCombinacoesDeDatasSaoValidas(bool hasPlanned, bool hasDue)
    {
        using var fixture = new TaskFixture(Now);

        var created = fixture.Tasks.Add(
            "Tarefa",
            hasPlanned ? Today : null,
            hasDue ? Today.AddDays(3) : null);

        Assert.NotNull(created);
        Assert.Equal(hasPlanned, created!.PlannedDate is not null);
        Assert.Equal(hasDue, created.DueDate is not null);
    }

    [Fact]
    public void PrazoPodeSerCriadoAlteradoERemovido()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.AddForToday("Relatório");
        var id = fixture.IdOf("Relatório");

        Assert.True(fixture.Tasks.SetDueDate(id, Today.AddDays(5)));
        Assert.Equal(Today.AddDays(5), fixture.Tasks.GetAll().Single().DueDate);

        fixture.Tasks.SetDueDate(id, Today.AddDays(2));
        Assert.Equal(Today.AddDays(2), fixture.Tasks.GetAll().Single().DueDate);

        fixture.Tasks.SetDueDate(id, null);
        fixture.Restart();
        Assert.Null(fixture.Tasks.GetAll().Single().DueDate);
    }

    // ---------- Rótulos ----------

    [Theory]
    [InlineData(0, "prazo hoje")]
    [InlineData(1, "prazo amanhã")]
    [InlineData(2, "prazo em 2 dias")]
    [InlineData(12, "prazo em 12 dias")]
    [InlineData(-1, "vencido ontem")]
    [InlineData(-2, "vencido há 2 dias")]
    public void RotuloDePrazo(int offset, string expected)
        => Assert.Equal(expected, TaskLabels.Deadline(Today.AddDays(offset), Today));

    [Fact]
    public void SemPrazoNaoHaRotulo()
        => Assert.Null(TaskLabels.Deadline(null, Today));

    [Fact]
    public void AtrasoEPrazoAparecemJuntos()
        => Assert.Equal(
            "planejada para ontem · prazo amanhã",
            TaskLabels.Subtitle(Today.AddDays(-1), Today.AddDays(1), Today));

    [Fact]
    public void SoPrazoAparecerQuandoNaoHaAtraso()
        => Assert.Equal("prazo em 2 dias", TaskLabels.Subtitle(Today, Today.AddDays(2), Today));

    [Fact]
    public void SemNadaARelatarOSubtituloEVazio()
        => Assert.Null(TaskLabels.Subtitle(Today, null, Today));

    // ---------- Próximos prazos ----------

    [Fact]
    public void ProximosPrazosSaoOrdenadosPorProximidade()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.Add("Trabalho de Materiais", Today, Today.AddDays(12));
        fixture.Tasks.Add("Relatório de laboratório", Today, Today.AddDays(2));
        fixture.Tasks.Add("Lista de Termodinâmica", Today, Today.AddDays(5));

        var upcoming = fixture.Tasks.GetUpcomingDeadlines();

        Assert.Equal(
            new[] { "Relatório de laboratório", "Lista de Termodinâmica", "Trabalho de Materiais" },
            upcoming.Select(d => d.Task.Title));
        Assert.Equal(new[] { 2, 5, 12 }, upcoming.Select(d => d.DaysRemaining));
    }

    [Fact]
    public void ProximosPrazosExcluemConcluidasESemPrazo()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.Add("Com prazo", Today, Today.AddDays(3));
        fixture.Tasks.Add("Sem prazo", Today, null);
        fixture.Tasks.Add("Concluída", Today, Today.AddDays(1));
        fixture.Tasks.SetCompleted(fixture.IdOf("Concluída"), true);

        var upcoming = fixture.Tasks.GetUpcomingDeadlines();

        Assert.Equal(new[] { "Com prazo" }, upcoming.Select(d => d.Task.Title));
    }

    [Fact]
    public void PrazoVencidoApareceNoTopoEEIdentificado()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.Add("Atrasada", Today, Today.AddDays(-3));
        fixture.Tasks.Add("Futura", Today, Today.AddDays(1));

        var upcoming = fixture.Tasks.GetUpcomingDeadlines();

        Assert.Equal("Atrasada", upcoming[0].Task.Title);
        Assert.True(upcoming[0].IsOverdue);
        Assert.False(upcoming[1].IsOverdue);
    }

    [Theory]
    [InlineData(-1, "vencido")]
    [InlineData(0, "hoje")]
    [InlineData(1, "amanhã")]
    [InlineData(5, "5 dias")]
    public void ColunaDeProximidade(int offset, string expected)
        => Assert.Equal(expected, TaskLabels.DeadlineProximity(Today.AddDays(offset), Today));

    // ---------- Calendário ----------

    [Fact]
    public void CalendarioColocaPlanejadosEPrazosNoDiaCerto()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.Add("Relatório", new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 18));

        var month = fixture.Tasks.GetMonth(2026, 9);

        var dia16 = month.Single(d => d.Date == new DateOnly(2026, 9, 16));
        var dia18 = month.Single(d => d.Date == new DateOnly(2026, 9, 18));

        Assert.Equal(new[] { "Relatório" }, dia16.Planned.Select(t => t.Title));
        Assert.Empty(dia16.Due);
        Assert.Equal(new[] { "Relatório" }, dia18.Due.Select(t => t.Title));
        Assert.Empty(dia18.Planned);
    }

    [Fact]
    public void CalendarioComecaNaSegundaECobreOMesInteiro()
    {
        using var fixture = new TaskFixture(Now);

        var month = fixture.Tasks.GetMonth(2026, 9);

        Assert.Equal(DayOfWeek.Monday, month[0].Date.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, month[^1].Date.DayOfWeek);
        Assert.Equal(0, month.Count % 7);
        Assert.Equal(30, month.Count(d => d.IsCurrentMonth));
        Assert.Contains(month, d => !d.IsCurrentMonth);
    }

    [Fact]
    public void CalendarioNaoMostraPrazoDeTarefaConcluida()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.Add("Feita", Today, new DateOnly(2026, 9, 20));
        fixture.Tasks.SetCompleted(fixture.IdOf("Feita"), true);

        var month = fixture.Tasks.GetMonth(2026, 9);

        Assert.Empty(month.Single(d => d.Date == new DateOnly(2026, 9, 20)).Due);
    }

    // ---------- Widget ----------

    [Fact]
    public void WidgetMostraPrazoNoSubtitulo()
    {
        using var fixture = new TaskFixture(Now);
        fixture.Tasks.Add("Fazer relatório", Today, Today.AddDays(2));

        var item = fixture.Tasks.GetSnapshot().Pending.Single();

        Assert.Equal("prazo em 2 dias", TaskLabels.Subtitle(item.PlannedDate, item.DueDate, Today));
    }

    // ---------- Migração V0.2 → V0.3 ----------

    [Fact]
    public void MigracaoParaV03PreservaTarefasEDataPlanejada()
    {
        using var temp = new TempDatabaseFile();

        // Banco no formato da V0.2: já tem planned_date, ainda não tem due_date.
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
                   $"Data Source={temp.FilePath};Pooling=False"))
        {
            connection.Open();
            using var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE tasks (
                    id TEXT PRIMARY KEY, title TEXT NOT NULL, is_completed INTEGER NOT NULL DEFAULT 0,
                    completed_local_date TEXT NULL, completed_at_utc TEXT NULL, sort_order INTEGER NOT NULL,
                    created_at_utc TEXT NOT NULL, updated_at_utc TEXT NOT NULL, planned_date TEXT NULL
                );
                CREATE TABLE settings (key TEXT PRIMARY KEY, value_json TEXT NOT NULL);
                CREATE TABLE display_layouts (
                    display_signature TEXT PRIMARY KEY, monitor_device TEXT NOT NULL,
                    x_dip REAL NOT NULL, y_dip REAL NOT NULL, width_dip REAL NOT NULL, height_dip REAL NOT NULL,
                    updated_at_utc TEXT NOT NULL);
                CREATE TABLE daily_review (local_date TEXT PRIMARY KEY, dismissed_at_utc TEXT NOT NULL);
                CREATE TABLE schema_info (version INTEGER NOT NULL);
                INSERT INTO schema_info (version) VALUES (2);
                INSERT INTO tasks VALUES ('t1', 'Academia', 0, NULL, NULL, 0,
                    '2026-09-01T08:00:00.000Z', '2026-09-01T08:00:00.000Z', '2026-09-16');
                """;
            create.ExecuteNonQuery();
        }

        using var database = new AppDatabase(temp.FilePath);
        var item = new TaskRepository(database).GetAll().Single();

        Assert.Equal("Academia", item.Title);
        Assert.Equal(new DateOnly(2026, 9, 16), item.PlannedDate);
        Assert.Null(item.DueDate);
    }
}
