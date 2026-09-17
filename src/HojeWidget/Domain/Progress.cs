namespace HojeWidget.Domain;

/// <summary>
/// V0.5 — progresso por quantidade concluída. Nada de ponderação por duração:
/// a regra é simples e verificável de olho, "concluídas / total".
/// </summary>
public readonly record struct Progress(int Completed, int Total)
{
    public static readonly Progress Empty = new(0, 0);

    public bool HasItems => Total > 0;

    /// <summary>Percentual inteiro de 0 a 100. Sem itens, zero.</summary>
    public int Percent => Total == 0 ? 0 : (int)Math.Round(100.0 * Completed / Total, MidpointRounding.AwayFromZero);

    /// <summary>Fração de 0 a 1, para a barra na interface.</summary>
    public double Fraction => Total == 0 ? 0 : (double)Completed / Total;

    public bool IsAllDone => Total > 0 && Completed == Total;

    /// <summary>"3 de 7 concluídas".</summary>
    public string Text => Total switch
    {
        0 => "Nenhuma etapa ainda",
        1 => Completed == 1 ? "1 de 1 concluída" : "0 de 1 concluída",
        _ => $"{Completed} de {Total} concluídas",
    };

    /// <summary>"████░░░░░░ 43%" — a mesma barra em texto, usada em log e testes.</summary>
    public string Bar(int width = 10)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var filled = Total == 0 ? 0 : (int)Math.Round(width * Fraction, MidpointRounding.ToZero);
        filled = Math.Clamp(filled, 0, width);
        return new string('█', filled) + new string('░', width - filled) + $" {Percent}%";
    }

    public static Progress Of<T>(IEnumerable<T> items, Func<T, bool> isCompleted)
    {
        var total = 0;
        var completed = 0;

        foreach (var item in items)
        {
            total++;
            if (isCompleted(item))
            {
                completed++;
            }
        }

        return new Progress(completed, total);
    }
}

/// <summary>
/// Duração estimada em minutos, sempre em português curto.
/// Duração desconhecida é um estado legítimo — melhor do que inventar precisão.
/// </summary>
public static class Estimate
{
    /// <summary>90 → "1h30", 80 → "1h20", 60 → "1h", 45 → "45 min".</summary>
    public static string Format(int minutes)
    {
        if (minutes <= 0)
        {
            return "0 min";
        }

        var hours = minutes / 60;
        var rest = minutes % 60;

        return hours switch
        {
            0 => $"{rest} min",
            _ when rest == 0 => $"{hours}h",
            _ => $"{hours}h{rest:00}",
        };
    }

    /// <summary>Null vira null: quem não sabe a duração não finge saber.</summary>
    public static string? Format(int? minutes) => minutes is { } value ? Format(value) : null;

    /// <summary>Texto de carga do dia: "3h50 estimadas".</summary>
    public static string Load(int minutes) => $"{Format(minutes)} estimadas";

    /// <summary>
    /// Lê "1h30", "90", "90 min", "1:30". Retorna null quando não dá para entender —
    /// nesse caso a duração fica desconhecida em vez de virar um número inventado.
    /// </summary>
    public static int? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("min", string.Empty);

        var separator = value.IndexOfAny(new[] { 'h', ':' });
        if (separator >= 0)
        {
            var hoursText = value[..separator];
            var minutesText = value[(separator + 1)..];

            if (!int.TryParse(hoursText, out var hours))
            {
                return null;
            }

            var minutes = 0;
            if (minutesText.Length > 0 && !int.TryParse(minutesText, out minutes))
            {
                return null;
            }

            var total = hours * 60 + minutes;
            return total > 0 ? total : null;
        }

        return int.TryParse(value, out var plain) && plain > 0 ? plain : null;
    }
}
