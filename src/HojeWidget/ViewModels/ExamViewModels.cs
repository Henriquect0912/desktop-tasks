using System.Collections.ObjectModel;
using HojeWidget.Domain;
using HojeWidget.Domain.Study;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.System;

namespace HojeWidget.ViewModels;

/// <summary>Uma prova na lista lateral.</summary>
public sealed class ExamListItemViewModel : ObservableObject
{
    private bool _isSelected;

    public ExamListItemViewModel(ExamBoard board, DateOnly today)
    {
        Id = board.Exam.Id;
        Title = board.Exam.DisplayName;
        DateText = LocalDate.ToShortText(board.Exam.ExamDate);
        Proximity = TaskLabels.ExamProximity(board.Exam.ExamDate, today);
        IsPast = board.Exam.ExamDate < today;

        var topics = board.TopicProgress;
        var lists = board.ListProgress;

        var parts = new List<string>(3);
        if (topics.HasItems)
        {
            parts.Add($"Conteúdos estudados: {topics.Completed}/{topics.Total}");
        }

        if (lists.HasItems)
        {
            parts.Add($"Listas concluídas: {lists.Completed}/{lists.Total}");
        }

        if (board.WaitingCount > 0)
        {
            parts.Add(board.WaitingCount == 1
                ? "1 item vem depois de outro"
                : $"{board.WaitingCount} itens vêm depois de outros");
        }

        Summary = parts.Count == 0 ? "Nenhum conteúdo cadastrado ainda." : string.Join(" · ", parts);
    }

    public string Id { get; }
    public string Title { get; }
    public string DateText { get; }
    public string Proximity { get; }
    public string Summary { get; }
    public bool IsPast { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// Uma linha da árvore de estudo. A árvore é achatada numa lista com recuo,
/// porque o que importa é ver a estrutura inteira de uma vez.
/// </summary>
public sealed class StudyNodeViewModel : ObservableObject
{
    private readonly Action<StudyNodeViewModel>? _onChanged;
    private Mastery _mastery;
    private bool _isCompleted;
    private bool _isSelected;
    private bool _isAddingHere;

    public StudyNodeViewModel(
        StudyNode node,
        StudyNodeState state,
        int depth,
        IReadOnlyList<string> after,
        Action<StudyNodeViewModel>? onChanged = null)
    {
        _onChanged = onChanged;
        Id = node.Id;
        Title = node.Title;
        NodeType = node.NodeType;
        IsSubject = node.IsSubject;
        Depth = depth;
        State = state;
        _mastery = node.MasteryLevel ?? Mastery.NotStudied;
        _isCompleted = node.IsCompleted;
        EstimatedMinutes = node.EstimatedMinutes;
        CanSplit = node.CanSplit;
        QuestionCount = node.QuestionCount;

        After = after;
        AfterText = after.Count == 0 ? string.Empty : "Depois de: " + string.Join(", ", after);

        var details = new List<string>(3);
        if (node.QuestionCount is { } questions)
        {
            details.Add(questions == 1 ? "1 questão" : $"{questions} questões");
        }

        if (Estimate.Format(node.EstimatedMinutes) is { } estimate)
        {
            details.Add(estimate);
        }

        if (!node.CanSplit && node.IsPlannable)
        {
            details.Add("bloco inteiro");
        }

        Details = string.Join(" · ", details);
    }

    public string Id { get; }
    public string Title { get; }
    public StudyNodeType NodeType { get; }
    public bool IsSubject { get; }
    public int Depth { get; }
    public StudyNodeState State { get; }
    public int? EstimatedMinutes { get; }
    public bool CanSplit { get; }
    public int? QuestionCount { get; }
    /// <summary>O que faz sentido vir antes deste item. Não impede nada.</summary>
    public IReadOnlyList<string> After { get; }
    public string Details { get; }
    public bool HasDetails => Details.Length > 0;

    /// <summary>Recuo visual da linha: 18 pontos por nível.</summary>
    public double Indent => Depth * 18;

    /// <summary>
    /// O estado nunca depende só de cor: cada um tem o seu símbolo e o seu texto.
    /// </summary>
    public string StateGlyph => State switch
    {
        StudyNodeState.Completed => "✓",
        StudyNodeState.Waiting => "↳",
        StudyNodeState.Partial => "◐",
        _ => "○",
    };

    public string StateText => State switch
    {
        StudyNodeState.Completed => "concluído",
        StudyNodeState.Waiting => "pendente, vem depois de outro item",
        StudyNodeState.Partial => "parcial",
        _ => "pendente",
    };

    /// <summary>Espera a vez de outro item — e mesmo assim pode ser feito quando você quiser.</summary>
    public bool IsWaiting => State == StudyNodeState.Waiting;

    public string AfterText { get; }

    /// <summary>Assunto não tem domínio nem caixa de conclusão: ele só agrupa.</summary>
    public bool IsPlannable => !IsSubject;

    public bool IsList => NodeType == StudyNodeType.ExerciseList;

    public string TypeLabel => NodeType switch
    {
        StudyNodeType.Topic => "ASSUNTO",
        StudyNodeType.Theory => "TEORIA",
        StudyNodeType.ExerciseList => "LISTA",
        StudyNodeType.Review => "REVISÃO",
        _ => "OUTRO",
    };

    public Mastery Mastery
    {
        get => _mastery;
        set
        {
            if (SetProperty(ref _mastery, value))
            {
                _onChanged?.Invoke(this);
            }
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (SetProperty(ref _isCompleted, value))
            {
                _onChanged?.Invoke(this);
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Este assunto está com o editor inline aberto.</summary>
    public bool IsAddingHere
    {
        get => _isAddingHere;
        set => SetProperty(ref _isAddingHere, value);
    }
}

/// <summary>Um item da lista de dependências possíveis, no painel do nó.</summary>
public sealed class DependencyOptionViewModel : ObservableObject
{
    private readonly Action<DependencyOptionViewModel, bool>? _onToggled;
    private bool _isChecked;
    private bool _suppress;

    public DependencyOptionViewModel(
        string id, string title, string context, bool isChecked,
        Action<DependencyOptionViewModel, bool>? onToggled = null)
    {
        Id = id;
        Title = title;
        Context = context;
        _isChecked = isChecked;
        _onToggled = onToggled;
    }

    public string Id { get; }
    public string Title { get; }
    public string Context { get; }
    public bool HasContext => Context.Length > 0;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value) && !_suppress)
            {
                _onToggled?.Invoke(this, value);
            }
        }
    }

    /// <summary>Volta a caixa sem disparar a gravação de novo — usado quando o grafo recusa.</summary>
    public void Revert(bool value)
    {
        _suppress = true;
        IsChecked = value;
        _suppress = false;
    }
}

/// <summary>Uma linha do plano de estudo proposto, com a data ainda editável.</summary>
public sealed class StudySessionRowViewModel : ObservableObject
{
    private DateTime _date;

    public StudySessionRowViewModel(PlannedStudySession session)
    {
        NodeId = session.NodeId;
        Title = session.Title;
        Minutes = session.Minutes;
        SessionIndex = session.SessionIndex;
        SessionCount = session.SessionCount;
        Explanation = session.Explanation;
        _date = session.Date.ToDateTime(TimeOnly.MinValue);
    }

    public string NodeId { get; }
    public string Title { get; }
    public int Minutes { get; }
    public int SessionIndex { get; }
    public int SessionCount { get; }
    public string Explanation { get; }

    public DateTime Date
    {
        get => _date;
        set
        {
            if (SetProperty(ref _date, value))
            {
                OnPropertyChanged(nameof(DateText));
            }
        }
    }

    public string DateText => LocalDate.ToShortText(DateOnly.FromDateTime(_date));

    public string DetailText => SessionCount > 1
        ? $"{Estimate.Format(Minutes)} · sessão {SessionIndex} de {SessionCount}"
        : Estimate.Format(Minutes);

    public PlannedStudySession ToSession() => new(
        NodeId, Title, DateOnly.FromDateTime(_date), Minutes, SessionIndex, SessionCount, Explanation);
}
