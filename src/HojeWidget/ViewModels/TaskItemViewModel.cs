using HojeWidget.Domain;
using HojeWidget.Models;

namespace HojeWidget.ViewModels;

/// <summary>
/// Uma linha da lista: tarefa comum ou ocorrência de rotina.
/// As duas se comportam igual na tela; o que muda é para onde vai a conclusão.
/// </summary>
public sealed class TaskItemViewModel : ObservableObject
{
    private string _title = string.Empty;
    private bool _isCompleted;
    private bool _isEditing;
    private string _editText = string.Empty;
    private bool _isMenuOpen;
    private bool _isDragging;
    private bool _isDropTargetBefore;
    private bool _isDropTargetAfter;
    private string? _subtitle;

    public TaskItemViewModel(DayEntry entry, string? subtitle = null)
    {
        Id = entry.Id;
        Kind = entry.Kind;
        _title = entry.Title;
        _isCompleted = entry.IsCompleted;
        _subtitle = subtitle;
        PlannedDate = entry.PlannedDate;
        DueDate = entry.DueDate;
        EstimatedMinutes = entry.EstimatedMinutes;
        CanSplit = entry.CanSplit;
        Priority = entry.Priority;
    }

    public TaskItemViewModel(TaskItem model, string? subtitle = null)
        : this(DayEntry.FromTask(model), subtitle)
    {
    }

    public string Id { get; }

    /// <summary>Tarefa comum ou ocorrência de rotina.</summary>
    public EntryKind Kind { get; }

    /// <summary>Rotinas ganham um marcador discreto na linha.</summary>
    public bool IsRoutine => Kind == EntryKind.Routine;

    /// <summary>V0.6 — sessões de estudo também ganham marcador próprio.</summary>
    public bool IsStudySession => Kind == EntryKind.StudySession;

    /// <summary>
    /// Marcador inline do tipo da linha: ↻ para rotina, ✎ para sessão de estudo.
    /// Vai dentro do próprio TextBlock do título para não atrapalhar a quebra de
    /// linha em colunas estreitas, como as da visão semanal.
    /// </summary>
    public string RoutinePrefix => Kind switch
    {
        EntryKind.Routine => "↻  ",
        EntryKind.StudySession => "✎  ",
        _ => string.Empty,
    };

    /// <summary>Ocorrência de rotina e sessão de estudo não se editam nem se excluem aqui.</summary>
    public bool CanEdit => Kind == EntryKind.Task;

    /// <summary>V0.5 — duração estimada, quando conhecida.</summary>
    public int? EstimatedMinutes { get; private set; }

    /// <summary>V0.5 — pode ser dividida em sessões?</summary>
    public bool CanSplit { get; private set; } = true;

    /// <summary>V0.7 — prioridade manual.</summary>
    public Priority Priority { get; private set; } = Priority.Normal;

    /// <summary>Só a prioridade alta e a baixa merecem um selo; normal é o silêncio.</summary>
    public bool HasPriorityBadge => Priority != Priority.Normal;

    public string PriorityBadge => PriorityLabels.Title(Priority).ToUpperInvariant();

    public bool IsHighPriority => Priority == Priority.High;

    /// <summary>Data planejada atual, usada pelo arrasto entre dias.</summary>
    public DateOnly? PlannedDate { get; private set; }

    /// <summary>Prazo atual, quando houver.</summary>
    public DateOnly? DueDate { get; private set; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetProperty(ref _isCompleted, value);
    }

    /// <summary>Linha discreta abaixo do título: atraso de planejamento e prazo.</summary>
    public string? Subtitle
    {
        get => _subtitle;
        set
        {
            if (SetProperty(ref _subtitle, value))
            {
                OnPropertyChanged(nameof(HasSubtitle));
            }
        }
    }

    public bool HasSubtitle => !string.IsNullOrEmpty(_subtitle);

    /// <summary>Concluidas e ocorrências de rotina não participam da ordenação manual.</summary>
    public bool CanReorder => !_isCompleted && Kind == EntryKind.Task;

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(IsNotEditing));
            }
        }
    }

    public bool IsNotEditing => !_isEditing;

    public string EditText
    {
        get => _editText;
        set => SetProperty(ref _editText, value);
    }

    public bool IsMenuOpen
    {
        get => _isMenuOpen;
        set => SetProperty(ref _isMenuOpen, value);
    }

    public bool IsDragging
    {
        get => _isDragging;
        set => SetProperty(ref _isDragging, value);
    }

    /// <summary>Marcador de insercao acima desta linha.</summary>
    public bool IsDropTargetBefore
    {
        get => _isDropTargetBefore;
        set => SetProperty(ref _isDropTargetBefore, value);
    }

    /// <summary>Marcador de insercao abaixo desta linha (usado no fim da lista de pendentes).</summary>
    public bool IsDropTargetAfter
    {
        get => _isDropTargetAfter;
        set => SetProperty(ref _isDropTargetAfter, value);
    }

    public void UpdateFrom(DayEntry entry, string? subtitle = null)
    {
        Title = entry.Title;
        IsCompleted = entry.IsCompleted;
        Subtitle = subtitle;
        PlannedDate = entry.PlannedDate;
        DueDate = entry.DueDate;
        EstimatedMinutes = entry.EstimatedMinutes;
        CanSplit = entry.CanSplit;
        Priority = entry.Priority;
        OnPropertyChanged(nameof(CanReorder));
    }

    public void UpdateFrom(TaskItem model, string? subtitle = null)
        => UpdateFrom(DayEntry.FromTask(model), subtitle);
}
