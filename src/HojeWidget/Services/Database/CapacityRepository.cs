using HojeWidget.Domain;
using Microsoft.Data.Sqlite;

namespace HojeWidget.Services.Database;

/// <summary>
/// V0.7 — capacidade semanal e exceções por data.
///
/// A capacidade é sempre em minutos. Zero num dia significa "não planejar
/// automaticamente nesse dia"; o usuário continua livre para colocar algo lá à mão.
/// </summary>
public sealed class CapacityRepository
{
    private readonly AppDatabase _database;

    public CapacityRepository(AppDatabase database) => _database = database;

    /// <summary>Minutos por dia da semana, indexados por 0 = segunda … 6 = domingo.</summary>
    public IReadOnlyDictionary<int, int> GetWeekly() => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT day_of_week, minutes FROM weekly_capacity;";

        var result = new Dictionary<int, int>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetInt32(0)] = reader.GetInt32(1);
        }

        return (IReadOnlyDictionary<int, int>)result;
    });

    public void SetWeekly(int dayIndex, int minutes) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO weekly_capacity (day_of_week, minutes) VALUES ($day, $minutes)
            ON CONFLICT(day_of_week) DO UPDATE SET minutes = excluded.minutes;
            """;
        command.Parameters.AddWithValue("$day", dayIndex);
        command.Parameters.AddWithValue("$minutes", Math.Max(0, minutes));
        command.ExecuteNonQuery();
    });

    /// <summary>Exceções por data, ordenadas.</summary>
    public IReadOnlyList<(DateOnly Date, int Minutes)> GetOverrides() => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT local_date, minutes FROM capacity_overrides ORDER BY local_date;";

        var result = new List<(DateOnly, int)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (LocalDate.Parse(reader.GetString(0)) is { } date)
            {
                result.Add((date, reader.GetInt32(1)));
            }
        }

        return (IReadOnlyList<(DateOnly, int)>)result;
    });

    public void SetOverride(DateOnly date, int minutes) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO capacity_overrides (local_date, minutes) VALUES ($date, $minutes)
            ON CONFLICT(local_date) DO UPDATE SET minutes = excluded.minutes;
            """;
        command.Parameters.AddWithValue("$date", LocalDate.From(date));
        command.Parameters.AddWithValue("$minutes", Math.Max(0, minutes));
        command.ExecuteNonQuery();
    });

    public void RemoveOverride(DateOnly date) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM capacity_overrides WHERE local_date = $date;";
        command.Parameters.AddWithValue("$date", LocalDate.From(date));
        command.ExecuteNonQuery();
    });
}

/// <summary>
/// V0.7 — a ordem do plano de um dia, com a marca de quem a decidiu.
/// É isso que faz o motor não desfazer um arrasto do usuário na execução seguinte.
/// </summary>
public sealed class DailyPlanRepository
{
    private readonly AppDatabase _database;

    public DailyPlanRepository(AppDatabase database) => _database = database;

    public IReadOnlyList<PlanEntryRecord> GetForDay(DateOnly date) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT item_kind, item_id, position, is_manual
              FROM daily_plan
             WHERE local_date = $date
             ORDER BY position;
            """;
        command.Parameters.AddWithValue("$date", LocalDate.From(date));

        var result = new List<PlanEntryRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new PlanEntryRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt64(3) != 0));
        }

        return (IReadOnlyList<PlanEntryRecord>)result;
    });

    /// <summary>Substitui o plano do dia inteiro, de forma atômica.</summary>
    public void Replace(DateOnly date, IReadOnlyList<PlanEntryRecord> entries) => _database.InTransaction(connection =>
    {
        using (var clear = connection.CreateCommand())
        {
            clear.CommandText = "DELETE FROM daily_plan WHERE local_date = $date;";
            clear.Parameters.AddWithValue("$date", LocalDate.From(date));
            clear.ExecuteNonQuery();
        }

        if (entries.Count == 0)
        {
            return;
        }

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO daily_plan (local_date, item_kind, item_id, position, is_manual)
            VALUES ($date, $kind, $id, $position, $manual);
            """;
        var dateParameter = insert.Parameters.Add("$date", SqliteType.Text);
        var kindParameter = insert.Parameters.Add("$kind", SqliteType.Integer);
        var idParameter = insert.Parameters.Add("$id", SqliteType.Text);
        var positionParameter = insert.Parameters.Add("$position", SqliteType.Integer);
        var manualParameter = insert.Parameters.Add("$manual", SqliteType.Integer);
        dateParameter.Value = LocalDate.From(date);

        foreach (var entry in entries)
        {
            kindParameter.Value = entry.Kind;
            idParameter.Value = entry.ItemId;
            positionParameter.Value = entry.Position;
            manualParameter.Value = entry.IsManual ? 1 : 0;
            insert.ExecuteNonQuery();
        }
    });
}

/// <summary>Uma linha da tabela do plano do dia.</summary>
public sealed record PlanEntryRecord(int Kind, string ItemId, int Position, bool IsManual);
