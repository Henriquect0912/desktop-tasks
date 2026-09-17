namespace HojeWidget.Domain;

/// <summary>
/// Textos auxiliares que aparecem abaixo do título da tarefa.
/// Descrevem atraso de planejamento e prazo sem qualquer tom de punição.
/// </summary>
public static class TaskLabels
{
    /// <summary>
    /// V0.2 — atraso no planejamento, não prazo vencido.
    /// Retorna null quando a tarefa está planejada para hoje ou para o futuro.
    /// </summary>
    public static string? PlanningDelay(DateOnly? planned, DateOnly today)
    {
        if (planned is not { } date || date >= today)
        {
            return null;
        }

        var days = today.DayNumber - date.DayNumber;
        return days == 1
            ? "planejada para ontem"
            : $"planejada para {LocalDate.ToShortText(date)}";
    }

    /// <summary>
    /// V0.3 — prazo. Data limite real, diferente da data planejada.
    /// Retorna null quando a tarefa não tem prazo.
    /// </summary>
    public static string? Deadline(DateOnly? due, DateOnly today)
    {
        if (due is not { } date)
        {
            return null;
        }

        var days = date.DayNumber - today.DayNumber;

        return days switch
        {
            < -1 => $"vencido há {-days} dias",
            -1 => "vencido ontem",
            0 => "prazo hoje",
            1 => "prazo amanhã",
            _ => $"prazo em {days} dias",
        };
    }

    /// <summary>
    /// Linha discreta completa: atraso de planejamento e prazo, quando houver os dois.
    /// Exemplo: "planejada para ontem · prazo amanhã".
    /// </summary>
    public static string? Subtitle(DateOnly? planned, DateOnly? due, DateOnly today)
    {
        var parts = new[] { PlanningDelay(planned, today), Deadline(due, today) }
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>
    /// V0.6 — proximidade da prova. Ocupa o lugar do prazo na linha de uma
    /// sessão de estudo, porque para ela a prova é o prazo.
    /// </summary>
    public static string ExamProximity(DateOnly examDate, DateOnly today)
    {
        var days = examDate.DayNumber - today.DayNumber;

        return days switch
        {
            < 0 => "prova já passou",
            0 => "prova hoje",
            1 => "prova amanhã",
            _ => $"prova em {days} dias",
        };
    }

    /// <summary>
    /// Linha discreta de qualquer item do dia: tarefa, subtarefa de projeto,
    /// ocorrência de rotina ou sessão de estudo.
    ///
    /// A ordem é sempre a mesma — de onde vem, quanto dura, quando foi planejado,
    /// para quando é — para o olho aprender a ler sempre no mesmo lugar.
    /// </summary>
    public static string? For(DayEntry entry, DateOnly today)
    {
        if (entry.IsCompleted)
        {
            return null;
        }

        var parts = new List<string?>(4);

        if (entry.ParentTitle is { Length: > 0 } parent)
        {
            parts.Add(entry.ParentProgress.HasItems
                ? $"{parent} · {entry.ParentProgress.Completed} de {entry.ParentProgress.Total}"
                : parent);
        }
        else if (entry.ExamName is { Length: > 0 } exam && entry.Kind == EntryKind.StudySession)
        {
            parts.Add(exam);
        }

        parts.Add(Estimate.Format(entry.EstimatedMinutes));
        parts.Add(PlanningDelay(entry.PlannedDate, today));

        parts.Add(entry.Kind == EntryKind.StudySession && entry.ExamDate is { } examDate
            ? ExamProximity(examDate, today)
            : Deadline(entry.DueDate, today));

        var used = parts.Where(p => !string.IsNullOrEmpty(p)).ToArray();
        return used.Length == 0 ? null : string.Join(" · ", used);
    }

    /// <summary>Coluna de proximidade da lista "Próximos prazos".</summary>
    public static string DeadlineProximity(DateOnly due, DateOnly today)
    {
        var days = due.DayNumber - today.DayNumber;

        return days switch
        {
            < 0 => "vencido",
            0 => "hoje",
            1 => "amanhã",
            _ => $"{days} dias",
        };
    }
}
