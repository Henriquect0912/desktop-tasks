using HojeWidget.Domain;
using HojeWidget.Models;

namespace HojeWidget.ViewModels;

/// <summary>Uma rotina na seção Rotinas: o nome e a regra em texto.</summary>
public sealed class RoutineListItemViewModel : ObservableObject
{
    private bool _isMenuOpen;

    public RoutineListItemViewModel(Routine routine)
    {
        Id = routine.Id;
        Title = routine.Title;
        Description = RecurrenceCalculator.Describe(routine);
        RecurrenceType = routine.RecurrenceType;
        DaysOfWeek = routine.DaysOfWeek;
        IntervalDays = routine.IntervalDays;
        DayOfMonth = routine.DayOfMonth;
    }

    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    public RecurrenceType RecurrenceType { get; }
    public int DaysOfWeek { get; }
    public int? IntervalDays { get; }
    public int? DayOfMonth { get; }

    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set => SetProperty(ref _isMenuOpen, value);
    }
}
