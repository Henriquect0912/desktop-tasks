namespace HojeWidget.Services.Database;

/// <summary>Registro das datas em que o aviso de revisao ja foi confirmado.</summary>
public sealed class DailyReviewRepository
{
    private readonly AppDatabase _database;

    public DailyReviewRepository(AppDatabase database) => _database = database;

    public bool IsDismissed(string localDate) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM daily_review WHERE local_date = $date LIMIT 1;";
        command.Parameters.AddWithValue("$date", localDate);
        return command.ExecuteScalar() is not null;
    });

    public void Dismiss(string localDate, DateTime dismissedAtUtc) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO daily_review (local_date, dismissed_at_utc) VALUES ($date, $at)
            ON CONFLICT(local_date) DO UPDATE SET dismissed_at_utc = excluded.dismissed_at_utc;
            """;
        command.Parameters.AddWithValue("$date", localDate);
        command.Parameters.AddWithValue("$at", Timestamps.ToStorage(dismissedAtUtc));
        command.ExecuteNonQuery();
    });
}
