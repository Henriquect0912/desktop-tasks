namespace HojeWidget.Domain;

/// <summary>Validacao e normalizacao do texto da tarefa.</summary>
public static class TaskTitle
{
    /// <summary>Limite defensivo de caracteres.</summary>
    public const int MaxLength = 500;

    /// <summary>
    /// Remove espacos das pontas, colapsa quebras de linha e aplica o limite.
    /// Retorna false quando o texto e vazio depois da normalizacao.
    /// </summary>
    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var collapsed = raw.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        var trimmed = collapsed.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        normalized = trimmed.Length > MaxLength ? trimmed[..MaxLength].TrimEnd() : trimmed;
        return normalized.Length > 0;
    }
}
