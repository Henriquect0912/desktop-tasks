using System.Collections.ObjectModel;
using HojeWidget.Domain;

namespace HojeWidget.ViewModels;

/// <summary>Uma coluna da visão semanal. Aceita tarefas arrastadas de outros dias.</summary>
public sealed class DayColumnViewModel : ObservableObject
{
    public DayColumnViewModel(DateOnly date, DateOnly today)
    {
        Date = date;
        IsToday = date == today;
        DayName = LocalDate.DayOfWeekShortName(date);
        DayNumber = LocalDate.ToShortText(date);
    }

    public DateOnly Date { get; }

    public bool IsToday { get; }

    /// <summary>seg, ter, qua…</summary>
    public string DayName { get; }

    /// <summary>08/09</summary>
    public string DayNumber { get; }

    public ObservableCollection<TaskItemViewModel> Items { get; } = new();

    public bool IsEmpty => Items.Count == 0;

    public void RaiseEmptyChanged() => OnPropertyChanged(nameof(IsEmpty));
}
