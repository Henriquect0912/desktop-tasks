using HojeWidget.Domain;
using HojeWidget.Models;
using Microsoft.Data.Sqlite;

namespace HojeWidget.Services.Database;

/// <summary>V0.5 — acesso a projetos e entregas. Consultas parametrizadas.</summary>
public sealed class ContainerRepository
{
    private const string SelectColumns =
        "id, kind, title, due_date, is_completed, completed_at_utc, sort_order, created_at_utc, updated_at_utc, priority";

    private readonly AppDatabase _database;

    public ContainerRepository(AppDatabase database) => _database = database;

    public IReadOnlyList<Container> GetAll() => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM containers ORDER BY kind, sort_order, created_at_utc;";

        var result = new List<Container>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(Read(reader));
        }

        return (IReadOnlyList<Container>)result;
    });

    public Container? GetById(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM containers WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    });

    public void Insert(Container container) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO containers (id, kind, title, due_date, is_completed, completed_at_utc, sort_order, created_at_utc, updated_at_utc, priority)
            VALUES ($id, $kind, $title, $due, $done, $doneAt, $sort, $created, $updated, $priority);
            """;
        Bind(command, container);
        command.ExecuteNonQuery();
    });

    public void Update(Container container) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE containers
               SET kind = $kind,
                   title = $title,
                   due_date = $due,
                   is_completed = $done,
                   completed_at_utc = $doneAt,
                   sort_order = $sort,
                   updated_at_utc = $updated,
                   priority = $priority
             WHERE id = $id;
            """;
        Bind(command, container);
        command.ExecuteNonQuery();
    });

    public void Delete(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM containers WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    });

    public long GetNextSortOrder(ContainerKind kind) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM containers WHERE kind = $kind;";
        command.Parameters.AddWithValue("$kind", (int)kind);
        return Convert.ToInt64(command.ExecuteScalar());
    });

    private static void Bind(SqliteCommand command, Container container)
    {
        command.Parameters.AddWithValue("$id", container.Id);
        command.Parameters.AddWithValue("$kind", (int)container.Kind);
        command.Parameters.AddWithValue("$title", container.Title);
        command.Parameters.AddWithValue("$due", (object?)LocalDate.From(container.DueDate) ?? DBNull.Value);
        command.Parameters.AddWithValue("$done", container.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$doneAt", container.CompletedAtUtc is { } at ? Timestamps.ToStorage(at) : DBNull.Value);
        command.Parameters.AddWithValue("$sort", container.SortOrder);
        command.Parameters.AddWithValue("$created", Timestamps.ToStorage(container.CreatedAtUtc));
        command.Parameters.AddWithValue("$updated", Timestamps.ToStorage(container.UpdatedAtUtc));
        command.Parameters.AddWithValue("$priority", (int)container.Priority);
    }

    private static Container Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Kind = (ContainerKind)reader.GetInt32(1),
        Title = reader.GetString(2),
        DueDate = reader.IsDBNull(3) ? null : LocalDate.Parse(reader.GetString(3)),
        IsCompleted = reader.GetInt64(4) != 0,
        CompletedAtUtc = reader.IsDBNull(5) ? null : Timestamps.FromStorage(reader.GetString(5)),
        SortOrder = reader.GetInt64(6),
        CreatedAtUtc = Timestamps.FromStorage(reader.GetString(7)),
        UpdatedAtUtc = Timestamps.FromStorage(reader.GetString(8)),
        Priority = (Priority)reader.GetInt32(9),
    };
}
