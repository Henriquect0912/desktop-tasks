using System.Collections.ObjectModel;
using HojeWidget.Domain;
using HojeWidget.Domain.Planning;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.System;

namespace HojeWidget.ViewModels;

/// <summary>Seções do aplicativo principal.</summary>
public enum AppSection
{
    Today = 0,
    Week = 1,
    Someday = 2,
    Calendar = 3,
    Deadlines = 4,
    Routines = 5,

    /// <summary>V0.5</summary>
    Projects = 6,

    /// <summary>V0.5</summary>
    Deliveries = 7,

    /// <summary>V0.6</summary>
    Exams = 8,

    /// <summary>V0.7</summary>
    DailyPlan = 9,

    /// <summary>V0.7</summary>
    Capacity = 10,
}

/// <summary>Opções de "Quando pretende fazer?" no formulário de tarefa.</summary>
public enum PlannedWhen
{
    Today = 0,
    Tomorrow = 1,
    PickDate = 2,
    Someday = 3,
}

/// <summary>Um destino possível ao mover uma tarefa para um projeto ou entrega.</summary>
public sealed record MoveTargetViewModel(string? Id, string Title, string KindLabel);

/// <summary>
/// Estado do aplicativo principal: onde o usuário organiza e planeja.
/// O widget continua sendo só a execução do dia.
/// </summary>
public sealed class MainAppViewModel : ObservableObject
{
    private readonly TaskListService _tasks;
    private readonly RoutineService _routines;
    private readonly ContainerService _containers;
    private readonly ExamService _exams;
    private readonly ExamPlanningService _examPlanning;
    private readonly PlanningService _planning;
    private readonly IClock _clock;

    private AppSection _selectedSection = AppSection.Today;
    private DateOnly _weekStart;
    private DateOnly _monthAnchor;
    private DateOnly? _selectedDate;
    private string _weekLabel = string.Empty;
    private string _calendarLabel = string.Empty;

    private bool _isTaskFormOpen;
    private bool _isEditing;
    private string? _editingId;
    private string _formTitle = string.Empty;
    private string _formHeader = "Nova tarefa";
    private PlannedWhen _formWhen = PlannedWhen.Today;
    private DateTime? _formPlannedDate;
    private DateTime? _formDueDate;
    private string _formMinutes = string.Empty;
    private bool _formCanSplit = true;
    private Priority _formPriority = Priority.Normal;

    private TaskItemViewModel? _deleteCandidate;
    private TaskItemViewModel? _moveCandidate;
    private bool _isMovePickerOpen;

    private bool _isRoutineFormOpen;
    private bool _isEditingRoutine;
    private string? _editingRoutineId;
    private string _routineFormHeader = "Nova rotina";
    private string _routineTitle = string.Empty;
    private RecurrenceType _routineType = RecurrenceType.Daily;
    private string _routineIntervalDays = "15";
    private string _routineDayOfMonth = "10";
    private readonly bool[] _routineDays = new bool[7];
    private RoutineListItemViewModel? _routineDeleteCandidate;

    public MainAppViewModel(
        TaskListService tasks,
        RoutineService routines,
        ContainerService containers,
        ExamService exams,
        ExamPlanningService examPlanning,
        PlanningService planning,
        IClock clock)
    {
        _tasks = tasks;
        _routines = routines;
        _containers = containers;
        _exams = exams;
        _examPlanning = examPlanning;
        _planning = planning;
        _clock = clock;
        _weekStart = LocalDate.StartOfWeek(clock.Today);
        _monthAnchor = new DateOnly(clock.Today.Year, clock.Today.Month, 1);

        Proposal = new DistributionProposalViewModel();

        Projects = new ContainerSectionViewModel(
            ContainerKind.Project, containers, tasks, clock, Proposal, () => planning.Capacity);
        Deliveries = new ContainerSectionViewModel(
            ContainerKind.Delivery, containers, tasks, clock, Proposal, () => planning.Capacity);
        Exams = new ExamSectionViewModel(exams, examPlanning, clock);
        DailyPlan = new DailyPlanViewModel(planning, clock);
        Capacity = new CapacityViewModel(planning.Capacity, clock);

        Projects.Changed += OnSectionChanged;
        Deliveries.Changed += OnSectionChanged;
        Exams.Changed += OnSectionChanged;
        DailyPlan.Changed += OnSectionChanged;
        Capacity.Changed += OnSectionChanged;

        OpenNewTaskCommand = new RelayCommand(OpenNewTask);
        CancelTaskFormCommand = new RelayCommand(CancelTaskForm);
        CommitTaskFormCommand = new RelayCommand(CommitTaskForm);
        ClearDueDateCommand = new RelayCommand(() => FormDueDate = null);
        CancelMoveCommand = new RelayCommand(() => IsMovePickerOpen = false);

        PreviousWeekCommand = new RelayCommand(() => ShiftWeek(-7));
        NextWeekCommand = new RelayCommand(() => ShiftWeek(7));
        CurrentWeekCommand = new RelayCommand(() => { _weekStart = LocalDate.StartOfWeek(_clock.Today); Reload(); });

        PreviousMonthCommand = new RelayCommand(() => ShiftMonth(-1));
        NextMonthCommand = new RelayCommand(() => ShiftMonth(1));
        CurrentMonthCommand = new RelayCommand(() =>
        {
            _monthAnchor = new DateOnly(_clock.Today.Year, _clock.Today.Month, 1);
            _selectedDate = _clock.Today;
            Reload();
        });

        ConfirmDeleteCommand = new RelayCommand(ConfirmDelete);
        CancelDeleteCommand = new RelayCommand(() => DeleteCandidate = null);

        OpenNewRoutineCommand = new RelayCommand(OpenNewRoutine);
        CancelRoutineFormCommand = new RelayCommand(() => IsRoutineFormOpen = false);
        CommitRoutineFormCommand = new RelayCommand(CommitRoutineForm);
        ConfirmRoutineDeleteCommand = new RelayCommand(ConfirmRoutineDelete);
        CancelRoutineDeleteCommand = new RelayCommand(() => RoutineDeleteCandidate = null);

        Reload();
    }

    public ObservableCollection<TaskItemViewModel> TodayItems { get; } = new();
    public ObservableCollection<TaskItemViewModel> SomedayItems { get; } = new();
    public ObservableCollection<DayColumnViewModel> WeekDays { get; } = new();
    public ObservableCollection<CalendarDayViewModel> CalendarDays { get; } = new();
    public ObservableCollection<DeadlineViewModel> UpcomingDeadlines { get; } = new();

    /// <summary>V0.7.1 — provas na agenda, como lembrete ao lado dos prazos.</summary>
    public ObservableCollection<ExamReminderViewModel> UpcomingExams { get; } = new();
    public ObservableCollection<MoveTargetViewModel> MoveTargets { get; } = new();
    public SelectedDayViewModel SelectedDay { get; } = new();

    /// <summary>V0.5 — a tela de plano sugerido, compartilhada por projetos, entregas e provas.</summary>
    public DistributionProposalViewModel Proposal { get; }

    public ContainerSectionViewModel Projects { get; }
    public ContainerSectionViewModel Deliveries { get; }
    public ExamSectionViewModel Exams { get; }
    public DailyPlanViewModel DailyPlan { get; }
    public CapacityViewModel Capacity { get; }

    public RelayCommand OpenNewTaskCommand { get; }
    public RelayCommand CancelTaskFormCommand { get; }
    public RelayCommand CommitTaskFormCommand { get; }
    public RelayCommand ClearDueDateCommand { get; }
    public RelayCommand CancelMoveCommand { get; }
    public RelayCommand PreviousWeekCommand { get; }
    public RelayCommand NextWeekCommand { get; }
    public RelayCommand CurrentWeekCommand { get; }
    public RelayCommand PreviousMonthCommand { get; }
    public RelayCommand NextMonthCommand { get; }
    public RelayCommand CurrentMonthCommand { get; }
    public RelayCommand ConfirmDeleteCommand { get; }
    public RelayCommand CancelDeleteCommand { get; }

    /// <summary>Disparado quando algo muda e o widget precisa se atualizar.</summary>
    public event Action? TasksChanged;

    /// <summary>Disparado quando o campo de título do formulário deve receber foco.</summary>
    public event Action? NewTaskFocusRequested;

    // ---------- Navegação ----------

    public AppSection SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                OnPropertyChanged(nameof(IsTodayVisible));
                OnPropertyChanged(nameof(IsWeekVisible));
                OnPropertyChanged(nameof(IsSomedayVisible));
                OnPropertyChanged(nameof(IsCalendarVisible));
                OnPropertyChanged(nameof(IsDeadlinesVisible));
                OnPropertyChanged(nameof(IsRoutinesVisible));
                OnPropertyChanged(nameof(IsProjectsVisible));
                OnPropertyChanged(nameof(IsDeliveriesVisible));
                OnPropertyChanged(nameof(IsExamsVisible));
                OnPropertyChanged(nameof(IsDailyPlanVisible));
                OnPropertyChanged(nameof(IsCapacityVisible));
            }
        }
    }

    public bool IsTodayVisible => _selectedSection == AppSection.Today;
    public bool IsWeekVisible => _selectedSection == AppSection.Week;
    public bool IsSomedayVisible => _selectedSection == AppSection.Someday;
    public bool IsCalendarVisible => _selectedSection == AppSection.Calendar;
    public bool IsDeadlinesVisible => _selectedSection == AppSection.Deadlines;
    public bool IsRoutinesVisible => _selectedSection == AppSection.Routines;
    public bool IsProjectsVisible => _selectedSection == AppSection.Projects;
    public bool IsDeliveriesVisible => _selectedSection == AppSection.Deliveries;
    public bool IsExamsVisible => _selectedSection == AppSection.Exams;
    public bool IsDailyPlanVisible => _selectedSection == AppSection.DailyPlan;
    public bool IsCapacityVisible => _selectedSection == AppSection.Capacity;

    public string WeekLabel
    {
        get => _weekLabel;
        private set => SetProperty(ref _weekLabel, value);
    }

    public string CalendarLabel
    {
        get => _calendarLabel;
        private set => SetProperty(ref _calendarLabel, value);
    }

    public bool IsTodayEmpty => TodayItems.Count == 0;
    public bool IsSomedayEmpty => SomedayItems.Count == 0;
    public bool IsDeadlinesEmpty => UpcomingDeadlines.Count == 0 && UpcomingExams.Count == 0;

    public bool HasUpcomingExams => UpcomingExams.Count > 0;

    public string TodayEmptyText => "Nada planejado para hoje.";

    // ---------- Formulário de tarefa ----------

    public bool IsTaskFormOpen
    {
        get => _isTaskFormOpen;
        private set => SetProperty(ref _isTaskFormOpen, value);
    }

    public string FormHeader
    {
        get => _formHeader;
        private set => SetProperty(ref _formHeader, value);
    }

    public string FormTitle
    {
        get => _formTitle;
        set => SetProperty(ref _formTitle, value);
    }

    public PlannedWhen FormWhen
    {
        get => _formWhen;
        set
        {
            if (SetProperty(ref _formWhen, value))
            {
                OnPropertyChanged(nameof(IsPlannedDatePickerVisible));
            }
        }
    }

    public bool IsPlannedDatePickerVisible => _formWhen == PlannedWhen.PickDate;

    public DateTime? FormPlannedDate
    {
        get => _formPlannedDate;
        set => SetProperty(ref _formPlannedDate, value);
    }

    /// <summary>Prazo, sempre opcional e independente da data planejada.</summary>
    public DateTime? FormDueDate
    {
        get => _formDueDate;
        set => SetProperty(ref _formDueDate, value);
    }

    /// <summary>V0.5 — duração estimada. Vazio significa desconhecida, e tudo bem.</summary>
    public string FormMinutes
    {
        get => _formMinutes;
        set => SetProperty(ref _formMinutes, value);
    }

    /// <summary>V0.5 — pode ser dividida em sessões ao longo de vários dias?</summary>
    public bool FormCanSplit
    {
        get => _formCanSplit;
        set => SetProperty(ref _formCanSplit, value);
    }

    /// <summary>V0.7 — prioridade manual.</summary>
    public Priority FormPriority
    {
        get => _formPriority;
        set => SetProperty(ref _formPriority, value);
    }

    public TaskItemViewModel? DeleteCandidate
    {
        get => _deleteCandidate;
        private set
        {
            if (SetProperty(ref _deleteCandidate, value))
            {
                OnPropertyChanged(nameof(IsDeleteConfirmationVisible));
            }
        }
    }

    public bool IsDeleteConfirmationVisible => _deleteCandidate is not null;

    /// <summary>V0.5 — escolha do projeto ou entrega ao mover uma tarefa existente.</summary>
    public bool IsMovePickerOpen
    {
        get => _isMovePickerOpen;
        private set => SetProperty(ref _isMovePickerOpen, value);
    }

    public void OpenNewTask()
    {
        _isEditing = false;
        _editingId = null;
        FormHeader = "Nova tarefa";
        FormTitle = string.Empty;
        FormWhen = _selectedSection == AppSection.Someday ? PlannedWhen.Someday : PlannedWhen.Today;
        FormPlannedDate = _clock.LocalNow.Date;
        FormDueDate = null;
        FormMinutes = string.Empty;
        FormCanSplit = true;
        FormPriority = Priority.Normal;
        IsTaskFormOpen = true;
        NewTaskFocusRequested?.Invoke();
    }

    /// <summary>Editar abre o mesmo formulário, com tudo o que a tarefa já tem.</summary>
    public void BeginEdit(TaskItemViewModel item)
    {
        foreach (var other in AllItems())
        {
            other.IsMenuOpen = false;
        }

        _isEditing = true;
        _editingId = item.Id;
        FormHeader = "Editar tarefa";
        FormTitle = item.Title;
        FormWhen = item.PlannedDate is null ? PlannedWhen.Someday : PlannedWhen.PickDate;
        FormPlannedDate = item.PlannedDate?.ToDateTime(TimeOnly.MinValue) ?? _clock.LocalNow.Date;
        FormDueDate = item.DueDate?.ToDateTime(TimeOnly.MinValue);
        FormMinutes = Estimate.Format(item.EstimatedMinutes) ?? string.Empty;
        FormCanSplit = item.CanSplit;
        FormPriority = item.Priority;
        IsTaskFormOpen = true;
        NewTaskFocusRequested?.Invoke();
    }

    public void CancelTaskForm()
    {
        IsTaskFormOpen = false;
        FormTitle = string.Empty;
        _editingId = null;
    }

    public void CommitTaskForm()
    {
        var planned = ResolvePlannedDate();
        var due = FormDueDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;
        var minutes = Estimate.TryParse(FormMinutes);

        var ok = _isEditing && _editingId is not null
            ? _tasks.Update(_editingId, FormTitle, planned, due, minutes, FormCanSplit, FormPriority)
            : _tasks.Add(FormTitle, planned, due, minutes, FormCanSplit, FormPriority) is not null;

        if (!ok)
        {
            // Texto vazio não salva; o formulário continua aberto.
            NewTaskFocusRequested?.Invoke();
            return;
        }

        IsTaskFormOpen = false;
        FormTitle = string.Empty;
        _editingId = null;
        Reload();
        TasksChanged?.Invoke();
    }

    private DateOnly? ResolvePlannedDate() => _formWhen switch
    {
        PlannedWhen.Today => _clock.Today,
        PlannedWhen.Tomorrow => _clock.Today.AddDays(1),
        PlannedWhen.PickDate => _formPlannedDate is { } picked ? DateOnly.FromDateTime(picked) : _clock.Today,
        _ => null,
    };

    // ---------- Ações sobre tarefas ----------

    public void ToggleCompleted(TaskItemViewModel item)
    {
        switch (item.Kind)
        {
            case EntryKind.Routine:
                _routines.SetOccurrenceCompleted(item.Id, item.IsCompleted);
                break;
            case EntryKind.StudySession:
                _exams.SetSessionCompleted(item.Id, item.IsCompleted);
                break;
            default:
                _tasks.SetCompleted(item.Id, item.IsCompleted);
                break;
        }

        Reload();
        TasksChanged?.Invoke();
    }

    public void RequestDelete(TaskItemViewModel item)
    {
        item.IsMenuOpen = false;
        DeleteCandidate = item;
    }

    public void ConfirmDelete()
    {
        if (DeleteCandidate is null)
        {
            return;
        }

        _tasks.Delete(DeleteCandidate.Id);
        DeleteCandidate = null;
        Reload();
        TasksChanged?.Invoke();
    }

    /// <summary>
    /// V0.5 — abre a escolha de projeto ou entrega para uma tarefa já existente.
    /// Nada do que a tarefa tem se perde ao mudar de dono.
    /// </summary>
    public void RequestMove(TaskItemViewModel item)
    {
        item.IsMenuOpen = false;
        _moveCandidate = item;

        MoveTargets.Clear();
        MoveTargets.Add(new MoveTargetViewModel(null, "Tarefa solta", "Sem projeto"));

        foreach (var container in _containers.GetAll().Where(c => !c.IsCompleted))
        {
            MoveTargets.Add(new MoveTargetViewModel(container.Id, container.Title, container.KindLabel));
        }

        IsMovePickerOpen = true;
    }

    public void MoveTo(MoveTargetViewModel target)
    {
        if (_moveCandidate is not null)
        {
            _containers.MoveTaskInto(_moveCandidate.Id, target.Id);
        }

        _moveCandidate = null;
        IsMovePickerOpen = false;
        Reload();
        TasksChanged?.Invoke();
    }

    /// <summary>Arrastar entre dias: muda a data planejada e persiste na hora.</summary>
    public void MoveToDay(string taskId, DateOnly date)
    {
        if (_tasks.SetPlannedDate(taskId, date))
        {
            Reload();
            TasksChanged?.Invoke();
        }
    }

    /// <summary>Tira a tarefa do planejamento e devolve para "Algum dia".</summary>
    public void MoveToSomeday(string taskId)
    {
        if (_tasks.SetPlannedDate(taskId, null))
        {
            Reload();
            TasksChanged?.Invoke();
        }
    }

    public void SelectDay(DateOnly date)
    {
        _selectedDate = date;
        RefreshSelectedDay();
    }

    // ---------- Rotinas ----------

    public ObservableCollection<RoutineListItemViewModel> Routines { get; } = new();

    public RelayCommand OpenNewRoutineCommand { get; }
    public RelayCommand CancelRoutineFormCommand { get; }
    public RelayCommand CommitRoutineFormCommand { get; }
    public RelayCommand ConfirmRoutineDeleteCommand { get; }
    public RelayCommand CancelRoutineDeleteCommand { get; }

    public bool IsRoutinesEmpty => Routines.Count == 0;

    public bool IsRoutineFormOpen
    {
        get => _isRoutineFormOpen;
        private set => SetProperty(ref _isRoutineFormOpen, value);
    }

    public string RoutineFormHeader
    {
        get => _routineFormHeader;
        private set => SetProperty(ref _routineFormHeader, value);
    }

    public string RoutineTitle
    {
        get => _routineTitle;
        set => SetProperty(ref _routineTitle, value);
    }

    public RecurrenceType RoutineType
    {
        get => _routineType;
        set
        {
            if (SetProperty(ref _routineType, value))
            {
                OnPropertyChanged(nameof(IsDaysOfWeekVisible));
                OnPropertyChanged(nameof(IsIntervalVisible));
                OnPropertyChanged(nameof(IsDayOfMonthVisible));
            }
        }
    }

    public bool IsDaysOfWeekVisible => _routineType == RecurrenceType.DaysOfWeek;
    public bool IsIntervalVisible => _routineType == RecurrenceType.EveryXDays;
    public bool IsDayOfMonthVisible => _routineType == RecurrenceType.Monthly;

    public string RoutineIntervalDays
    {
        get => _routineIntervalDays;
        set => SetProperty(ref _routineIntervalDays, value);
    }

    public string RoutineDayOfMonth
    {
        get => _routineDayOfMonth;
        set => SetProperty(ref _routineDayOfMonth, value);
    }

    public bool RoutineMonday { get => _routineDays[0]; set => SetDay(0, value); }
    public bool RoutineTuesday { get => _routineDays[1]; set => SetDay(1, value); }
    public bool RoutineWednesday { get => _routineDays[2]; set => SetDay(2, value); }
    public bool RoutineThursday { get => _routineDays[3]; set => SetDay(3, value); }
    public bool RoutineFriday { get => _routineDays[4]; set => SetDay(4, value); }
    public bool RoutineSaturday { get => _routineDays[5]; set => SetDay(5, value); }
    public bool RoutineSunday { get => _routineDays[6]; set => SetDay(6, value); }

    private void SetDay(int index, bool value)
    {
        if (_routineDays[index] == value)
        {
            return;
        }

        _routineDays[index] = value;
        OnPropertyChanged(index switch
        {
            0 => nameof(RoutineMonday),
            1 => nameof(RoutineTuesday),
            2 => nameof(RoutineWednesday),
            3 => nameof(RoutineThursday),
            4 => nameof(RoutineFriday),
            5 => nameof(RoutineSaturday),
            _ => nameof(RoutineSunday),
        });
    }

    public RoutineListItemViewModel? RoutineDeleteCandidate
    {
        get => _routineDeleteCandidate;
        private set
        {
            if (SetProperty(ref _routineDeleteCandidate, value))
            {
                OnPropertyChanged(nameof(IsRoutineDeleteVisible));
            }
        }
    }

    public bool IsRoutineDeleteVisible => _routineDeleteCandidate is not null;

    public void OpenNewRoutine()
    {
        _isEditingRoutine = false;
        _editingRoutineId = null;
        RoutineFormHeader = "Nova rotina";
        RoutineTitle = string.Empty;
        RoutineType = RecurrenceType.Daily;
        RoutineIntervalDays = "15";
        RoutineDayOfMonth = "10";
        ApplyDaysMask(0);
        IsRoutineFormOpen = true;
    }

    public void BeginEditRoutine(RoutineListItemViewModel item)
    {
        _isEditingRoutine = true;
        _editingRoutineId = item.Id;
        item.IsMenuOpen = false;
        RoutineFormHeader = "Editar rotina";
        RoutineTitle = item.Title;
        RoutineType = item.RecurrenceType;
        RoutineIntervalDays = (item.IntervalDays ?? 15).ToString();
        RoutineDayOfMonth = (item.DayOfMonth ?? 10).ToString();
        ApplyDaysMask(item.DaysOfWeek);
        IsRoutineFormOpen = true;
    }

    public void CommitRoutineForm()
    {
        var mask = CurrentDaysMask();
        var interval = int.TryParse(_routineIntervalDays, out var parsedInterval) ? parsedInterval : 0;
        var dayOfMonth = int.TryParse(_routineDayOfMonth, out var parsedDay) ? parsedDay : 0;

        var ok = _isEditingRoutine && _editingRoutineId is not null
            ? _routines.Update(_editingRoutineId, RoutineTitle, _routineType, mask, interval, dayOfMonth)
            : _routines.Create(RoutineTitle, _routineType, mask, interval, dayOfMonth) is not null;

        if (!ok)
        {
            // Regra incompleta ou título vazio: o formulário continua aberto.
            return;
        }

        IsRoutineFormOpen = false;
        _editingRoutineId = null;
        Reload();
        TasksChanged?.Invoke();
    }

    public void RequestRoutineDelete(RoutineListItemViewModel item)
    {
        item.IsMenuOpen = false;
        RoutineDeleteCandidate = item;
    }

    public void ConfirmRoutineDelete()
    {
        if (RoutineDeleteCandidate is null)
        {
            return;
        }

        _routines.Delete(RoutineDeleteCandidate.Id);
        RoutineDeleteCandidate = null;
        Reload();
        TasksChanged?.Invoke();
    }

    private int CurrentDaysMask()
    {
        var days = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
        };

        var mask = 0;
        for (var i = 0; i < 7; i++)
        {
            if (_routineDays[i])
            {
                mask |= RecurrenceCalculator.MaskOf(days[i]);
            }
        }

        return mask;
    }

    private void ApplyDaysMask(int mask)
    {
        var days = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
        };

        for (var i = 0; i < 7; i++)
        {
            SetDay(i, RecurrenceCalculator.Includes(mask, days[i]));
        }
    }

    // ---------- Carga ----------

    private void ShiftWeek(int days)
    {
        _weekStart = _weekStart.AddDays(days);
        Reload();
    }

    private void ShiftMonth(int months)
    {
        _monthAnchor = _monthAnchor.AddMonths(months);
        Reload();
    }

    private void OnSectionChanged()
    {
        Reload();
        TasksChanged?.Invoke();
    }

    public void Reload()
    {
        var today = _clock.Today;
        var containers = _containers.GetProgressIndex();

        // Hoje, a semana e o calendário misturam tarefas, ocorrências de rotina
        // (V0.4) e sessões de estudo (V0.6).
        var snapshot = _tasks.GetSnapshot();
        var (todayPending, todayCompleted) = DayBoard.Merge(
            snapshot.Pending.Select(t => Entry(t, containers)).ToList(),
            snapshot.CompletedToday.Select(t => Entry(t, containers)).ToList(),
            _routines.GetForDay(today),
            _exams.GetSessionsForDay(today));

        Fill(TodayItems, todayPending.Concat(todayCompleted).ToList(), today);
        OnPropertyChanged(nameof(IsTodayEmpty));

        // "Algum dia" é só de tarefas soltas: rotina e sessão sempre têm dia definido.
        Fill(SomedayItems, _tasks.GetSomeday().Display.Select(t => Entry(t, containers)).ToList(), today);
        OnPropertyChanged(nameof(IsSomedayEmpty));

        var weekEnd = _weekStart.AddDays(6);
        var weekOccurrences = _routines.GetBetween(_weekStart, weekEnd);
        var weekSessions = _exams.GetSessionsBetween(_weekStart, weekEnd);

        WeekDays.Clear();
        foreach (var day in _tasks.GetWeek(_weekStart))
        {
            var column = new DayColumnViewModel(day.Date, today);

            var entries = day.All.Select(t => Entry(t, containers))
                .Concat(weekSessions.Where(s => s.Session.PlannedDate == day.Date).Select(DayEntry.FromSession))
                .Concat(weekOccurrences.Where(o => o.Occurrence.OccurrenceDate == day.Date).Select(DayEntry.FromOccurrence));

            foreach (var entry in entries)
            {
                column.Items.Add(new TaskItemViewModel(entry, TaskLabels.For(entry, today)));
            }

            WeekDays.Add(column);
        }

        WeekLabel = $"{LocalDate.ToShortText(_weekStart)} – {LocalDate.ToShortText(weekEnd)}";

        var monthStart = LocalDate.StartOfWeek(_monthAnchor);
        var monthEnd = LocalDate.StartOfWeek(_monthAnchor.AddMonths(1).AddDays(-1)).AddDays(6);
        var monthOccurrences = _routines.GetBetween(monthStart, monthEnd);
        var monthSessions = _exams.GetSessionsBetween(monthStart, monthEnd);
        var allExams = _exams.GetAll();

        CalendarDays.Clear();
        foreach (var day in _tasks.GetMonth(_monthAnchor.Year, _monthAnchor.Month, monthOccurrences, monthSessions, allExams))
        {
            CalendarDays.Add(new CalendarDayViewModel(day, today));
        }

        Routines.Clear();
        foreach (var routine in _routines.GetAll())
        {
            Routines.Add(new RoutineListItemViewModel(routine));
        }

        OnPropertyChanged(nameof(IsRoutinesEmpty));

        CalendarLabel = $"{LocalDate.MonthName(_monthAnchor.Month)} de {_monthAnchor.Year}";

        UpcomingDeadlines.Clear();
        foreach (var deadline in _tasks.GetUpcomingDeadlines())
        {
            UpcomingDeadlines.Add(new DeadlineViewModel(deadline, today));
        }

        UpcomingExams.Clear();
        foreach (var exam in _exams.GetAll().Where(e => e.ExamDate >= today).OrderBy(e => e.ExamDate))
        {
            UpcomingExams.Add(new ExamReminderViewModel(exam, today));
        }

        OnPropertyChanged(nameof(IsDeadlinesEmpty));
        OnPropertyChanged(nameof(HasUpcomingExams));

        Projects.Reload();
        Deliveries.Reload();
        Exams.Reload();
        DailyPlan.Reload();
        Capacity.Reload();

        RefreshSelectedDay();
    }

    /// <summary>Converte a tarefa em linha, já com o nome e o progresso do todo maior.</summary>
    private static DayEntry Entry(
        TaskItem task,
        IReadOnlyDictionary<string, (Container Container, Progress Progress)> containers)
        => task.ParentId is { } parent && containers.TryGetValue(parent, out var found)
            ? DayEntry.FromTask(task, found.Container, found.Progress)
            : DayEntry.FromTask(task);

    private void RefreshSelectedDay()
    {
        var today = _clock.Today;
        var date = _selectedDate ?? today;

        foreach (var day in CalendarDays)
        {
            day.IsSelected = day.Date == date;
        }

        SelectedDay.Title = $"{LocalDate.DayOfWeekShortName(date)}, {LocalDate.ToShortText(date)}";
        SelectedDay.Planned.Clear();
        SelectedDay.Due.Clear();
        SelectedDay.Exams.Clear();

        var all = _tasks.GetAll();
        var containers = _containers.GetProgressIndex();

        var entries = TaskArranger.PlannedOn(all, date).Select(t => Entry(t, containers))
            .Concat(_exams.GetSessionsForDay(date).Select(DayEntry.FromSession))
            .Concat(_routines.GetForDay(date).Select(DayEntry.FromOccurrence));

        foreach (var entry in entries)
        {
            SelectedDay.Planned.Add(new TaskItemViewModel(entry, TaskLabels.For(entry, today)));
        }

        foreach (var model in all.Where(t => t.DueDate == date && !t.IsCompleted).OrderBy(t => t.SortOrder))
        {
            SelectedDay.Due.Add(new DeadlineViewModel(new UpcomingDeadline(model, date, date.DayNumber - today.DayNumber), today));
        }

        foreach (var exam in _exams.GetAll().Where(e => e.ExamDate == date))
        {
            SelectedDay.Exams.Add(new ExamReminderViewModel(exam, today));
        }

        SelectedDay.IsEmpty = SelectedDay.Planned.Count == 0
                              && SelectedDay.Due.Count == 0
                              && SelectedDay.Exams.Count == 0;
    }

    private static void Fill(ObservableCollection<TaskItemViewModel> target, IReadOnlyList<DayEntry> entries, DateOnly today)
    {
        target.Clear();
        foreach (var entry in entries)
        {
            target.Add(new TaskItemViewModel(entry, TaskLabels.For(entry, today)));
        }
    }

    private IEnumerable<TaskItemViewModel> AllItems()
        => TodayItems
            .Concat(SomedayItems)
            .Concat(WeekDays.SelectMany(d => d.Items))
            .Concat(SelectedDay.Planned)
            .Concat(Projects.Subtasks)
            .Concat(Deliveries.Subtasks);
}
