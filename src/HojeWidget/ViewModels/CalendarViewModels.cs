using System.Collections.ObjectModel;
using HojeWidget.Domain;
using HojeWidget.Models;

namespace HojeWidget.ViewModels;

/// <summary>
/// Um dia do calendário mensal. Distingue item planejado de prazo, sem virar agenda
/// por hora: o foco é o dia.
/// </summary>
public sealed class CalendarDayViewModel : ObservableObject
{
    private bool _isSelected;

    public CalendarDayViewModel(CalendarDay day, DateOnly today)
    {
        Date = day.Date;
        DayNumber = day.Date.Day.ToString();
        IsCurrentMonth = day.IsCurrentMonth;
        IsToday = day.Date == today;
        PlannedCount = day.Planned.Count;
        DueCount = day.Due.Count;
        ExamCount = day.Exams.Count;
    }

    public DateOnly Date { get; }
    public string DayNumber { get; }
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }
    public int PlannedCount { get; }
    public int DueCount { get; }

    /// <summary>V0.6 — provas marcadas para este dia.</summary>
    public int ExamCount { get; }

    /// <summary>Marcador de item planejado.</summary>
    public bool HasPlanned => PlannedCount > 0;

    /// <summary>Marcador de prazo.</summary>
    public bool HasDue => DueCount > 0;

    /// <summary>Marcador de prova. Distinto do de prazo: prova não é tarefa.</summary>
    public bool HasExam => ExamCount > 0;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>Uma linha de "Próximos prazos": proximidade + título.</summary>
public sealed class DeadlineViewModel
{
    public DeadlineViewModel(UpcomingDeadline deadline, DateOnly today)
    {
        Id = deadline.Task.Id;
        Title = deadline.Task.Title;
        Proximity = TaskLabels.DeadlineProximity(deadline.DueDate, today);
        IsOverdue = deadline.IsOverdue;
        DateText = LocalDate.ToShortText(deadline.DueDate);
    }

    public string Id { get; }
    public string Title { get; }
    public string Proximity { get; }
    public string DateText { get; }
    public bool IsOverdue { get; }
}

/// <summary>
/// V0.7.1 — uma prova na agenda. Não é tarefa: não tem caixa para marcar, não se
/// conclui e não some quando o dia passa. É só um lembrete de que ela existe.
/// </summary>
public sealed class ExamReminderViewModel
{
    public ExamReminderViewModel(Exam exam, DateOnly today)
    {
        Id = exam.Id;
        Title = exam.DisplayName;
        DateText = LocalDate.ToShortText(exam.ExamDate);
        Proximity = TaskLabels.ExamProximity(exam.ExamDate, today);
        IsPast = exam.ExamDate < today;
    }

    public string Id { get; }
    public string Title { get; }
    public string DateText { get; }
    public string Proximity { get; }
    public bool IsPast { get; }
}

/// <summary>Itens de um dia selecionado no calendário.</summary>
public sealed class SelectedDayViewModel : ObservableObject
{
    private string _title = string.Empty;
    private bool _isEmpty = true;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        set => SetProperty(ref _isEmpty, value);
    }

    public ObservableCollection<TaskItemViewModel> Planned { get; } = new();

    public ObservableCollection<DeadlineViewModel> Due { get; } = new();

    /// <summary>V0.6 — provas deste dia. Lembrete, não tarefa.</summary>
    public ObservableCollection<ExamReminderViewModel> Exams { get; } = new();
}
