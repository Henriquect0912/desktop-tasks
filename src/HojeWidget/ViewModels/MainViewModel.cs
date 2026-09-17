using System.Collections.ObjectModel;
using HojeWidget.Domain;
using HojeWidget.Domain.Planning;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.System;

namespace HojeWidget.ViewModels;

/// <summary>Estado da interface do widget.</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly TaskListService _tasks;
    private readonly RoutineService _routines;
    private readonly ExamService _exams;
    private readonly PlanningService _planning;
    private readonly DailyReviewService _review;
    private readonly IClock _clock;

    private string _pendingCountText = PendingCounter.Format(0);
    private ListState _state = ListState.Empty;
    private bool _isAdding;
    private bool _isAddMenuOpen;
    private string _newTaskText = string.Empty;
    private bool _isReviewVisible;
    private string _reviewSummary = "Revise suas tarefas de hoje.";
    private TaskItemViewModel? _deleteCandidate;
    private string _currentLocalDate;

    public MainViewModel(
        TaskListService tasks,
        RoutineService routines,
        ExamService exams,
        PlanningService planning,
        DailyReviewService review,
        IClock clock)
    {
        _tasks = tasks;
        _routines = routines;
        _exams = exams;
        _planning = planning;
        _review = review;
        _clock = clock;
        _currentLocalDate = clock.LocalDate;

        ToggleAddMenuCommand = new RelayCommand(ToggleAddMenu);
        BeginAddCommand = new RelayCommand(BeginAdd);
        OpenMainAppCommand = new RelayCommand(OpenMainApp);
        CommitAddCommand = new RelayCommand(CommitAdd);
        CancelAddCommand = new RelayCommand(CancelAdd);
        DismissReviewCommand = new RelayCommand(DismissReview);
        ReviewAddTaskCommand = new RelayCommand(BeginAdd);
        ReviewPlanCommand = new RelayCommand(OpenDailyPlan);
        ConfirmDeleteCommand = new RelayCommand(ConfirmDelete);
        CancelDeleteCommand = new RelayCommand(CancelDelete);

        Reload();
    }

    public ObservableCollection<TaskItemViewModel> Items { get; } = new();

    public RelayCommand ToggleAddMenuCommand { get; }
    public RelayCommand BeginAddCommand { get; }
    public RelayCommand OpenMainAppCommand { get; }
    public RelayCommand CommitAddCommand { get; }
    public RelayCommand CancelAddCommand { get; }
    public RelayCommand DismissReviewCommand { get; }
    public RelayCommand ReviewAddTaskCommand { get; }
    public RelayCommand ReviewPlanCommand { get; }
    public RelayCommand ConfirmDeleteCommand { get; }
    public RelayCommand CancelDeleteCommand { get; }

    /// <summary>Disparado quando o campo de criacao deve receber foco.</summary>
    public event Action? AddFieldFocusRequested;

    /// <summary>Disparado quando o usuario pede para abrir o aplicativo principal.</summary>
    public event Action? OpenMainAppRequested;

    /// <summary>V0.7 — disparado quando o usuário quer revisar o plano do dia.</summary>
    public event Action? OpenDailyPlanRequested;

    /// <summary>Disparado quando o campo de edicao de um item deve receber foco.</summary>
    public event Action<TaskItemViewModel>? EditFieldFocusRequested;

    public string PendingCountText
    {
        get => _pendingCountText;
        private set => SetProperty(ref _pendingCountText, value);
    }

    public ListState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsAllDone));
            }
        }
    }

    public bool IsEmpty => _state == ListState.Empty;

    public bool IsAllDone => _state == ListState.AllDone;

    public bool IsAdding
    {
        get => _isAdding;
        private set => SetProperty(ref _isAdding, value);
    }

    /// <summary>Menu do botão "+": adicionar para hoje ou abrir o aplicativo.</summary>
    public bool IsAddMenuOpen
    {
        get => _isAddMenuOpen;
        private set => SetProperty(ref _isAddMenuOpen, value);
    }

    public string NewTaskText
    {
        get => _newTaskText;
        set => SetProperty(ref _newTaskText, value);
    }

    public bool IsReviewVisible
    {
        get => _isReviewVisible;
        private set => SetProperty(ref _isReviewVisible, value);
    }

    /// <summary>
    /// V0.7 — a revisão diária deixou de ser um lembrete genérico e passou a dizer
    /// o que está planejado: "5 atividades · 3h20 estimadas".
    /// </summary>
    public string ReviewSummary
    {
        get => _reviewSummary;
        private set => SetProperty(ref _reviewSummary, value);
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

    /// <summary>Data local considerada na ultima atualizacao da lista.</summary>
    public string CurrentLocalDate => _currentLocalDate;

    // ---------- Lista ----------

    /// <summary>Recarrega tudo do banco e reaplica agrupamento e contagem.</summary>
    public void Reload()
    {
        var snapshot = _tasks.GetSnapshot();
        _currentLocalDate = snapshot.LocalDate;

        // V0.4/V0.6: Hoje mistura tarefas comuns, ocorrências de rotina e sessões de estudo.
        var occurrences = _routines.GetForDay(snapshot.Today);
        var sessions = _exams.GetSessionsForDay(snapshot.Today);
        var (pending, completed) = DayBoard.Merge(
            snapshot.Pending.Select(t => DayEntry.FromTask(t)).ToList(),
            snapshot.CompletedToday.Select(t => DayEntry.FromTask(t)).ToList(),
            occurrences,
            sessions);

        var existing = Items.ToDictionary(i => i.Id, StringComparer.Ordinal);

        Items.Clear();
        foreach (var entry in pending.Concat(completed))
        {
            var subtitle = BuildSubtitle(entry, snapshot.Today);

            if (existing.TryGetValue(entry.Id, out var vm))
            {
                vm.UpdateFrom(entry, subtitle);
                vm.IsDropTargetBefore = false;
                vm.IsDropTargetAfter = false;
                vm.IsDragging = false;
                Items.Add(vm);
            }
            else
            {
                Items.Add(new TaskItemViewModel(entry, subtitle));
            }
        }

        PendingCountText = PendingCounter.Format(pending.Count);
        State = pending.Count > 0
            ? ListState.Normal
            : completed.Count > 0 ? ListState.AllDone : ListState.Empty;
    }

    /// <summary>
    /// Linha discreta abaixo do título: atraso de planejamento e prazo.
    /// Concluídas não mostram nada — o widget não cobra o que já foi feito.
    /// </summary>
    private static string? BuildSubtitle(DayEntry entry, DateOnly today)
        => TaskLabels.For(entry, today);

    /// <summary>
    /// Reavalia a data local. Retorna true quando o dia mudou e a lista foi atualizada.
    /// </summary>
    public bool RefreshForDayChange()
    {
        var today = _clock.LocalDate;
        if (string.Equals(today, _currentLocalDate, StringComparison.Ordinal))
        {
            return false;
        }

        Reload();
        return true;
    }

    // ---------- Criacao ----------

    public void ToggleAddMenu() => IsAddMenuOpen = !IsAddMenuOpen;

    /// <summary>Abre o campo de criação inline. A tarefa nasce planejada para hoje.</summary>
    public void BeginAdd()
    {
        IsAddMenuOpen = false;
        NewTaskText = string.Empty;
        IsAdding = true;
        AddFieldFocusRequested?.Invoke();
    }

    public void OpenMainApp()
    {
        IsAddMenuOpen = false;
        OpenMainAppRequested?.Invoke();
    }

    public void CommitAdd()
    {
        var created = _tasks.AddForToday(NewTaskText);
        NewTaskText = string.Empty;

        if (created is null)
        {
            // Texto vazio nao cria tarefa e mantem o campo aberto.
            AddFieldFocusRequested?.Invoke();
            return;
        }

        Reload();
        AddFieldFocusRequested?.Invoke();
    }

    public void CancelAdd()
    {
        NewTaskText = string.Empty;
        IsAdding = false;
    }

    // ---------- Conclusao ----------

    public void ToggleCompleted(TaskItemViewModel item, bool completed)
    {
        switch (item.Kind)
        {
            case EntryKind.Routine:
                // Concluir a ocorrência de hoje não encerra a rotina.
                _routines.SetOccurrenceCompleted(item.Id, completed);
                break;
            case EntryKind.StudySession:
                _exams.SetSessionCompleted(item.Id, completed);
                break;
            default:
                _tasks.SetCompleted(item.Id, completed);
                break;
        }

        Reload();
    }

    // ---------- Edicao ----------

    public void BeginEdit(TaskItemViewModel item)
    {
        foreach (var other in Items)
        {
            other.IsEditing = false;
            other.IsMenuOpen = false;
        }

        item.EditText = item.Title;
        item.IsEditing = true;
        EditFieldFocusRequested?.Invoke(item);
    }

    public void CommitEdit(TaskItemViewModel item)
    {
        if (_tasks.Rename(item.Id, item.EditText))
        {
            item.IsEditing = false;
            Reload();
            return;
        }

        // Texto invalido: cancela sem alterar nada.
        CancelEdit(item);
    }

    public static void CancelEdit(TaskItemViewModel item)
    {
        item.IsEditing = false;
        item.EditText = string.Empty;
    }

    // ---------- Exclusao ----------

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
    }

    public void CancelDelete() => DeleteCandidate = null;

    // ---------- Ordenacao manual ----------

    /// <summary>Move uma pendente para outra posicao entre as pendentes e persiste a ordem.</summary>
    public void MovePending(TaskItemViewModel item, int targetIndexAmongPending)
    {
        var pending = Items.Where(i => !i.IsCompleted).ToList();
        var currentIndex = pending.IndexOf(item);
        if (currentIndex < 0)
        {
            return;
        }

        var target = Math.Clamp(targetIndexAmongPending, 0, pending.Count - 1);
        if (target == currentIndex)
        {
            return;
        }

        pending.RemoveAt(currentIndex);
        pending.Insert(target, item);

        _tasks.ReorderPending(pending.Select(i => i.Id).ToList());
        Reload();
    }

    // ---------- Revisao diaria ----------

    /// <summary>
    /// Decide se o aviso deve ser exibido na ativação atual e monta o resumo do
    /// planejamento sugerido para hoje.
    /// </summary>
    public void EvaluateDailyReview()
    {
        _currentLocalDate = _clock.LocalDate;
        IsReviewVisible = _review.ShouldShow();

        if (!IsReviewVisible)
        {
            return;
        }

        var result = _planning.Plan();
        var count = result.TodayPlan.Count;
        var minutes = result.TodayPlannedMinutes;

        ReviewSummary = count switch
        {
            0 => "Nada planejado para hoje ainda.",
            _ when minutes == 0 => $"Planejamento sugerido: {Describe(count)}.",
            _ => $"Planejamento sugerido: {Describe(count)} · {Estimate.Load(minutes)}.",
        };
    }

    private static string Describe(int count) => count == 1 ? "1 atividade" : $"{count} atividades";

    private void OpenDailyPlan()
    {
        DismissReview();
        OpenDailyPlanRequested?.Invoke();
    }

    public void DismissReview()
    {
        _review.Dismiss();
        IsReviewVisible = false;
    }
}
