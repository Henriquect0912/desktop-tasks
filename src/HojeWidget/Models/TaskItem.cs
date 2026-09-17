namespace HojeWidget.Models;

/// <summary>Tarefa persistida.</summary>
public sealed class TaskItem
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public bool IsCompleted { get; set; }

    /// <summary>Data civil local (YYYY-MM-DD) em que a tarefa foi concluida, ou null.</summary>
    public string? CompletedLocalDate { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>
    /// V0.2 — dia em que o usuario pretende executar a tarefa.
    /// Null significa "Algum dia": a tarefa existe, mas nao foi planejada.
    /// </summary>
    public DateOnly? PlannedDate { get; set; }

    /// <summary>
    /// V0.3 — data limite real. Independente de <see cref="PlannedDate"/>:
    /// planejar e prazo sao coisas diferentes.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// V0.5 — projeto ou entrega a que esta tarefa pertence. Null é uma tarefa solta.
    /// O pai é sempre um <see cref="Container"/>, nunca outra tarefa: é isso que
    /// garante estruturalmente que só exista um nível de subtarefa.
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// V0.5 — duração estimada em minutos. Null significa duração desconhecida;
    /// o planejamento prefere admitir que não sabe a inventar precisão.
    /// </summary>
    public int? EstimatedMinutes { get; set; }

    /// <summary>
    /// V0.5 — se a tarefa pode ser dividida em sessões ao longo de vários dias.
    /// "Escrever o relatório" pode; "levar o carro à oficina" não.
    /// </summary>
    public bool CanSplit { get; set; } = true;

    /// <summary>V0.7 — prioridade manual.</summary>
    public Priority Priority { get; set; } = Priority.Normal;

    /// <summary>
    /// V0.7 — o usuário escolheu este dia à mão (arrastando ou pelo formulário).
    /// O motor respeita a decisão e não a desfaz por conta própria.
    /// </summary>
    public bool ManualPlanning { get; set; }

    /// <summary>Posicao manual global. Concluidas conservam o valor para voltar ao lugar ao desmarcar.</summary>
    public long SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public TaskItem Clone() => new()
    {
        Id = Id,
        Title = Title,
        IsCompleted = IsCompleted,
        CompletedLocalDate = CompletedLocalDate,
        CompletedAtUtc = CompletedAtUtc,
        PlannedDate = PlannedDate,
        DueDate = DueDate,
        ParentId = ParentId,
        EstimatedMinutes = EstimatedMinutes,
        CanSplit = CanSplit,
        Priority = Priority,
        ManualPlanning = ManualPlanning,
        SortOrder = SortOrder,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc,
    };
}
