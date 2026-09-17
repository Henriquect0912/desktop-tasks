using HojeWidget.Models;

namespace HojeWidget.Services.Database;

/// <summary>Um layout salvo por assinatura de configuracao de monitores.</summary>
public sealed class DisplayLayoutRepository
{
    private readonly AppDatabase _database;

    public DisplayLayoutRepository(AppDatabase database) => _database = database;

    public StoredLayout? Get(string signature) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT display_signature, monitor_device, x_dip, y_dip, width_dip, height_dip, updated_at_utc
              FROM display_layouts
             WHERE display_signature = $signature;
            """;
        command.Parameters.AddWithValue("$signature", signature);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new StoredLayout(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetDouble(2),
            reader.GetDouble(3),
            reader.GetDouble(4),
            reader.GetDouble(5),
            Timestamps.FromStorage(reader.GetString(6)));
    });

    public void Save(StoredLayout layout) => _database.Execute(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO display_layouts (display_signature, monitor_device, x_dip, y_dip, width_dip, height_dip, updated_at_utc)
            VALUES ($signature, $device, $x, $y, $width, $height, $updated)
            ON CONFLICT(display_signature) DO UPDATE SET
                monitor_device = excluded.monitor_device,
                x_dip = excluded.x_dip,
                y_dip = excluded.y_dip,
                width_dip = excluded.width_dip,
                height_dip = excluded.height_dip,
                updated_at_utc = excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$signature", layout.DisplaySignature);
        command.Parameters.AddWithValue("$device", layout.MonitorDevice);
        command.Parameters.AddWithValue("$x", layout.XDip);
        command.Parameters.AddWithValue("$y", layout.YDip);
        command.Parameters.AddWithValue("$width", layout.WidthDip);
        command.Parameters.AddWithValue("$height", layout.HeightDip);
        command.Parameters.AddWithValue("$updated", Timestamps.ToStorage(layout.UpdatedAtUtc));
        command.ExecuteNonQuery();
    });
}
