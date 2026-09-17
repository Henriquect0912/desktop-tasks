using HojeWidget.Domain;
using HojeWidget.Models;
using Microsoft.Data.Sqlite;

namespace HojeWidget.Services.Database;

/// <summary>Acesso às rotinas e às suas ocorrências. Consultas parametrizadas.</summary>
public sealed class RoutineRepository
{
    private const string RoutineColumns =
        "id, title, recurrence_type, start_date, interval_days, days_of_week, day_of_month, is_active, created_at_utc, updated_at_utc";

    private const string OccurrenceColumns =
        "id, routine_id, occurrence_date, is_completed, completed_at_utc";

    private readonly AppDatabase _database;

    public RoutineRepository(AppDatabase database) => _database = database;

    // ---------- Rotinas ----------

    public IReadOnlyList<Routine> GetAll() => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {RoutineColumns} FROM routines ORDER BY title;";

        var result = new List<Routine>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(ReadRoutine(reader));
        }

        return (IReadOnlyList<Routine>)result;
    });

    public Routine? GetById(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {RoutineColumns} FROM routines WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadRoutine(reader) : null;
    });

    public void Insert(Routine routine) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO routines (id, title, recurrence_type, start_date, interval_days, days_of_week, day_of_month, is_active, created_at_utc, updated_at_utc)
            VALUES ($id, $title, $type, $start, $interval, $days, $dayOfMonth, $active, $created, $updated);
            """;
        BindRoutine(command, routine);
        command.ExecuteNonQuery();
    });

    public void Update(Routine routine) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE routines
               SET title = $title,
                   recurrence_type = $type,
                   start_date = $start,
                   interval_days = $interval,
                   days_of_week = $days,
                   day_of_month = $dayOfMonth,
                   is_active = $active,
                   updated_at_utc = $updated
             WHERE id = $id;
            """;
        BindRoutine(command, routine);
        command.ExecuteNonQuery();
    });

    /// <summary>Remove a rotina. As ocorrências saem junto por ON DELETE CASCADE.</summary>
    public void Delete(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM routines WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    });

    // ---------- Ocorrências ----------

    /// <summary>
    /// Insere as datas informadas ignorando as que já existem.
    /// É a constraint única que torna a geração idempotente.
    /// </summary>
    public void EnsureOccurrences(string routineId, IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count == 0)
        {
            return;
        }

        _database.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO routine_occurrences (id, routine_id, occurrence_date, is_completed, completed_at_utc)
                VALUES ($id, $routineId, $date, 0, NULL);
                """;
            var idParameter = command.Parameters.Add("$id", SqliteType.Text);
            var routineParameter = command.Parameters.Add("$routineId", SqliteType.Text);
            var dateParameter = command.Parameters.Add("$date", SqliteType.Text);
            routineParameter.Value = routineId;

            foreach (var date in dates)
            {
                idParameter.Value = Guid.NewGuid().ToString("n");
                dateParameter.Value = LocalDate.From(date);
                command.ExecuteNonQuery();
            }
        });
    }

    /// <summary>Ocorrências de um intervalo, com o título da rotina já resolvido.</summary>
    public IReadOnlyList<RoutineOccurrenceView> GetBetween(DateOnly from, DateOnly to) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {string.Join(", ", OccurrenceColumns.Split(", ").Select(c => "o." + c))}, r.title
              FROM routine_occurrences o
              JOIN routines r ON r.id = o.routine_id
             WHERE o.occurrence_date BETWEEN $from AND $to
             ORDER BY o.occurrence_date, r.title;
            """;
        command.Parameters.AddWithValue("$from", LocalDate.From(from));
        command.Parameters.AddWithValue("$to", LocalDate.From(to));

        var result = new List<RoutineOccurrenceView>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new RoutineOccurrenceView(ReadOccurrence(reader), reader.GetString(5)));
        }

        return (IReadOnlyList<RoutineOccurrenceView>)result;
    });

    public RoutineOccurrence? GetOccurrence(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {OccurrenceColumns} FROM routine_occurrences WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadOccurrence(reader) : null;
    });

    public void UpdateOccurrence(RoutineOccurrence occurrence) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE routine_occurrences
               SET is_completed = $done, completed_at_utc = $at
             WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", occurrence.Id);
        command.Parameters.AddWithValue("$done", occurrence.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$at", occurrence.CompletedAtUtc is { } at ? Timestamps.ToStorage(at) : DBNull.Value);
        command.ExecuteNonQuery();
    });

    /// <summary>
    /// Apaga ocorrências futuras ainda pendentes. O passado e o que já foi concluído
    /// ficam intactos: a história do que aconteceu não muda quando a regra muda.
    /// </summary>
    public void DeleteFuturePending(string routineId, DateOnly from) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM routine_occurrences
             WHERE routine_id = $routineId
               AND occurrence_date >= $from
               AND is_completed = 0;
            """;
        command.Parameters.AddWithValue("$routineId", routineId);
        command.Parameters.AddWithValue("$from", LocalDate.From(from));
        command.ExecuteNonQuery();
    });

    public int CountOccurrences(string routineId) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM routine_occurrences WHERE routine_id = $id;";
        command.Parameters.AddWithValue("$id", routineId);
        return Convert.ToInt32(command.ExecuteScalar());
    });

    // ---------- Mapeamento ----------

    private static void BindRoutine(SqliteCommand command, Routine routine)
    {
        command.Parameters.AddWithValue("$id", routine.Id);
        command.Parameters.AddWithValue("$title", routine.Title);
        command.Parameters.AddWithValue("$type", (int)routine.RecurrenceType);
        command.Parameters.AddWithValue("$start", LocalDate.From(routine.StartDate));
        command.Parameters.AddWithValue("$interval", (object?)routine.IntervalDays ?? DBNull.Value);
        command.Parameters.AddWithValue("$days", routine.DaysOfWeek);
        command.Parameters.AddWithValue("$dayOfMonth", (object?)routine.DayOfMonth ?? DBNull.Value);
        command.Parameters.AddWithValue("$active", routine.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$created", Timestamps.ToStorage(routine.CreatedAtUtc));
        command.Parameters.AddWithValue("$updated", Timestamps.ToStorage(routine.UpdatedAtUtc));
    }

    private static Routine ReadRoutine(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Title = reader.GetString(1),
        RecurrenceType = (RecurrenceType)reader.GetInt32(2),
        StartDate = LocalDate.Parse(reader.GetString(3)) ?? DateOnly.FromDateTime(DateTime.Now),
        IntervalDays = reader.IsDBNull(4) ? null : reader.GetInt32(4),
        DaysOfWeek = reader.GetInt32(5),
        DayOfMonth = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        IsActive = reader.GetInt64(7) != 0,
        CreatedAtUtc = Timestamps.FromStorage(reader.GetString(8)),
        UpdatedAtUtc = Timestamps.FromStorage(reader.GetString(9)),
    };

    private static RoutineOccurrence ReadOccurrence(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        RoutineId = reader.GetString(1),
        OccurrenceDate = LocalDate.Parse(reader.GetString(2)) ?? DateOnly.FromDateTime(DateTime.Now),
        IsCompleted = reader.GetInt64(3) != 0,
        CompletedAtUtc = reader.IsDBNull(4) ? null : Timestamps.FromStorage(reader.GetString(4)),
    };
}
