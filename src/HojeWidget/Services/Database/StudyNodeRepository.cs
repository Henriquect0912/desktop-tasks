using HojeWidget.Models;
using Microsoft.Data.Sqlite;

namespace HojeWidget.Services.Database;

/// <summary>V0.7.1 — acesso à árvore de estudo e ao grafo de dependências.</summary>
public sealed class StudyNodeRepository
{
    private const string NodeColumns =
        "id, exam_id, parent_node_id, node_type, title, description, estimated_minutes, can_split, " +
        "mastery, question_count, sort_order, is_completed, completed_at_utc, created_at_utc";

    private readonly AppDatabase _database;

    public StudyNodeRepository(AppDatabase database) => _database = database;

    // ---------- Nós ----------

    public IReadOnlyList<StudyNode> GetByExam(string examId) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {NodeColumns} FROM study_nodes WHERE exam_id = $exam ORDER BY sort_order;";
        command.Parameters.AddWithValue("$exam", examId);

        var result = new List<StudyNode>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(ReadNode(reader));
        }

        return (IReadOnlyList<StudyNode>)result;
    });

    public StudyNode? GetNode(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {NodeColumns} FROM study_nodes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadNode(reader) : null;
    });

    public void Insert(StudyNode node) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO study_nodes (id, exam_id, parent_node_id, node_type, title, description, estimated_minutes,
                                     can_split, mastery, question_count, sort_order, is_completed, completed_at_utc,
                                     created_at_utc)
            VALUES ($id, $exam, $parent, $type, $title, $description, $minutes, $split, $mastery, $questions,
                    $sort, $done, $doneAt, $created);
            """;
        Bind(command, node);
        command.ExecuteNonQuery();
    });

    public void Update(StudyNode node) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE study_nodes
               SET parent_node_id = $parent, node_type = $type, title = $title, description = $description,
                   estimated_minutes = $minutes, can_split = $split, mastery = $mastery,
                   question_count = $questions, sort_order = $sort, is_completed = $done,
                   completed_at_utc = $doneAt
             WHERE id = $id;
            """;
        Bind(command, node);
        command.ExecuteNonQuery();
    });

    /// <summary>Remove o nó. Filhos e arestas saem junto, por integridade referencial.</summary>
    public void Delete(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM study_nodes WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    });

    public long GetNextSortOrder(string examId) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM study_nodes WHERE exam_id = $exam;";
        command.Parameters.AddWithValue("$exam", examId);
        return Convert.ToInt64(command.ExecuteScalar());
    });

    // ---------- Dependências ----------

    public IReadOnlyList<StudyDependency> GetDependencies(string examId) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, exam_id, node_id, depends_on_node_id
              FROM study_dependencies
             WHERE exam_id = $exam;
            """;
        command.Parameters.AddWithValue("$exam", examId);

        var result = new List<StudyDependency>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new StudyDependency
            {
                Id = reader.GetString(0),
                ExamId = reader.GetString(1),
                NodeId = reader.GetString(2),
                DependsOnNodeId = reader.GetString(3),
            });
        }

        return (IReadOnlyList<StudyDependency>)result;
    });

    public void InsertDependency(StudyDependency dependency) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO study_dependencies (id, exam_id, node_id, depends_on_node_id)
            VALUES ($id, $exam, $node, $dependsOn);
            """;
        command.Parameters.AddWithValue("$id", dependency.Id);
        command.Parameters.AddWithValue("$exam", dependency.ExamId);
        command.Parameters.AddWithValue("$node", dependency.NodeId);
        command.Parameters.AddWithValue("$dependsOn", dependency.DependsOnNodeId);
        command.ExecuteNonQuery();
    });

    public void DeleteDependency(string nodeId, string dependsOnId) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM study_dependencies
             WHERE node_id = $node AND depends_on_node_id = $dependsOn;
            """;
        command.Parameters.AddWithValue("$node", nodeId);
        command.Parameters.AddWithValue("$dependsOn", dependsOnId);
        command.ExecuteNonQuery();
    });

    // ---------- Mapeamento ----------

    private static void Bind(SqliteCommand command, StudyNode node)
    {
        command.Parameters.AddWithValue("$id", node.Id);
        command.Parameters.AddWithValue("$exam", node.ExamId);
        command.Parameters.AddWithValue("$parent", (object?)node.ParentNodeId ?? DBNull.Value);
        command.Parameters.AddWithValue("$type", (int)node.NodeType);
        command.Parameters.AddWithValue("$title", node.Title);
        command.Parameters.AddWithValue("$description", (object?)node.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$minutes", (object?)node.EstimatedMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$split", node.CanSplit ? 1 : 0);
        command.Parameters.AddWithValue("$mastery", node.MasteryLevel is { } m ? (int)m : DBNull.Value);
        command.Parameters.AddWithValue("$questions", (object?)node.QuestionCount ?? DBNull.Value);
        command.Parameters.AddWithValue("$sort", node.SortOrder);
        command.Parameters.AddWithValue("$done", node.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$doneAt", node.CompletedAtUtc is { } at ? Timestamps.ToStorage(at) : DBNull.Value);
        command.Parameters.AddWithValue("$created", Timestamps.ToStorage(node.CreatedAtUtc));
    }

    private static StudyNode ReadNode(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        ExamId = reader.GetString(1),
        ParentNodeId = reader.IsDBNull(2) ? null : reader.GetString(2),
        NodeType = (StudyNodeType)reader.GetInt32(3),
        Title = reader.GetString(4),
        Description = reader.IsDBNull(5) ? null : reader.GetString(5),
        EstimatedMinutes = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        CanSplit = reader.GetInt64(7) != 0,
        MasteryLevel = reader.IsDBNull(8) ? null : (Mastery)reader.GetInt32(8),
        QuestionCount = reader.IsDBNull(9) ? null : reader.GetInt32(9),
        SortOrder = reader.GetInt64(10),
        IsCompleted = reader.GetInt64(11) != 0,
        CompletedAtUtc = reader.IsDBNull(12) ? null : Timestamps.FromStorage(reader.GetString(12)),
        CreatedAtUtc = Timestamps.FromStorage(reader.GetString(13)),
    };
}
