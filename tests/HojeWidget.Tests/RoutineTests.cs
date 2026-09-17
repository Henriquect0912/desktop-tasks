using HojeWidget.Domain;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.Database;

namespace HojeWidget.Tests;

/// <summary>V0.4 — rotinas, ocorrências e recorrência.</summary>
public class RoutineTests
{
    private static readonly DateTime Now = new(2026, 9, 7, 9, 0, 0, DateTimeKind.Local); // segunda-feira
    private static readonly DateOnly Today = new(2026, 9, 7);

    private static Routine Rule(
        RecurrenceType type,
        DateOnly? start = null,
        int daysOfWeek = 0,
        int? interval = null,
        int? dayOfMonth = null,
        bool active = true) => new()
        {
            Id = "r1",
            Title = "Rotina",
            RecurrenceType = type,
            StartDate = start ?? Today,
            DaysOfWeek = daysOfWeek,
            IntervalDays = interval,
            DayOfMonth = dayOfMonth,
            IsActive = active,
        };

    // ---------- Cálculo da recorrência ----------

    [Fact]
    public void DiariaAconteceTodosOsDias()
    {
        var dates = RecurrenceCalculator.Occurrences(Rule(RecurrenceType.Daily), Today, Today.AddDays(4));

        Assert.Equal(5, dates.Count);
        Assert.Equal(Today, dates[0]);
        Assert.Equal(Today.AddDays(4), dates[^1]);
    }

    [Fact]
    public void DiasEspecificosSoAcontecemNosDiasEscolhidos()
    {
        var mask = RecurrenceCalculator.MaskOf(DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Saturday);

        var dates = RecurrenceCalculator.Occurrences(
            Rule(RecurrenceType.DaysOfWeek, daysOfWeek: mask), Today, Today.AddDays(6));

        Assert.Equal(
            new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Saturday },
            dates.Select(d => d.DayOfWeek));
    }

    [Fact]
    public void DiasEspecificosSemNenhumDiaNaoGeraNada()
        => Assert.Empty(RecurrenceCalculator.Occurrences(
            Rule(RecurrenceType.DaysOfWeek, daysOfWeek: 0), Today, Today.AddDays(30)));

    [Fact]
    public void ACadaXDiasContaAPartirDoInicio()
    {
        var dates = RecurrenceCalculator.Occurrences(
            Rule(RecurrenceType.EveryXDays, interval: 15), Today, Today.AddDays(45));

        Assert.Equal(
            new[] { Today, Today.AddDays(15), Today.AddDays(30), Today.AddDays(45) },
            dates);
    }

    [Fact]
    public void ACadaXDiasSeAlinhaQuandoAJanelaComecaDepoisDoInicio()
    {
        var start = Today.AddDays(-10);

        var dates = RecurrenceCalculator.Occurrences(
            Rule(RecurrenceType.EveryXDays, start: start, interval: 7), Today, Today.AddDays(10));

        // Ocorrências em start, +7, +14 → a primeira dentro da janela é start+14.
        Assert.Equal(new[] { start.AddDays(14) }, dates);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void IntervaloInvalidoNaoGeraNada(int interval)
        => Assert.Empty(RecurrenceCalculator.Occurrences(
            Rule(RecurrenceType.EveryXDays, interval: interval), Today, Today.AddDays(30)));

    [Fact]
    public void MensalAconteceUmaVezPorMes()
    {
        var dates = RecurrenceCalculator.Occurrences(
            Rule(RecurrenceType.Monthly, start: new DateOnly(2026, 1, 1), dayOfMonth: 10),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 4, 30));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 10),
                new DateOnly(2026, 3, 10), new DateOnly(2026, 4, 10),
            },
            dates);
    }

    [Fact]
    public void MensalDia31UsaOUltimoDiaQuandoOMesNaoTem31()
    {
        var dates = RecurrenceCalculator.Occurrences(
            Rule(RecurrenceType.Monthly, start: new DateOnly(2026, 1, 1), dayOfMonth: 31),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 4, 30));

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 31),
                new DateOnly(2026, 2, 28), // fevereiro de ano comum
                new DateOnly(2026, 3, 31),
                new DateOnly(2026, 4, 30), // abril tem 30
            },
            dates);
    }

    [Fact]
    public void MensalDia30EmFevereiroCaiNoUltimoDia()
        => Assert.Equal(new DateOnly(2026, 2, 28), RecurrenceCalculator.MonthlyDate(2026, 2, 30));

    [Fact]
    public void AnoBissextoUsaODia29DeFevereiro()
    {
        Assert.Equal(new DateOnly(2028, 2, 29), RecurrenceCalculator.MonthlyDate(2028, 2, 31));
        Assert.Equal(new DateOnly(2028, 2, 29), RecurrenceCalculator.MonthlyDate(2028, 2, 29));
    }

    [Fact]
    public void NadaEGeradoAntesDaDataDeInicio()
    {
        var start = Today.AddDays(5);

        var dates = RecurrenceCalculator.Occurrences(Rule(RecurrenceType.Daily, start: start), Today, Today.AddDays(10));

        Assert.Equal(start, dates[0]);
    }

    [Fact]
    public void RotinaInativaNaoGeraNada()
        => Assert.Empty(RecurrenceCalculator.Occurrences(
            Rule(RecurrenceType.Daily, active: false), Today, Today.AddDays(10)));

    // ---------- Serviço ----------

    [Fact]
    public void CriarRotinaJaGeraAsOcorrenciasDoHorizonte()
    {
        using var fixture = new RoutineFixture(Now);

        var routine = fixture.Routines.Create("Academia", RecurrenceType.Daily);

        Assert.NotNull(routine);
        Assert.Equal(RoutineService.HorizonDays + 1, fixture.CountOccurrences(routine!.Id));
        Assert.Single(fixture.Routines.GetForDay(Today));
    }

    [Fact]
    public void GeracaoEIdempotenteENaoDuplica()
    {
        using var fixture = new RoutineFixture(Now);
        var routine = fixture.Routines.Create("Academia", RecurrenceType.Daily)!;

        var before = fixture.CountOccurrences(routine.Id);

        fixture.Routines.GenerateAll();
        fixture.Routines.GenerateAll();
        fixture.Routines.GenerateAll();

        Assert.Equal(before, fixture.CountOccurrences(routine.Id));
        Assert.Single(fixture.Routines.GetForDay(Today));
    }

    [Fact]
    public void RotinaComRegraIncompletaNaoECriada()
    {
        using var fixture = new RoutineFixture(Now);

        Assert.Null(fixture.Routines.Create("Sem dias", RecurrenceType.DaysOfWeek, daysOfWeek: 0));
        Assert.Null(fixture.Routines.Create("Sem intervalo", RecurrenceType.EveryXDays, intervalDays: 0));
        Assert.Null(fixture.Routines.Create("Dia inválido", RecurrenceType.Monthly, dayOfMonth: 32));
        Assert.Null(fixture.Routines.Create("   ", RecurrenceType.Daily));
        Assert.Empty(fixture.Routines.GetAll());
    }

    [Fact]
    public void ConcluirOcorrenciaNaoEncerraARotina()
    {
        using var fixture = new RoutineFixture(Now);
        var routine = fixture.Routines.Create("Academia", RecurrenceType.Daily)!;

        var hoje = fixture.Routines.GetForDay(Today).Single();
        Assert.True(fixture.Routines.SetOccurrenceCompleted(hoje.Occurrence.Id, true));

        // A rotina continua ativa e a ocorrência de amanhã continua pendente.
        Assert.True(fixture.Routines.GetById(routine.Id)!.IsActive);
        var amanha = fixture.Routines.GetForDay(Today.AddDays(1)).Single();
        Assert.False(amanha.Occurrence.IsCompleted);
    }

    [Fact]
    public void OcorrenciasSaoIndependentesEntreSi()
    {
        using var fixture = new RoutineFixture(Now);
        fixture.Routines.Create("Academia", RecurrenceType.Daily);

        var hoje = fixture.Routines.GetForDay(Today).Single();
        fixture.Routines.SetOccurrenceCompleted(hoje.Occurrence.Id, true);

        Assert.True(fixture.Routines.GetForDay(Today).Single().Occurrence.IsCompleted);
        Assert.False(fixture.Routines.GetForDay(Today.AddDays(1)).Single().Occurrence.IsCompleted);
        Assert.False(fixture.Routines.GetForDay(Today.AddDays(2)).Single().Occurrence.IsCompleted);
    }

    [Fact]
    public void DesmarcarOcorrenciaLimpaAConclusao()
    {
        using var fixture = new RoutineFixture(Now);
        fixture.Routines.Create("Academia", RecurrenceType.Daily);
        var id = fixture.Routines.GetForDay(Today).Single().Occurrence.Id;

        fixture.Routines.SetOccurrenceCompleted(id, true);
        fixture.Routines.SetOccurrenceCompleted(id, false);

        var occurrence = fixture.Routines.GetForDay(Today).Single().Occurrence;
        Assert.False(occurrence.IsCompleted);
        Assert.Null(occurrence.CompletedAtUtc);
    }

    [Fact]
    public void OcorrenciaConcluidaOntemNaoApareceHoje()
    {
        using var fixture = new RoutineFixture(Now);
        fixture.Routines.Create("Academia", RecurrenceType.Daily);

        var hoje = fixture.Routines.GetForDay(Today).Single();
        fixture.Routines.SetOccurrenceCompleted(hoje.Occurrence.Id, true);

        fixture.Clock.AdvanceDays(1);

        var amanha = fixture.Routines.GetForDay(fixture.Clock.Today).Single();
        Assert.NotEqual(hoje.Occurrence.Id, amanha.Occurrence.Id);
        Assert.False(amanha.Occurrence.IsCompleted);
    }

    [Fact]
    public void EditarRotinaMantemOPassadoERecalculaOFuturo()
    {
        using var fixture = new RoutineFixture(Now);
        var routine = fixture.Routines.Create("Academia", RecurrenceType.Daily)!;

        // Conclui a de hoje: ela representa algo que já aconteceu.
        var hoje = fixture.Routines.GetForDay(Today).Single();
        fixture.Routines.SetOccurrenceCompleted(hoje.Occurrence.Id, true);

        var mask = RecurrenceCalculator.MaskOf(DayOfWeek.Tuesday);
        Assert.True(fixture.Routines.Update(routine.Id, "Academia", RecurrenceType.DaysOfWeek, daysOfWeek: mask));

        // A concluída de hoje continua lá.
        Assert.True(fixture.Routines.GetForDay(Today).Single().Occurrence.IsCompleted);

        // O futuro segue a nova regra: só terças.
        var futuras = fixture.Routines.GetBetween(Today.AddDays(1), Today.AddDays(14));
        Assert.NotEmpty(futuras);
        Assert.All(futuras, o => Assert.Equal(DayOfWeek.Tuesday, o.Occurrence.OccurrenceDate.DayOfWeek));
    }

    [Fact]
    public void ExcluirRotinaRemoveAsOcorrencias()
    {
        using var fixture = new RoutineFixture(Now);
        var routine = fixture.Routines.Create("Academia", RecurrenceType.Daily)!;

        fixture.Routines.Delete(routine.Id);

        Assert.Empty(fixture.Routines.GetAll());
        Assert.Empty(fixture.Routines.GetForDay(Today));
        Assert.Equal(0, fixture.CountOccurrences(routine.Id));
    }

    [Fact]
    public void RotinasSobrevivemAoReinicio()
    {
        using var fixture = new RoutineFixture(Now);
        fixture.Routines.Create("Academia", RecurrenceType.DaysOfWeek,
            daysOfWeek: RecurrenceCalculator.MaskOf(DayOfWeek.Monday, DayOfWeek.Wednesday));

        fixture.Restart();

        var routine = fixture.Routines.GetAll().Single();
        Assert.Equal("Academia", routine.Title);
        Assert.Equal(RecurrenceType.DaysOfWeek, routine.RecurrenceType);
        Assert.True(RecurrenceCalculator.Includes(routine.DaysOfWeek, DayOfWeek.Monday));
        Assert.True(RecurrenceCalculator.Includes(routine.DaysOfWeek, DayOfWeek.Wednesday));
        Assert.False(RecurrenceCalculator.Includes(routine.DaysOfWeek, DayOfWeek.Friday));
    }

    // ---------- Integração com Hoje ----------

    [Fact]
    public void HojeMisturaTarefasEOcorrencias()
    {
        using var fixture = new RoutineFixture(Now);
        fixture.Tasks.AddForToday("Fazer lista");
        fixture.Routines.Create("Academia", RecurrenceType.Daily);

        var snapshot = fixture.Tasks.GetSnapshot();
        var (pending, completed) = DayBoard.Merge(
            snapshot.Pending, snapshot.CompletedToday, fixture.Routines.GetForDay(Today));

        Assert.Equal(new[] { "Fazer lista", "Academia" }, pending.Select(e => e.Title));
        Assert.Equal(new[] { EntryKind.Task, EntryKind.Routine }, pending.Select(e => e.Kind));
        Assert.Empty(completed);
    }

    [Fact]
    public void OcorrenciaConcluidaVaiParaOGrupoDeBaixo()
    {
        using var fixture = new RoutineFixture(Now);
        fixture.Tasks.AddForToday("Fazer lista");
        fixture.Routines.Create("Academia", RecurrenceType.Daily);

        var id = fixture.Routines.GetForDay(Today).Single().Occurrence.Id;
        fixture.Routines.SetOccurrenceCompleted(id, true);

        var snapshot = fixture.Tasks.GetSnapshot();
        var (pending, completed) = DayBoard.Merge(
            snapshot.Pending, snapshot.CompletedToday, fixture.Routines.GetForDay(Today));

        Assert.Equal(new[] { "Fazer lista" }, pending.Select(e => e.Title));
        Assert.Equal(new[] { "Academia" }, completed.Select(e => e.Title));
    }

    // ---------- Migração V0.3 → V0.4 ----------

    [Fact]
    public void MigracaoParaV04PreservaTarefasDatasESettings()
    {
        using var temp = new TempDatabaseFile();

        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={temp.FilePath};Pooling=False"))
        {
            connection.Open();
            using var create = connection.CreateCommand();
            create.CommandText = """
                CREATE TABLE tasks (
                    id TEXT PRIMARY KEY, title TEXT NOT NULL, is_completed INTEGER NOT NULL DEFAULT 0,
                    completed_local_date TEXT NULL, completed_at_utc TEXT NULL, sort_order INTEGER NOT NULL,
                    created_at_utc TEXT NOT NULL, updated_at_utc TEXT NOT NULL,
                    planned_date TEXT NULL, due_date TEXT NULL
                );
                CREATE TABLE settings (key TEXT PRIMARY KEY, value_json TEXT NOT NULL);
                CREATE TABLE display_layouts (
                    display_signature TEXT PRIMARY KEY, monitor_device TEXT NOT NULL,
                    x_dip REAL NOT NULL, y_dip REAL NOT NULL, width_dip REAL NOT NULL, height_dip REAL NOT NULL,
                    updated_at_utc TEXT NOT NULL);
                CREATE TABLE daily_review (local_date TEXT PRIMARY KEY, dismissed_at_utc TEXT NOT NULL);
                CREATE TABLE schema_info (version INTEGER NOT NULL);
                INSERT INTO schema_info (version) VALUES (3);
                INSERT INTO tasks VALUES ('t1', 'Relatório', 0, NULL, NULL, 0,
                    '2026-09-01T08:00:00.000Z', '2026-09-01T08:00:00.000Z', '2026-09-07', '2026-09-09');
                INSERT INTO settings (key, value_json) VALUES ('theme', '1');
                """;
            create.ExecuteNonQuery();
        }

        using var database = new AppDatabase(temp.FilePath);

        var task = new TaskRepository(database).GetAll().Single();
        Assert.Equal("Relatório", task.Title);
        Assert.Equal(new DateOnly(2026, 9, 7), task.PlannedDate);
        Assert.Equal(new DateOnly(2026, 9, 9), task.DueDate);
        Assert.Equal(1, new SettingsRepository(database).GetInt("theme", 0));

        // As tabelas novas existem e começam vazias: nada é convertido em rotina.
        Assert.Empty(new RoutineRepository(database).GetAll());
    }
}
