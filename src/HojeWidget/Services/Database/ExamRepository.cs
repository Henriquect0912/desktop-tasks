using HojeWidget.Domain;
using HojeWidget.Models;
using Microsoft.Data.Sqlite;

namespace HojeWidget.Services.Database;

/// <summary>V0.6 / V0.7.1 — provas e sessões de estudo.</summary>
public sealed class ExamRepository
{
    private const string ExamColumns =
        "id, subject, title, exam_date, notes, created_at_utc, updated_at_utc, " +
        "comfortable_daily_minutes, max_daily_minutes, max_session_minutes";

    private const string SessionColumns =
        "id, exam_id, node_id, title, estimated_minutes, planned_date, can_split, is_completed, completed_at_utc, " +
        "sort_order, created_at_utc, updated_at_utc, priority, manual_planning, needs_review";

    private readonly AppDatabase _database;

    public ExamRepository(AppDatabase database) => _database = database;

    // ---------- Provas ----------

    public IReadOnlyList<Exam> GetAllExams() => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {ExamColumns} FROM exams ORDER BY exam_date, subject;";
        return ReadAll(command, ReadExam);
    });

    public Exam? GetExam(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {ExamColumns} FROM exams WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadExam(reader) : null;
    });

    public void InsertExam(Exam exam) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO exams (id, subject, title, exam_date, notes, created_at_utc, updated_at_utc,
                               comfortable_daily_minutes, max_daily_minutes, max_session_minutes)
            VALUES ($id, $subject, $title, $date, $notes, $created, $updated,
                    $comfortable, $maxDaily, $maxSession);
            """;
        BindExam(command, exam);
        command.ExecuteNonQuery();
    });

    public void UpdateExam(Exam exam) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE exams
               SET subject = $subject, title = $title, exam_date = $date, notes = $notes, updated_at_utc = $updated,
                   comfortable_daily_minutes = $comfortable, max_daily_minutes = $maxDaily,
                   max_session_minutes = $maxSession
             WHERE id = $id;
            """;
        BindExam(command, exam);
        command.ExecuteNonQuery();
    });

    /// <summary>Conteúdos e sessões saem junto, por integridade referencial.</summary>
    public void DeleteExam(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM exams WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    });

    // Conteúdos e listas viraram study_nodes na V0.7.1; a tabela exam_topics
    // continua no banco como retrato do que existia antes, mas ninguém mais
    // escreve nela.

    // ---------- Sessões de estudo ----------

    public IReadOnlyList<StudySession> GetSessions(string examId) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SessionColumns} FROM study_sessions WHERE exam_id = $exam ORDER BY sort_order;";
        command.Parameters.AddWithValue("$exam", examId);
        return ReadAll(command, ReadSession);
    });

    public IReadOnlyList<StudySession> GetAllSessions() => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SessionColumns} FROM study_sessions ORDER BY planned_date, sort_order;";
        return ReadAll(command, ReadSession);
    });

    /// <summary>Sessões de um intervalo, já com o nome da prova e o domínio do conteúdo.</summary>
    public IReadOnlyList<StudySessionView> GetSessionsBetween(DateOnly from, DateOnly to) => _database.Execute(connection =>
    {
        var prefixed = string.Join(", ", SessionColumns.Split(", ").Select(c => "s." + c));

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {prefixed}, e.subject, e.title, e.exam_date, COALESCE(t.mastery, 0)
              FROM study_sessions s
              JOIN exams e ON e.id = s.exam_id
              LEFT JOIN study_nodes t ON t.id = s.node_id
             WHERE s.planned_date BETWEEN $from AND $to
             ORDER BY s.planned_date, s.sort_order;
            """;
        command.Parameters.AddWithValue("$from", LocalDate.From(from));
        command.Parameters.AddWithValue("$to", LocalDate.From(to));

        var result = new List<StudySessionView>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var session = ReadSession(reader);
            var subject = reader.GetString(15);
            var title = reader.GetString(16);
            var examDate = LocalDate.Parse(reader.GetString(17)) ?? DateOnly.FromDateTime(DateTime.Now);
            var mastery = (Mastery)reader.GetInt32(18);

            var name = string.IsNullOrWhiteSpace(title) ? subject : subject + " — " + title;
            result.Add(new StudySessionView(session, name, examDate, mastery));
        }

        return (IReadOnlyList<StudySessionView>)result;
    });

    /// <summary>
    /// Todas as sessões de uma prova, já resolvidas para exibição — inclusive as
    /// que ainda não têm dia, que a consulta por intervalo deixaria de fora.
    /// </summary>
    public IReadOnlyList<StudySessionView> GetSessionViews(string examId) => _database.Execute(connection =>
    {
        var prefixed = string.Join(", ", SessionColumns.Split(", ").Select(c => "s." + c));

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {prefixed}, e.subject, e.title, e.exam_date, COALESCE(t.mastery, 0)
              FROM study_sessions s
              JOIN exams e ON e.id = s.exam_id
              LEFT JOIN study_nodes t ON t.id = s.node_id
             WHERE s.exam_id = $exam
             ORDER BY s.planned_date, s.sort_order;
            """;
        command.Parameters.AddWithValue("$exam", examId);

        var result = new List<StudySessionView>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var session = ReadSession(reader);
            var subject = reader.GetString(15);
            var title = reader.GetString(16);
            var examDate = LocalDate.Parse(reader.GetString(17)) ?? DateOnly.FromDateTime(DateTime.Now);
            var mastery = (Mastery)reader.GetInt32(18);

            var name = string.IsNullOrWhiteSpace(title) ? subject : subject + " — " + title;
            result.Add(new StudySessionView(session, name, examDate, mastery));
        }

        return (IReadOnlyList<StudySessionView>)result;
    });

    public StudySession? GetSession(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SessionColumns} FROM study_sessions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadSession(reader) : null;
    });

    public void InsertSession(StudySession session) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO study_sessions (id, exam_id, node_id, title, estimated_minutes, planned_date, can_split,
                                        is_completed, completed_at_utc, sort_order, created_at_utc, updated_at_utc,
                                        priority, manual_planning, needs_review)
            VALUES ($id, $exam, $node, $title, $minutes, $planned, $split, $done, $doneAt, $sort, $created, $updated,
                    $priority, $manual, $review);
            """;
        BindSession(command, session);
        command.ExecuteNonQuery();
    });

    public void UpdateSession(StudySession session) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE study_sessions
               SET node_id = $node, title = $title, estimated_minutes = $minutes, planned_date = $planned,
                   can_split = $split, is_completed = $done, completed_at_utc = $doneAt, sort_order = $sort,
                   updated_at_utc = $updated, priority = $priority, manual_planning = $manual,
                   needs_review = $review
             WHERE id = $id;
            """;
        BindSession(command, session);
        command.ExecuteNonQuery();
    });

    public void DeleteSession(string id) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM study_sessions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    });

    /// <summary>Sessões pendentes ligadas a um nó. Usado antes de remover o nó.</summary>
    public IReadOnlyList<StudySession> GetPendingSessionsForNode(string nodeId) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SessionColumns} FROM study_sessions WHERE node_id = $node AND is_completed = 0;";
        command.Parameters.AddWithValue("$node", nodeId);
        return ReadAll(command, ReadSession);
    });

    /// <summary>
    /// Marca sessões futuras como precisando de revisão. Usado quando uma regra
    /// muda e o plano existente deixa de ser válido — nada é apagado.
    /// </summary>
    public void FlagSessionsForReview(IReadOnlyList<string> sessionIds)
    {
        if (sessionIds.Count == 0)
        {
            return;
        }

        _database.InTransaction(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE study_sessions SET needs_review = 1 WHERE id = $id;";
            var parameter = command.Parameters.Add("$id", SqliteType.Text);

            foreach (var id in sessionIds)
            {
                parameter.Value = id;
                command.ExecuteNonQuery();
            }
        });
    }

    public long GetNextSessionSortOrder(string examId) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM study_sessions WHERE exam_id = $exam;";
        command.Parameters.AddWithValue("$exam", examId);
        return Convert.ToInt64(command.ExecuteScalar());
    });

    // ---------- Mapeamento ----------

    private static IReadOnlyList<T> ReadAll<T>(SqliteCommand command, Func<SqliteDataReader, T> read)
    {
        var result = new List<T>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(read(reader));
        }

        return result;
    }

    private static void BindExam(SqliteCommand command, Exam exam)
    {
        command.Parameters.AddWithValue("$id", exam.Id);
        command.Parameters.AddWithValue("$subject", exam.Subject);
        command.Parameters.AddWithValue("$title", exam.Title);
        command.Parameters.AddWithValue("$date", LocalDate.From(exam.ExamDate));
        command.Parameters.AddWithValue("$notes", (object?)exam.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", Timestamps.ToStorage(exam.CreatedAtUtc));
        command.Parameters.AddWithValue("$updated", Timestamps.ToStorage(exam.UpdatedAtUtc));
        command.Parameters.AddWithValue("$comfortable", exam.Policy.ComfortableDailyMinutes);
        command.Parameters.AddWithValue("$maxDaily", exam.Policy.MaxDailyMinutes);
        command.Parameters.AddWithValue("$maxSession", exam.Policy.MaxSessionMinutes);
    }

    private static Exam ReadExam(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Subject = reader.GetString(1),
        Title = reader.GetString(2),
        ExamDate = LocalDate.Parse(reader.GetString(3)) ?? DateOnly.FromDateTime(DateTime.Now),
        Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
        CreatedAtUtc = Timestamps.FromStorage(reader.GetString(5)),
        UpdatedAtUtc = Timestamps.FromStorage(reader.GetString(6)),
        Policy = new ExamStudyPolicy(reader.GetInt32(7), reader.GetInt32(8), reader.GetInt32(9)),
    };

    private static void BindSession(SqliteCommand command, StudySession session)
    {
        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$exam", session.ExamId);
        command.Parameters.AddWithValue("$node", (object?)session.NodeId ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", session.Title);
        command.Parameters.AddWithValue("$minutes", (object?)session.EstimatedMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$planned", (object?)LocalDate.From(session.PlannedDate) ?? DBNull.Value);
        command.Parameters.AddWithValue("$split", session.CanSplit ? 1 : 0);
        command.Parameters.AddWithValue("$done", session.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$doneAt", session.CompletedAtUtc is { } at ? Timestamps.ToStorage(at) : DBNull.Value);
        command.Parameters.AddWithValue("$sort", session.SortOrder);
        command.Parameters.AddWithValue("$created", Timestamps.ToStorage(session.CreatedAtUtc));
        command.Parameters.AddWithValue("$updated", Timestamps.ToStorage(session.UpdatedAtUtc));
        command.Parameters.AddWithValue("$priority", (int)session.Priority);
        command.Parameters.AddWithValue("$manual", session.ManualPlanning ? 1 : 0);
        command.Parameters.AddWithValue("$review", session.NeedsReview ? 1 : 0);
    }

    private static StudySession ReadSession(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        ExamId = reader.GetString(1),
        NodeId = reader.IsDBNull(2) ? null : reader.GetString(2),
        Title = reader.GetString(3),
        EstimatedMinutes = reader.IsDBNull(4) ? null : reader.GetInt32(4),
        PlannedDate = reader.IsDBNull(5) ? null : LocalDate.Parse(reader.GetString(5)),
        CanSplit = reader.GetInt64(6) != 0,
        IsCompleted = reader.GetInt64(7) != 0,
        CompletedAtUtc = reader.IsDBNull(8) ? null : Timestamps.FromStorage(reader.GetString(8)),
        SortOrder = reader.GetInt64(9),
        CreatedAtUtc = Timestamps.FromStorage(reader.GetString(10)),
        UpdatedAtUtc = Timestamps.FromStorage(reader.GetString(11)),
        Priority = (Priority)reader.GetInt32(12),
        ManualPlanning = reader.GetInt64(13) != 0,
        NeedsReview = reader.GetInt64(14) != 0,
    };
}
