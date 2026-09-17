using System.Globalization;
using HojeWidget.Domain;
using HojeWidget.Models;
using Microsoft.Data.Sqlite;

namespace HojeWidget.Services.Database;

/// <summary>Acesso a tabela de tarefas. Todas as consultas sao parametrizadas.</summary>
public sealed class TaskRepository
{
    private const string SelectColumns =
        "id, title, is_completed, completed_local_date, completed_at_utc, sort_order, created_at_utc, updated_at_utc, " +
        "planned_date, due_date, parent_id, estimated_minutes, can_split, priority, manual_planning";

    private readonly AppDatabase _database;

    public TaskRepository(AppDatabase database) => _database = database;

    public IReadOnlyList<TaskItem> GetAll() => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM tasks ORDER BY sort_order, created_at_utc;";

        var result = new List<TaskItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(Read(reader));
        }

        return (IReadOnlyList<TaskItem>)result;
    });

    public TaskItem? GetById(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM tasks WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    });

    /// <summary>V0.5 — subtarefas de um projeto ou entrega, na ordem manual.</summary>
    public IReadOnlyList<TaskItem> GetByParent(string parentId) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM tasks WHERE parent_id = $parentId ORDER BY sort_order, created_at_utc;";
        command.Parameters.AddWithValue("$parentId", parentId);

        var result = new List<TaskItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(Read(reader));
        }

        return (IReadOnlyList<TaskItem>)result;
    });

    /// <summary>
    /// V0.5 — solta as subtarefas de um container. Excluir um projeto não apaga
    /// o trabalho já registrado: as subtarefas voltam a ser tarefas soltas.
    /// </summary>
    public void DetachChildren(string parentId, DateTime updatedAtUtc) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE tasks SET parent_id = NULL, updated_at_utc = $updated WHERE parent_id = $parentId;";
        command.Parameters.AddWithValue("$parentId", parentId);
        command.Parameters.AddWithValue("$updated", Timestamps.ToStorage(updatedAtUtc));
        command.ExecuteNonQuery();
    });

    public void Insert(TaskItem item) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO tasks (id, title, is_completed, completed_local_date, completed_at_utc, sort_order, created_at_utc, updated_at_utc,
                               planned_date, due_date, parent_id, estimated_minutes, can_split, priority, manual_planning)
            VALUES ($id, $title, $isCompleted, $completedLocalDate, $completedAtUtc, $sortOrder, $createdAtUtc, $updatedAtUtc,
                    $plannedDate, $dueDate, $parentId, $estimatedMinutes, $canSplit, $priority, $manualPlanning);
            """;
        Bind(command, item);
        command.ExecuteNonQuery();
    });

    public void Update(TaskItem item) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE tasks
               SET title = $title,
                   is_completed = $isCompleted,
                   completed_local_date = $completedLocalDate,
                   completed_at_utc = $completedAtUtc,
                   sort_order = $sortOrder,
                   updated_at_utc = $updatedAtUtc,
                   planned_date = $plannedDate,
                   due_date = $dueDate,
                   parent_id = $parentId,
                   estimated_minutes = $estimatedMinutes,
                   can_split = $canSplit,
                   priority = $priority,
                   manual_planning = $manualPlanning
             WHERE id = $id;
            """;
        Bind(command, item);
        command.ExecuteNonQuery();
    });

    public void Delete(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tasks WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    });

    public long GetNextSortOrder() => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM tasks;";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    });

    /// <summary>Aplica novas posicoes de forma atomica.</summary>
    public void ApplySortOrders(IReadOnlyList<(string Id, long SortOrder)> assignments, DateTime updatedAtUtc)
    {
        if (assignments.Count == 0)
        {
            return;
        }

        _database.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE tasks SET sort_order = $sortOrder, updated_at_utc = $updatedAtUtc WHERE id = $id;";
            var idParameter = command.Parameters.Add("$id", SqliteType.Text);
            var sortParameter = command.Parameters.Add("$sortOrder", SqliteType.Integer);
            var updatedParameter = command.Parameters.Add("$updatedAtUtc", SqliteType.Text);
            updatedParameter.Value = Timestamps.ToStorage(updatedAtUtc);

            foreach (var (id, sortOrder) in assignments)
            {
                idParameter.Value = id;
                sortParameter.Value = sortOrder;
                command.ExecuteNonQuery();
            }
        });
    }

    /// <summary>
    /// Renumera sort_order para 0..N-1 preservando a ordem atual.
    /// Mantem os valores compactos sem alterar nenhuma posicao relativa.
    /// </summary>
    public void NormalizeSortOrders() => _database.InTransaction(connection =>
    {
        var ids = new List<string>();
        using (var read = connection.CreateCommand())
        {
            read.CommandText = "SELECT id FROM tasks ORDER BY sort_order, created_at_utc;";
            using var reader = read.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetString(0));
            }
        }

        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE tasks SET sort_order = $sortOrder WHERE id = $id;";
        var idParameter = update.Parameters.Add("$id", SqliteType.Text);
        var sortParameter = update.Parameters.Add("$sortOrder", SqliteType.Integer);

        for (var i = 0; i < ids.Count; i++)
        {
            idParameter.Value = ids[i];
            sortParameter.Value = (long)i;
            update.ExecuteNonQuery();
        }
    });

    private static void Bind(SqliteCommand command, TaskItem item)
    {
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$isCompleted", item.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$completedLocalDate", (object?)item.CompletedLocalDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$completedAtUtc", item.CompletedAtUtc is { } completed ? Timestamps.ToStorage(completed) : DBNull.Value);
        command.Parameters.AddWithValue("$sortOrder", item.SortOrder);
        command.Parameters.AddWithValue("$createdAtUtc", Timestamps.ToStorage(item.CreatedAtUtc));
        command.Parameters.AddWithValue("$updatedAtUtc", Timestamps.ToStorage(item.UpdatedAtUtc));
        command.Parameters.AddWithValue("$plannedDate", (object?)LocalDate.From(item.PlannedDate) ?? DBNull.Value);
        command.Parameters.AddWithValue("$dueDate", (object?)LocalDate.From(item.DueDate) ?? DBNull.Value);
        command.Parameters.AddWithValue("$parentId", (object?)item.ParentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$estimatedMinutes", (object?)item.EstimatedMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$canSplit", item.CanSplit ? 1 : 0);
        command.Parameters.AddWithValue("$priority", (int)item.Priority);
        command.Parameters.AddWithValue("$manualPlanning", item.ManualPlanning ? 1 : 0);
    }

    private static TaskItem Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Title = reader.GetString(1),
        IsCompleted = reader.GetInt64(2) != 0,
        CompletedLocalDate = reader.IsDBNull(3) ? null : reader.GetString(3),
        CompletedAtUtc = reader.IsDBNull(4) ? null : Timestamps.FromStorage(reader.GetString(4)),
        SortOrder = reader.GetInt64(5),
        CreatedAtUtc = Timestamps.FromStorage(reader.GetString(6)),
        UpdatedAtUtc = Timestamps.FromStorage(reader.GetString(7)),
        PlannedDate = reader.IsDBNull(8) ? null : LocalDate.Parse(reader.GetString(8)),
        DueDate = reader.IsDBNull(9) ? null : LocalDate.Parse(reader.GetString(9)),
        ParentId = reader.IsDBNull(10) ? null : reader.GetString(10),
        EstimatedMinutes = reader.IsDBNull(11) ? null : reader.GetInt32(11),
        CanSplit = reader.GetInt64(12) != 0,
        Priority = (Priority)reader.GetInt32(13),
        ManualPlanning = reader.GetInt64(14) != 0,
    };
}
