using System.Globalization;
using System.Text.Json;

namespace HojeWidget.Services.Database;

/// <summary>Chave/valor simples para as poucas preferencias da V0.1.</summary>
public sealed class SettingsRepository
{
    public const string ThemeKey = "theme";
    public const string StartupInitializedKey = "startup.initialized";

    /// <summary>Última vez que pendências atrasadas foram trazidas para o dia, e quantas.</summary>
    public const string CarryForwardKey = "carryforward.last";

    private readonly AppDatabase _database;

    public SettingsRepository(AppDatabase database) => _database = database;

    public string? GetRaw(string key) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value_json FROM settings WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    });

    public void SetRaw(string key, string valueJson) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO settings (key, value_json) VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value_json = excluded.value_json;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", valueJson);
        command.ExecuteNonQuery();
    });

    public T Get<T>(string key, T fallback)
    {
        var raw = GetRaw(key);
        if (string.IsNullOrEmpty(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(raw) ?? fallback;
        }
        catch (JsonException)
        {
            // Valor corrompido nao deve impedir o app de abrir.
            return fallback;
        }
    }

    public void Set<T>(string key, T value)
        => SetRaw(key, JsonSerializer.Serialize(value));

    public bool GetBool(string key, bool fallback)
    {
        var raw = GetRaw(key);
        return raw switch
        {
            null or "" => fallback,
            "true" => true,
            "false" => false,
            _ => bool.TryParse(raw, out var parsed) ? parsed : fallback,
        };
    }

    public void SetBool(string key, bool value)
        => SetRaw(key, value ? "true" : "false");

    public int GetInt(string key, int fallback)
    {
        var raw = GetRaw(key);
        return raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public void SetInt(string key, int value)
        => SetRaw(key, value.ToString(CultureInfo.InvariantCulture));
}
