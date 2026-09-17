using System.Collections.ObjectModel;
using HojeWidget.Domain;
using HojeWidget.Domain.Planning;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.System;

namespace HojeWidget.ViewModels;

/// <summary>Um projeto ou entrega na lista lateral da seção.</summary>
public sealed class ContainerListItemViewModel : ObservableObject
{
    private bool _isSelected;
    private bool _isMenuOpen;

    public ContainerListItemViewModel(ContainerBoard board, DateOnly today)
    {
        Id = board.Container.Id;
        Title = board.Container.Title;
        Kind = board.Container.Kind;
        IsCompleted = board.Container.IsCompleted;
        Priority = board.Container.Priority;

        var progress = board.Progress;
        ProgressText = progress.Text;
        ProgressFraction = progress.Fraction;
        PercentText = progress.HasItems ? $"{progress.Percent}%" : string.Empty;

        DueDate = board.Container.DueDate;
        DueText = board.Container.DueDate is { } due
            ? $"Prazo: {LocalDate.ToShortText(due)} · {TaskLabels.DeadlineProximity(due, today)}"
            : "Sem prazo";
    }

    public string Id { get; }
    public string Title { get; }
    public ContainerKind Kind { get; }
    public bool IsCompleted { get; }
    public Priority Priority { get; }
    public string ProgressText { get; }
    public double ProgressFraction { get; }
    public string PercentText { get; }
    public DateOnly? DueDate { get; }
    public string DueText { get; }

    /// <summary>Largura da barra de progresso, com 150 de trilho.</summary>
    public double BarWidth => 150 * ProgressFraction;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set => SetProperty(ref _isMenuOpen, value);
    }
}

/// <summary>
/// V0.5 — a seção de Projetos ou a de Entregas.
///
/// A mesma classe atende as duas porque a mecânica é idêntica; o que muda é o
/// <see cref="ContainerKind"/> e os textos. Na interface elas continuam sendo
/// duas seções separadas, com nomes próprios.
/// </summary>
public sealed class ContainerSectionViewModel : ObservableObject
{
    private readonly ContainerService _containers;
    private readonly TaskListService _tasks;
    private readonly IClock _clock;
    private readonly DistributionProposalViewModel _proposal;
    private readonly Func<ICapacityProvider> _capacity;

    private ContainerListItemViewModel? _selected;
    private string _detailTitle = string.Empty;
    private string _detailDue = string.Empty;
    private string _progressText = string.Empty;
    private double _progressFraction;
    private string _progressBar = string.Empty;

    private bool _isFormOpen;
    private bool _isEditing;
    private string _formHeader = string.Empty;
    private string _formTitle = string.Empty;
    private DateTime? _formDueDate;
    private Priority _formPriority = Priority.Normal;

    private bool _isSubtaskFormOpen;
    private string _subtaskTitle = string.Empty;
    private string _subtaskMinutes = string.Empty;
    private bool _subtaskCanSplit = true;
    private DateTime? _subtaskPlannedDate;

    private bool _isCompletionAskVisible;
    private string _completionQuestion = string.Empty;

    private bool _isDeleteAskVisible;
    private bool _isNoticeVisible;
    private string _noticeText = string.Empty;

    public ContainerSectionViewModel(
        ContainerKind kind,
        ContainerService containers,
        TaskListService tasks,
        IClock clock,
        DistributionProposalViewModel proposal,
        Func<ICapacityProvider> capacity)
    {
        Kind = kind;
        _containers = containers;
        _tasks = tasks;
        _clock = clock;
        _proposal = proposal;
        _capacity = capacity;

        OpenNewCommand = new RelayCommand(OpenNew);
        CancelFormCommand = new RelayCommand(() => IsFormOpen = false);
        CommitFormCommand = new RelayCommand(CommitForm);
        ClearFormDueDateCommand = new RelayCommand(() => FormDueDate = null);

        OpenSubtaskFormCommand = new RelayCommand(OpenSubtaskForm);
        CancelSubtaskFormCommand = new RelayCommand(() => IsSubtaskFormOpen = false);
        CommitSubtaskFormCommand = new RelayCommand(CommitSubtaskForm);

        DistributeCommand = new RelayCommand(Distribute);
        ConfirmCompletionCommand = new RelayCommand(ConfirmCompletion);
        DismissCompletionCommand = new RelayCommand(() => IsCompletionAskVisible = false);

        DismissNoticeCommand = new RelayCommand(() => IsNoticeVisible = false);
        RequestDeleteCommand = new RelayCommand(() => IsDeleteAskVisible = Selected is not null);
        CancelDeleteCommand = new RelayCommand(() => IsDeleteAskVisible = false);
        ConfirmDeleteCommand = new RelayCommand(ConfirmDelete);
    }

    public ContainerKind Kind { get; }

    /// <summary>"Projetos" ou "Entregas".</summary>
    public string Header => Kind == ContainerKind.Project ? "Projetos" : "Entregas";

    public string NewButtonText => Kind == ContainerKind.Project ? "+ Novo projeto" : "+ Nova entrega";

    public string SingularLabel => Kind == ContainerKind.Project ? "projeto" : "entrega";

    public string EmptyText => Kind == ContainerKind.Project
        ? "Nenhum projeto ainda. Um projeto é uma iniciativa maior, com várias etapas ao longo do tempo."
        : "Nenhuma entrega ainda. Uma entrega tem poucos passos e um resultado final definido.";

    public ObservableCollection<ContainerListItemViewModel> Items { get; } = new();

    public ObservableCollection<TaskItemViewModel> Subtasks { get; } = new();

    public RelayCommand OpenNewCommand { get; }
    public RelayCommand CancelFormCommand { get; }
    public RelayCommand CommitFormCommand { get; }
    public RelayCommand ClearFormDueDateCommand { get; }
    public RelayCommand OpenSubtaskFormCommand { get; }
    public RelayCommand CancelSubtaskFormCommand { get; }
    public RelayCommand CommitSubtaskFormCommand { get; }
    public RelayCommand DistributeCommand { get; }
    public RelayCommand ConfirmCompletionCommand { get; }
    public RelayCommand DismissCompletionCommand { get; }
    public RelayCommand DismissNoticeCommand { get; }
    public RelayCommand RequestDeleteCommand { get; }
    public RelayCommand CancelDeleteCommand { get; }
    public RelayCommand ConfirmDeleteCommand { get; }

    public event Action? Changed;

    public bool IsEmpty => Items.Count == 0;

    public bool HasSelection => _selected is not null;

    public ContainerListItemViewModel? Selected
    {
        get => _selected;
        private set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public string DetailTitle
    {
        get => _detailTitle;
        private set => SetProperty(ref _detailTitle, value);
    }

    public string DetailDue
    {
        get => _detailDue;
        private set => SetProperty(ref _detailDue, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public double ProgressFraction
    {
        get => _progressFraction;
        private set
        {
            if (SetProperty(ref _progressFraction, value))
            {
                OnPropertyChanged(nameof(ProgressBarWidth));
            }
        }
    }

    public double ProgressBarWidth => 260 * _progressFraction;

    /// <summary>A mesma barra em texto — a que a especificação desenha.</summary>
    public string ProgressBar
    {
        get => _progressBar;
        private set => SetProperty(ref _progressBar, value);
    }

    // ---------- Formulário do projeto/entrega ----------

    public bool IsFormOpen
    {
        get => _isFormOpen;
        private set => SetProperty(ref _isFormOpen, value);
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

    public DateTime? FormDueDate
    {
        get => _formDueDate;
        set => SetProperty(ref _formDueDate, value);
    }

    public Priority FormPriority
    {
        get => _formPriority;
        set => SetProperty(ref _formPriority, value);
    }

    // ---------- Formulário da subtarefa ----------

    public bool IsSubtaskFormOpen
    {
        get => _isSubtaskFormOpen;
        private set => SetProperty(ref _isSubtaskFormOpen, value);
    }

    public string SubtaskTitle
    {
        get => _subtaskTitle;
        set => SetProperty(ref _subtaskTitle, value);
    }

    /// <summary>Aceita "1h30", "90". Vazio significa duração desconhecida.</summary>
    public string SubtaskMinutes
    {
        get => _subtaskMinutes;
        set => SetProperty(ref _subtaskMinutes, value);
    }

    public bool SubtaskCanSplit
    {
        get => _subtaskCanSplit;
        set => SetProperty(ref _subtaskCanSplit, value);
    }

    public DateTime? SubtaskPlannedDate
    {
        get => _subtaskPlannedDate;
        set => SetProperty(ref _subtaskPlannedDate, value);
    }

    // ---------- Conclusão e exclusão ----------

    public bool IsCompletionAskVisible
    {
        get => _isCompletionAskVisible;
        private set => SetProperty(ref _isCompletionAskVisible, value);
    }

    public string CompletionQuestion
    {
        get => _completionQuestion;
        private set => SetProperty(ref _completionQuestion, value);
    }

    public bool IsDeleteAskVisible
    {
        get => _isDeleteAskVisible;
        private set => SetProperty(ref _isDeleteAskVisible, value);
    }

    /// <summary>Aviso simples, com um só botão. Não se confunde com uma pergunta.</summary>
    public bool IsNoticeVisible
    {
        get => _isNoticeVisible;
        private set => SetProperty(ref _isNoticeVisible, value);
    }

    public string NoticeText
    {
        get => _noticeText;
        private set => SetProperty(ref _noticeText, value);
    }

    public string DeleteQuestion => Selected is null
        ? string.Empty
        : $"Excluir o {SingularLabel} \"{Selected.Title}\"?";

    // ---------- Carga ----------

    public void Reload()
    {
        var today = _clock.Today;
        var previous = Selected?.Id;

        Items.Clear();
        foreach (var board in _containers.GetBoards(Kind))
        {
            Items.Add(new ContainerListItemViewModel(board, today));
        }

        Selected = Items.FirstOrDefault(i => i.Id == previous) ?? Items.FirstOrDefault();

        foreach (var item in Items)
        {
            item.IsSelected = item.Id == Selected?.Id;
        }

        RefreshDetail();
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Select(ContainerListItemViewModel item)
    {
        Selected = item;
        foreach (var other in Items)
        {
            other.IsSelected = other.Id == item.Id;
        }

        RefreshDetail();
    }

    private void RefreshDetail()
    {
        Subtasks.Clear();

        if (Selected is null || _containers.GetBoard(Selected.Id) is not { } board)
        {
            DetailTitle = string.Empty;
            DetailDue = string.Empty;
            ProgressText = string.Empty;
            ProgressFraction = 0;
            ProgressBar = string.Empty;
            OnPropertyChanged(nameof(DeleteQuestion));
            return;
        }

        var today = _clock.Today;

        DetailTitle = board.Container.Title;
        DetailDue = board.Container.DueDate is { } due
            ? $"Prazo: {LocalDate.ToShortText(due)}"
            : "Sem prazo";

        var progress = board.Progress;
        ProgressText = progress.Text;
        ProgressFraction = progress.Fraction;
        ProgressBar = progress.Bar();

        foreach (var task in board.Subtasks)
        {
            var entry = DayEntry.FromTask(task);
            Subtasks.Add(new TaskItemViewModel(entry, TaskLabels.For(entry, today)));
        }

        OnPropertyChanged(nameof(DeleteQuestion));
    }

    // ---------- Ações ----------

    private void OpenNew()
    {
        _isEditing = false;
        FormHeader = Kind == ContainerKind.Project ? "Novo projeto" : "Nova entrega";
        FormTitle = string.Empty;
        FormDueDate = null;
        FormPriority = Priority.Normal;
        IsFormOpen = true;
    }

    public void BeginEdit()
    {
        if (Selected is null || _containers.GetById(Selected.Id) is not { } container)
        {
            return;
        }

        _isEditing = true;
        FormHeader = Kind == ContainerKind.Project ? "Editar projeto" : "Editar entrega";
        FormTitle = container.Title;
        FormDueDate = container.DueDate?.ToDateTime(TimeOnly.MinValue);
        FormPriority = container.Priority;
        IsFormOpen = true;
    }

    private void CommitForm()
    {
        var due = FormDueDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;

        var ok = _isEditing && Selected is not null
            ? _containers.Update(Selected.Id, FormTitle, due, FormPriority)
            : _containers.Create(Kind, FormTitle, due, FormPriority) is not null;

        if (!ok)
        {
            return;
        }

        IsFormOpen = false;
        Reload();
        Changed?.Invoke();
    }

    private void OpenSubtaskForm()
    {
        if (Selected is null)
        {
            return;
        }

        SubtaskTitle = string.Empty;
        SubtaskMinutes = string.Empty;
        SubtaskCanSplit = true;
        SubtaskPlannedDate = null;
        IsSubtaskFormOpen = true;
    }

    private void CommitSubtaskForm()
    {
        if (Selected is null)
        {
            return;
        }

        var planned = SubtaskPlannedDate is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null;

        var created = _containers.AddSubtask(
            Selected.Id,
            SubtaskTitle,
            planned,
            dueDate: null,
            Estimate.TryParse(SubtaskMinutes),
            SubtaskCanSplit);

        if (created is null)
        {
            return;
        }

        IsSubtaskFormOpen = false;
        Reload();
        Changed?.Invoke();
    }

    /// <summary>
    /// Concluir uma subtarefa. Quando era a última, a pergunta aparece — mas o
    /// todo nunca se conclui sozinho.
    /// </summary>
    public void ToggleSubtask(TaskItemViewModel item)
    {
        _tasks.SetCompleted(item.Id, item.IsCompleted);

        if (Selected is not null && item.IsCompleted && _containers.IsFullyChecked(Selected.Id))
        {
            CompletionQuestion = $"O {SingularLabel} \"{Selected.Title}\" está concluído?";
            IsCompletionAskVisible = true;
        }

        Reload();
        Changed?.Invoke();
    }

    public void DeleteSubtask(TaskItemViewModel item)
    {
        _tasks.Delete(item.Id);
        Reload();
        Changed?.Invoke();
    }

    /// <summary>Tira a subtarefa do todo maior; ela volta a ser uma tarefa solta.</summary>
    public void DetachSubtask(TaskItemViewModel item)
    {
        _containers.MoveTaskInto(item.Id, null);
        Reload();
        Changed?.Invoke();
    }

    private void ConfirmCompletion()
    {
        if (Selected is not null)
        {
            _containers.SetCompleted(Selected.Id, true);
        }

        IsCompletionAskVisible = false;
        Reload();
        Changed?.Invoke();
    }

    private void ConfirmDelete()
    {
        if (Selected is not null)
        {
            _containers.Delete(Selected.Id);
        }

        IsDeleteAskVisible = false;
        Selected = null;
        Reload();
        Changed?.Invoke();
    }

    private void Distribute()
    {
        if (Selected is null)
        {
            return;
        }

        var plan = _containers.BuildDistribution(Selected.Id, _capacity());

        if (plan.IsEmpty && !plan.HasConflicts)
        {
            NoticeText = Selected.DueDate is null
                ? $"Defina um prazo para {(Kind == ContainerKind.Project ? "o projeto" : "a entrega")} antes de distribuir as etapas."
                : "Não há etapa pendente para distribuir.";
            IsNoticeVisible = true;
            return;
        }

        _proposal.Open($"{Selected.Title} · {Selected.DueText}", plan, _clock.Today, slots =>
        {
            _containers.ApplyDistribution(slots);
            Reload();
            Changed?.Invoke();
        });
    }
}
