using HojeWidget.Models;

namespace HojeWidget.Domain;

/// <summary>Resultado ja agrupado e ordenado, pronto para a interface.</summary>
public sealed class TaskListSnapshot
{
    public required IReadOnlyList<TaskItem> Pending { get; init; }
    public required IReadOnlyList<TaskItem> CompletedToday { get; init; }
    public required string LocalDate { get; init; }
    public required DateOnly Today { get; init; }

    public int PendingCount => Pending.Count;

    public string PendingCountText => PendingCounter.Format(PendingCount);

    public ListState State =>
        Pending.Count > 0 ? ListState.Normal
        : CompletedToday.Count > 0 ? ListState.AllDone
        : ListState.Empty;

    /// <summary>Ordem de exibicao: pendentes por ordem manual, depois concluidas de hoje por conclusao.</summary>
    public IReadOnlyList<TaskItem> Display
    {
        get
        {
            var all = new List<TaskItem>(Pending.Count + CompletedToday.Count);
            all.AddRange(Pending);
            all.AddRange(CompletedToday);
            return all;
        }
    }
}
