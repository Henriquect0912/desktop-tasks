using System.Collections.ObjectModel;
using HojeWidget.Domain;
using HojeWidget.Domain.Study;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.System;

namespace HojeWidget.ViewModels;

/// <summary>
/// V0.7.1 — a seção Provas: árvore de estudo, grafo de dependências e o plano
/// de estudo proposto.
///
/// A prova não é uma tarefa e não se conclui. O que entra em Hoje e na Semana
/// são as sessões — e só as que estão com o caminho livre.
/// </summary>
public sealed class ExamSectionViewModel : ObservableObject
{
    private readonly ExamService _exams;
    private readonly StudyGraphService _graph;
    private readonly ExamPlanningService _planning;
    private readonly IClock _clock;

    private ExamListItemViewModel? _selected;
    private StudyNodeViewModel? _selectedNode;

    private string _detailTitle = string.Empty;
    private string _detailDate = string.Empty;
    private string _policyText = string.Empty;
    private string _topicProgress = string.Empty;
    private string _listProgress = string.Empty;
    private string _sessionProgress = string.Empty;
    private string _waitingSummary = string.Empty;
    private string _reviewSummary = string.Empty;

    private bool _isFormOpen;
    private bool _isEditing;
    private string _formHeader = "Nova prova";
    private string _formSubject = string.Empty;
    private string _formTitle = string.Empty;
    private DateTime? _formDate;
    private string _formComfortable = "2h";
    private string _formMaxDaily = "3h";
    private string _formMaxSession = "1h30";
    private string _formError = string.Empty;

    /// <summary>Sentinela de "editor aberto para um item de raiz".</summary>
    private const string RootParent = "__raiz__";

    private string? _inlineParentId;
    private StudyNodeType _inlineType = StudyNodeType.Theory;
    private string _inlineTitle = string.Empty;
    private string _inlineMinutes = string.Empty;
    private string _inlineQuestions = string.Empty;
    private bool _inlineDependOnTheory = true;

    private string _nodeMinutes = string.Empty;
    private bool _nodeCanSplit = true;
    private string _nodeQuestions = string.Empty;
    private string _dependencyError = string.Empty;

    private bool _isDeleteAskVisible;
    private string _deleteQuestion = string.Empty;
    private Action? _deleteAction;

    private bool _isNoticeVisible;
    private string _noticeText = string.Empty;

    private bool _isPlanOpen;
    private string _planSubtitle = string.Empty;
    private string _deficitText = string.Empty;
    private string _startText = string.Empty;
    private bool _isAdjustingPlan;
    private bool _isImpossible;
    private string? _marginText;
    private int _temporaryMaxDaily;

    public ExamSectionViewModel(
        ExamService exams,
        ExamPlanningService planning,
        IClock clock)
    {
        _exams = exams;
        _graph = exams.Graph;
        _planning = planning;
        _clock = clock;

        OpenNewCommand = new RelayCommand(OpenNew);
        EditCommand = new RelayCommand(BeginEdit);
        CancelFormCommand = new RelayCommand(() => IsFormOpen = false);
        CommitFormCommand = new RelayCommand(CommitForm);

        AddSubjectCommand = new RelayCommand(AddSubject);
        CommitInlineCommand = new RelayCommand(CommitInline);
        CancelInlineCommand = new RelayCommand(CloseInline);

        SaveNodeCommand = new RelayCommand(SaveNode);
        DeleteNodeCommand = new RelayCommand(RequestDeleteNode);
        CloseNodePanelCommand = new RelayCommand(() => SelectNode(null));

        GeneratePlanCommand = new RelayCommand(GeneratePlan);
        AdjustPlanCommand = new RelayCommand(() => IsAdjustingPlan = true);
        AcceptPlanCommand = new RelayCommand(AcceptPlan);
        CancelPlanCommand = new RelayCommand(ClosePlan);
        RaiseLimitCommand = new RelayCommand(RaiseLimitTemporarily);
        AdjustContentCommand = new RelayCommand(ClosePlan);

        RequestDeleteExamCommand = new RelayCommand(RequestDeleteExam);
        CancelDeleteCommand = new RelayCommand(() => IsDeleteAskVisible = false);
        ConfirmDeleteCommand = new RelayCommand(ConfirmDelete);
        DismissNoticeCommand = new RelayCommand(() => IsNoticeVisible = false);

    }

    public ObservableCollection<ExamListItemViewModel> Items { get; } = new();
    public ObservableCollection<StudyNodeViewModel> Tree { get; } = new();
    public ObservableCollection<TaskItemViewModel> Sessions { get; } = new();
    public ObservableCollection<DependencyOptionViewModel> DependencyOptions { get; } = new();
    public ObservableCollection<StudySessionRowViewModel> PlanRows { get; } = new();
    public ObservableCollection<string> PlanWaiting { get; } = new();
    public ObservableCollection<string> PlanConflicts { get; } = new();
    public ObservableCollection<string> PlanNotes { get; } = new();

    public RelayCommand OpenNewCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand CancelFormCommand { get; }
    public RelayCommand CommitFormCommand { get; }
    public RelayCommand AddSubjectCommand { get; }
    public RelayCommand CommitInlineCommand { get; }
    public RelayCommand CancelInlineCommand { get; }
    public RelayCommand SaveNodeCommand { get; }
    public RelayCommand DeleteNodeCommand { get; }
    public RelayCommand CloseNodePanelCommand { get; }
    public RelayCommand GeneratePlanCommand { get; }
    public RelayCommand AdjustPlanCommand { get; }
    public RelayCommand AcceptPlanCommand { get; }
    public RelayCommand CancelPlanCommand { get; }
    public RelayCommand RaiseLimitCommand { get; }
    public RelayCommand AdjustContentCommand { get; }
    public RelayCommand RequestDeleteExamCommand { get; }
    public RelayCommand CancelDeleteCommand { get; }
    public RelayCommand ConfirmDeleteCommand { get; }
    public RelayCommand DismissNoticeCommand { get; }

    public event Action? Changed;

    public bool IsEmpty => Items.Count == 0;
    public bool HasSelection => _selected is not null;

    public ExamListItemViewModel? Selected
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

    public string DetailTitle { get => _detailTitle; private set => SetProperty(ref _detailTitle, value); }
    public string DetailDate { get => _detailDate; private set => SetProperty(ref _detailDate, value); }
    public string PolicyText { get => _policyText; private set => SetProperty(ref _policyText, value); }
    public string TopicProgress { get => _topicProgress; private set => SetProperty(ref _topicProgress, value); }
    public string ListProgress { get => _listProgress; private set => SetProperty(ref _listProgress, value); }
    public string SessionProgress { get => _sessionProgress; private set => SetProperty(ref _sessionProgress, value); }

    /// <summary>
    /// "2 itens vêm depois de outros" — é ordem sugerida, não impedimento.
    /// </summary>
    public string WaitingSummary
    {
        get => _waitingSummary;
        private set
        {
            if (SetProperty(ref _waitingSummary, value))
            {
                OnPropertyChanged(nameof(HasWaiting));
            }
        }
    }

    public bool HasWaiting => _waitingSummary.Length > 0;

    /// <summary>
    /// Sessões que não batem mais com as regras atuais. Elas continuam ali:
    /// o aplicativo avisa e oferece gerar o plano de novo.
    /// </summary>
    public string ReviewSummary
    {
        get => _reviewSummary;
        private set
        {
            if (SetProperty(ref _reviewSummary, value))
            {
                OnPropertyChanged(nameof(HasReview));
            }
        }
    }

    public bool HasReview => _reviewSummary.Length > 0;

    public bool IsTreeEmpty => Tree.Count == 0;

    // ---------- Formulário da prova ----------

    public bool IsFormOpen { get => _isFormOpen; private set => SetProperty(ref _isFormOpen, value); }
    public string FormHeader { get => _formHeader; private set => SetProperty(ref _formHeader, value); }
    public string FormSubject { get => _formSubject; set => SetProperty(ref _formSubject, value); }
    public string FormTitle { get => _formTitle; set => SetProperty(ref _formTitle, value); }
    public DateTime? FormDate { get => _formDate; set => SetProperty(ref _formDate, value); }

    /// <summary>Meta confortável diária: preferência, não limite.</summary>
    public string FormComfortable { get => _formComfortable; set => SetProperty(ref _formComfortable, value); }

    /// <summary>Máximo diário desta prova: limite duro, mesmo com o dia inteiro livre.</summary>
    public string FormMaxDaily { get => _formMaxDaily; set => SetProperty(ref _formMaxDaily, value); }

    /// <summary>Máximo de uma sessão contínua: limite duro por sessão.</summary>
    public string FormMaxSession { get => _formMaxSession; set => SetProperty(ref _formMaxSession, value); }

    public string FormError
    {
        get => _formError;
        private set
        {
            if (SetProperty(ref _formError, value))
            {
                OnPropertyChanged(nameof(HasFormError));
            }
        }
    }

    public bool HasFormError => _formError.Length > 0;

    // ---------- Editor inline da árvore ----------

    public bool IsInlineOpen => _inlineParentId is not null;

    public StudyNodeType InlineType
    {
        get => _inlineType;
        private set
        {
            if (SetProperty(ref _inlineType, value))
            {
                OnPropertyChanged(nameof(InlineHeader));
                OnPropertyChanged(nameof(InlineTitlePlaceholder));
                OnPropertyChanged(nameof(InlineHint));
                OnPropertyChanged(nameof(IsInlineDurationVisible));
                OnPropertyChanged(nameof(IsInlineListFields));
            }
        }
    }

    public string InlineHeader => InlineType switch
    {
        StudyNodeType.Topic => "Novo assunto",
        StudyNodeType.Theory => "Nova teoria",
        StudyNodeType.ExerciseList => "Nova lista",
        _ => "Novo item",
    };

    /// <summary>O que se espera no campo do nome, dito dentro do próprio campo.</summary>
    public string InlineTitlePlaceholder => InlineType switch
    {
        StudyNodeType.Topic => "nome do assunto",
        StudyNodeType.ExerciseList => "nome da lista",
        _ => "nome do conteúdo",
    };

    /// <summary>Uma linha explicando o que cada campo faz e o que é opcional.</summary>
    public string InlineHint => InlineType switch
    {
        StudyNodeType.Topic =>
            "Um assunto só agrupa — teoria, listas e o mais que houver entram dentro dele.",
        StudyNodeType.ExerciseList =>
            "Duração e questões são opcionais. Sem duração, a lista não entra no plano automático.",
        _ =>
            "A duração é opcional. Sem ela, este item não entra no plano automático.",
    };

    /// <summary>Assunto não tem duração própria: quem tem são os itens dentro dele.</summary>
    public bool IsInlineDurationVisible => InlineType != StudyNodeType.Topic;

    public bool IsInlineListFields => InlineType == StudyNodeType.ExerciseList;

    public string InlineTitle { get => _inlineTitle; set => SetProperty(ref _inlineTitle, value); }
    public string InlineMinutes { get => _inlineMinutes; set => SetProperty(ref _inlineMinutes, value); }
    public string InlineQuestions { get => _inlineQuestions; set => SetProperty(ref _inlineQuestions, value); }

    /// <summary>Marcado por padrão: uma lista quase sempre depende da teoria do assunto.</summary>
    public bool InlineDependOnTheory
    {
        get => _inlineDependOnTheory;
        set => SetProperty(ref _inlineDependOnTheory, value);
    }

    // ---------- Painel do nó ----------

    public StudyNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set
        {
            if (SetProperty(ref _selectedNode, value))
            {
                OnPropertyChanged(nameof(HasSelectedNode));
                OnPropertyChanged(nameof(IsNodeListFields));
            }
        }
    }

    public bool HasSelectedNode => _selectedNode is not null;
    public bool IsNodeListFields => _selectedNode?.IsList ?? false;

    public string NodeMinutes { get => _nodeMinutes; set => SetProperty(ref _nodeMinutes, value); }
    public bool NodeCanSplit { get => _nodeCanSplit; set => SetProperty(ref _nodeCanSplit, value); }
    public string NodeQuestions { get => _nodeQuestions; set => SetProperty(ref _nodeQuestions, value); }

    public string DependencyError
    {
        get => _dependencyError;
        private set
        {
            if (SetProperty(ref _dependencyError, value))
            {
                OnPropertyChanged(nameof(HasDependencyError));
            }
        }
    }

    public bool HasDependencyError => _dependencyError.Length > 0;

    // ---------- Plano de estudo ----------

    public bool IsPlanOpen { get => _isPlanOpen; private set => SetProperty(ref _isPlanOpen, value); }
    public string PlanSubtitle { get => _planSubtitle; private set => SetProperty(ref _planSubtitle, value); }

    public string DeficitText { get => _deficitText; private set => SetProperty(ref _deficitText, value); }

    /// <summary>
    /// A data-limite para começar. É a recomendação principal do plano: o
    /// usuário não precisa escolher dia nenhum.
    /// </summary>
    public string StartText
    {
        get => _startText;
        private set
        {
            if (SetProperty(ref _startText, value))
            {
                OnPropertyChanged(nameof(HasStart));
            }
        }
    }

    public bool HasStart => _startText.Length > 0;

    /// <summary>Enquanto não se pede para ajustar, as datas são só leitura.</summary>
    public bool IsAdjustingPlan
    {
        get => _isAdjustingPlan;
        private set
        {
            if (SetProperty(ref _isAdjustingPlan, value))
            {
                OnPropertyChanged(nameof(IsPlanReadOnly));
            }
        }
    }

    public bool IsPlanReadOnly => !_isAdjustingPlan;

    /// <summary>Não existe plano viável com as restrições atuais.</summary>
    public bool IsImpossible { get => _isImpossible; private set => SetProperty(ref _isImpossible, value); }

    public string? MarginText
    {
        get => _marginText;
        private set
        {
            if (SetProperty(ref _marginText, value))
            {
                OnPropertyChanged(nameof(HasMargin));
            }
        }
    }

    public bool HasMargin => !string.IsNullOrEmpty(_marginText);
    public bool HasPlanConflicts => PlanConflicts.Count > 0;
    public bool HasPlanWaiting => PlanWaiting.Count > 0;
    public bool HasPlanNotes => PlanNotes.Count > 0;
    public bool IsPlanEmpty => PlanRows.Count == 0;

    // ---------- Confirmações ----------

    public bool IsDeleteAskVisible { get => _isDeleteAskVisible; private set => SetProperty(ref _isDeleteAskVisible, value); }
    public string DeleteQuestion { get => _deleteQuestion; private set => SetProperty(ref _deleteQuestion, value); }
    public bool IsNoticeVisible { get => _isNoticeVisible; private set => SetProperty(ref _isNoticeVisible, value); }
    public string NoticeText { get => _noticeText; private set => SetProperty(ref _noticeText, value); }

    // ---------- Carga ----------

    public void Reload()
    {
        var today = _clock.Today;
        var previous = Selected?.Id;
        var previousNode = SelectedNode?.Id;

        Items.Clear();
        foreach (var board in _exams.GetBoards())
        {
            Items.Add(new ExamListItemViewModel(board, today));
        }

        Selected = Items.FirstOrDefault(i => i.Id == previous) ?? Items.FirstOrDefault();

        foreach (var item in Items)
        {
            item.IsSelected = item.Id == Selected?.Id;
        }

        RefreshDetail();

        if (previousNode is not null)
        {
            SelectNode(Tree.FirstOrDefault(n => n.Id == previousNode));
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Select(ExamListItemViewModel item)
    {
        Selected = item;
        foreach (var other in Items)
        {
            other.IsSelected = other.Id == item.Id;
        }

        SelectedNode = null;
        CloseInline();
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        Tree.Clear();
        Sessions.Clear();

        if (Selected is null || _exams.GetBoard(Selected.Id) is not { } board)
        {
            DetailTitle = string.Empty;
            DetailDate = string.Empty;
            PolicyText = string.Empty;
            TopicProgress = string.Empty;
            ListProgress = string.Empty;
            SessionProgress = string.Empty;
            WaitingSummary = string.Empty;
            OnPropertyChanged(nameof(IsTreeEmpty));
            return;
        }

        var today = _clock.Today;
        var policy = board.Exam.Policy;

        DetailTitle = board.Exam.DisplayName;
        DetailDate = $"Data: {LocalDate.ToShortText(board.Exam.ExamDate)} · {TaskLabels.ExamProximity(board.Exam.ExamDate, today)}";
        PolicyText =
            $"Meta {Estimate.Format(policy.ComfortableDailyMinutes)}/dia · " +
            $"máximo {Estimate.Format(policy.MaxDailyMinutes)}/dia · " +
            $"sessão até {Estimate.Format(policy.MaxSessionMinutes)}";

        var topics = board.TopicProgress;
        var lists = board.ListProgress;
        var sessions = board.SessionProgress;

        TopicProgress = topics.HasItems
            ? $"Conteúdos estudados: {topics.Completed}/{topics.Total}"
            : "Nenhum conteúdo cadastrado.";

        ListProgress = lists.HasItems ? $"Listas concluídas: {lists.Completed}/{lists.Total}" : string.Empty;
        SessionProgress = sessions.HasItems
            ? $"Sessões concluídas: {sessions.Completed}/{sessions.Total}"
            : "Nenhuma sessão planejada.";

        WaitingSummary = board.WaitingCount switch
        {
            0 => string.Empty,
            1 => "1 item vem depois de outro — o plano respeita a ordem, e você pode fazê-lo quando quiser",
            _ => $"{board.WaitingCount} itens vêm depois de outros — o plano respeita a ordem, " +
                 "e você pode fazê-los quando quiser",
        };

        BuildTree(board);

        // Revalidar aqui cobre de uma vez a migração, o limite apertado e a
        // dependência nova: os três deixam sessões futuras sem sentido.
        var needingReview = _exams.RevalidateSessions(board.Exam.Id);
        ReviewSummary = needingReview switch
        {
            0 => string.Empty,
            1 => "1 sessão precisa ser revisada: ela não cabe mais nas regras atuais.",
            _ => $"{needingReview} sessões precisam ser revisadas: elas não cabem mais nas regras atuais.",
        };

        foreach (var view in _exams.GetSessionViews(board.Exam.Id))
        {
            var entry = DayEntry.FromSession(view);
            Sessions.Add(new TaskItemViewModel(entry, TaskLabels.For(entry, today)));
        }

        OnPropertyChanged(nameof(IsTreeEmpty));
    }

    /// <summary>Achata a árvore em linhas com recuo, assuntos primeiro e filhos abaixo.</summary>
    private void BuildTree(ExamBoard board)
    {
        void AddLevel(string? parentId, int depth)
        {
            foreach (var node in board.Graph.ChildrenOf(parentId))
            {
                var partial = node.IsPlannable && _exams.HasCompletedSessions(board.Exam.Id, node.Id);
                var state = board.Graph.StateOf(node, partial);
                var after = board.Graph.PendingPrerequisites(node.Id).Select(d => d.Title).ToList();

                Tree.Add(new StudyNodeViewModel(node, state, depth, after, PersistNode));
                AddLevel(node.Id, depth + 1);
            }
        }

        AddLevel(null, 0);
    }

    /// <summary>
    /// Grava domínio e conclusão assim que o usuário toca neles. Concluir pode
    /// destravar outros nós, então a árvore é remontada — mas não a seção
    /// inteira, para não desmontar a linha que está sendo clicada.
    /// </summary>
    private void PersistNode(StudyNodeViewModel row)
    {
        _graph.SetMastery(row.Id, row.Mastery);
        _graph.SetCompleted(row.Id, row.IsCompleted);

        if (Selected is not null && _exams.GetBoard(Selected.Id) is { } board)
        {
            var previousNode = SelectedNode?.Id;
            Tree.Clear();
            BuildTree(board);
            WaitingSummary = board.WaitingCount switch
            {
                0 => string.Empty,
                1 => "1 item vem depois de outro — o plano respeita a ordem, e você pode fazê-lo quando quiser",
                _ => $"{board.WaitingCount} itens vêm depois de outros — o plano respeita a ordem, " +
                     "e você pode fazê-los quando quiser",
            };

            if (previousNode is not null)
            {
                SelectNode(Tree.FirstOrDefault(n => n.Id == previousNode));
            }
        }

        Changed?.Invoke();
    }

    // ---------- Prova ----------

    private void OpenNew()
    {
        _isEditing = false;
        FormHeader = "Nova prova";
        FormSubject = string.Empty;
        FormTitle = string.Empty;
        FormDate = _clock.LocalNow.Date.AddDays(7);

        var policy = ExamStudyPolicy.Default;
        FormComfortable = Estimate.Format(policy.ComfortableDailyMinutes);
        FormMaxDaily = Estimate.Format(policy.MaxDailyMinutes);
        FormMaxSession = Estimate.Format(policy.MaxSessionMinutes);

        FormError = string.Empty;
        IsFormOpen = true;
    }

    private void BeginEdit()
    {
        if (Selected is null || _exams.GetById(Selected.Id) is not { } exam)
        {
            return;
        }

        _isEditing = true;
        FormHeader = "Editar prova";
        FormSubject = exam.Subject;
        FormTitle = exam.Title;
        FormDate = exam.ExamDate.ToDateTime(TimeOnly.MinValue);
        FormComfortable = Estimate.Format(exam.Policy.ComfortableDailyMinutes);
        FormMaxDaily = Estimate.Format(exam.Policy.MaxDailyMinutes);
        FormMaxSession = Estimate.Format(exam.Policy.MaxSessionMinutes);
        FormError = string.Empty;
        IsFormOpen = true;
    }

    private void CommitForm()
    {
        var date = FormDate is { } d ? DateOnly.FromDateTime(d) : _clock.Today;

        var policy = new ExamStudyPolicy(
            Estimate.TryParse(FormComfortable) ?? 0,
            Estimate.TryParse(FormMaxDaily) ?? 0,
            Estimate.TryParse(FormMaxSession) ?? 0);

        if (policy.Validate() is { } error)
        {
            FormError = error;
            return;
        }

        var ok = _isEditing && Selected is not null
            ? _exams.Update(Selected.Id, FormSubject, FormTitle, date, policy)
            : _exams.Create(FormSubject, FormTitle, date, policy) is not null;

        if (!ok)
        {
            FormError = "Informe ao menos a disciplina.";
            return;
        }

        // Apertar um limite com plano já feito não corrige nada em silêncio:
        // as sessões que passam do novo limite ficam marcadas para revisão.
        if (_isEditing && Selected is not null)
        {
            var beyond = _exams.SessionsBeyondPolicy(Selected.Id);
            if (beyond.Count > 0)
            {
                _exams.FlagForReview(beyond);
                NoticeText =
                    $"O plano atual ultrapassa os novos limites em {beyond.Count} " +
                    $"{(beyond.Count == 1 ? "sessão" : "sessões")}. " +
                    "Elas ficaram marcadas para revisão — gere o plano de novo quando quiser.";
                IsNoticeVisible = true;
            }
        }

        IsFormOpen = false;
        FormError = string.Empty;
        Reload();
        Changed?.Invoke();
    }

    // ---------- Árvore ----------

    /// <summary>
    /// Abre o editor inline para um assunto novo, na raiz da árvore.
    /// <see cref="RootParent"/> marca "sem pai" sem confundir com o editor fechado.
    /// </summary>
    private void AddSubject()
    {
        if (Selected is null)
        {
            return;
        }

        CloseInline();

        InlineType = StudyNodeType.Topic;
        InlineTitle = string.Empty;
        InlineMinutes = string.Empty;
        InlineQuestions = string.Empty;

        _inlineParentId = RootParent;
        OnPropertyChanged(nameof(IsInlineOpen));
    }

    /// <summary>Abre o editor inline embaixo do assunto clicado.</summary>
    public void BeginAdd(StudyNodeViewModel subject, StudyNodeType type)
    {
        CloseInline();

        _inlineParentId = subject.Id;
        InlineType = type;
        InlineTitle = string.Empty;
        InlineMinutes = string.Empty;
        InlineQuestions = string.Empty;
        InlineDependOnTheory = type == StudyNodeType.ExerciseList;

        subject.IsAddingHere = true;
        OnPropertyChanged(nameof(IsInlineOpen));
    }

    public void CloseInline()
    {
        _inlineParentId = null;

        foreach (var row in Tree)
        {
            row.IsAddingHere = false;
        }

        OnPropertyChanged(nameof(IsInlineOpen));
    }

    private void CommitInline()
    {
        if (Selected is null || _inlineParentId is null)
        {
            return;
        }

        var examId = Selected.Id;
        var minutes = Estimate.TryParse(InlineMinutes);
        var questions = int.TryParse(InlineQuestions, out var parsed) ? parsed : (int?)null;

        StudyNode? created;

        if (_inlineParentId == RootParent)
        {
            created = _graph.AddSubject(examId, InlineTitle);
        }
        else if (InlineType == StudyNodeType.ExerciseList)
        {
            created = _graph.AddExerciseList(
                examId, InlineTitle, _inlineParentId, minutes, questions, InlineDependOnTheory);
        }
        else
        {
            created = _graph.AddNode(examId, InlineTitle, InlineType, _inlineParentId, minutes);
        }

        if (created is null)
        {
            return;
        }

        CloseInline();
        RefreshDetail();
        Changed?.Invoke();
    }

    // ---------- Painel do nó ----------

    public void SelectNode(StudyNodeViewModel? node)
    {
        foreach (var row in Tree)
        {
            row.IsSelected = node is not null && row.Id == node.Id;
        }

        SelectedNode = node;
        DependencyError = string.Empty;
        DependencyOptions.Clear();

        if (node is null || Selected is null)
        {
            return;
        }

        NodeMinutes = Estimate.Format(node.EstimatedMinutes) ?? string.Empty;
        NodeCanSplit = node.CanSplit;
        NodeQuestions = node.QuestionCount?.ToString() ?? string.Empty;

        if (!node.IsPlannable)
        {
            return;
        }

        var graph = _graph.GetGraph(Selected.Id);
        var current = graph.DependenciesOf(node.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var other in graph.Nodes.Where(n => n.IsPlannable && n.Id != node.Id).OrderBy(n => n.SortOrder))
        {
            var parent = other.ParentNodeId is null ? null : graph.Find(other.ParentNodeId);

            DependencyOptions.Add(new DependencyOptionViewModel(
                other.Id,
                other.Title,
                parent?.Title ?? string.Empty,
                current.Contains(other.Id),
                ToggleDependency));
        }
    }

    /// <summary>
    /// Liga ou desliga uma dependência. Quando o grafo recusa — ciclo, repetição
    /// — a caixa volta sozinha e o motivo aparece escrito.
    /// </summary>
    private void ToggleDependency(DependencyOptionViewModel option, bool wanted)
    {
        if (Selected is null || SelectedNode is null)
        {
            return;
        }

        if (!wanted)
        {
            _graph.RemoveDependency(SelectedNode.Id, option.Id);
            DependencyError = string.Empty;
            AfterGraphChanged();
            return;
        }

        var result = _graph.AddDependency(Selected.Id, SelectedNode.Id, option.Id);
        if (!result.Accepted)
        {
            option.Revert(false);
            DependencyError = result.Message;
            return;
        }

        DependencyError = string.Empty;
        AfterGraphChanged();
    }

    /// <summary>
    /// Mudar o grafo revalida os bloqueios e as sessões futuras. Uma sessão que
    /// deixou de fazer sentido é marcada para revisão, nunca apagada calada.
    /// </summary>
    private void AfterGraphChanged()
    {
        if (Selected is null)
        {
            return;
        }

        // Mudar a ordem não invalida sessão nenhuma: o próximo plano é que vai
        // reordenar. O que invalida é passar dos limites, e isso a revalidação
        // da recarga já cobre.
        var node = SelectedNode?.Id;
        RefreshDetail();
        SelectNode(Tree.FirstOrDefault(n => n.Id == node));
        Changed?.Invoke();
    }

    private void SaveNode()
    {
        if (SelectedNode is not { } node)
        {
            return;
        }

        _graph.UpdateNode(
            node.Id,
            node.Title,
            node.NodeType,
            Estimate.TryParse(NodeMinutes),
            NodeCanSplit,
            int.TryParse(NodeQuestions, out var questions) ? questions : null);

        var id = node.Id;
        RefreshDetail();
        SelectNode(Tree.FirstOrDefault(n => n.Id == id));
        Changed?.Invoke();
    }

    private void RequestDeleteNode()
    {
        if (SelectedNode is not { } node)
        {
            return;
        }

        var dependent = _exams.CountDependentSessions(node.Id);

        DeleteQuestion = dependent == 0
            ? $"Remover \"{node.Title}\"?"
            : $"Remover \"{node.Title}\"? {dependent} {(dependent == 1 ? "sessão futura será excluída" : "sessões futuras serão excluídas")}.";

        _deleteAction = () =>
        {
            _exams.DeleteNode(node.Id);
            SelectedNode = null;
        };

        IsDeleteAskVisible = true;
    }

    private void RequestDeleteExam()
    {
        if (Selected is null)
        {
            return;
        }

        var id = Selected.Id;
        DeleteQuestion = $"Excluir a prova \"{Selected.Title}\"? A árvore de estudo e as sessões saem junto.";
        _deleteAction = () =>
        {
            _exams.Delete(id);
            Selected = null;
            SelectedNode = null;
        };

        IsDeleteAskVisible = true;
    }

    private void ConfirmDelete()
    {
        _deleteAction?.Invoke();
        _deleteAction = null;
        IsDeleteAskVisible = false;
        Reload();
        Changed?.Invoke();
    }

    public void ToggleSession(TaskItemViewModel item)
    {
        _exams.SetSessionCompleted(item.Id, item.IsCompleted);
        RefreshDetail();
        Changed?.Invoke();
    }

    // ---------- Plano de estudo ----------

    private void GeneratePlan()
    {
        if (Selected is null)
        {
            return;
        }

        _temporaryMaxDaily = 0;
        ShowPlan(_planning.BuildPlan(Selected.Id));
    }

    private void ShowPlan(StudyPlanProposal proposal)
    {
        if (Selected is null)
        {
            return;
        }

        PlanRows.Clear();
        foreach (var session in proposal.Sessions)
        {
            PlanRows.Add(new StudySessionRowViewModel(session));
        }

        PlanWaiting.Clear();
        foreach (var waiting in proposal.Waiting)
        {
            PlanWaiting.Add($"↳ {waiting.Title} — {waiting.Explanation}");
        }

        PlanConflicts.Clear();
        foreach (var conflict in proposal.Conflicts)
        {
            PlanConflicts.Add($"{conflict.Title}: {conflict.Message}");
        }

        PlanNotes.Clear();
        foreach (var note in proposal.Notes)
        {
            PlanNotes.Add(note);
        }

        MarginText = proposal.MarginDate is { } margin
            ? $"{LocalDate.ToShortText(margin)} — revisão / margem antes da prova"
            : null;

        StartText = proposal.RecommendedStart is { } start && start > _clock.Today
            ? $"Comece em {LocalDate.ToShortText(start)}. Esse é o limite: " +
              "começando depois disso, o cronograma aperta."
            : proposal.RecommendedStart is not null
                ? "Para caber no ritmo confortável, é para começar hoje."
                : string.Empty;

        IsAdjustingPlan = false;

        IsImpossible = proposal.IsImpossible;
        DeficitText = proposal.IsImpossible
            ? $"Trabalho restante: {Estimate.Format(proposal.RequiredMinutes)} · " +
              $"capacidade possível até a prova: {Estimate.Format(proposal.PossibleMinutes)} · " +
              $"déficit: {Estimate.Format(proposal.DeficitMinutes)}"
            : string.Empty;

        PlanSubtitle = _temporaryMaxDaily > 0
            ? $"{Selected.Title} · limite diário elevado temporariamente para {Estimate.Format(_temporaryMaxDaily)}"
            : $"{Selected.Title} · prova em {Selected.DateText}";

        OnPropertyChanged(nameof(HasPlanConflicts));
        OnPropertyChanged(nameof(HasPlanWaiting));
        OnPropertyChanged(nameof(HasPlanNotes));
        OnPropertyChanged(nameof(IsPlanEmpty));
        IsPlanOpen = true;
    }

    /// <summary>
    /// Recalcula uma vez com o limite diário elevado, só para este plano. A
    /// política gravada da prova não muda: quem decide alterá-la é o usuário,
    /// no formulário.
    /// </summary>
    private void RaiseLimitTemporarily()
    {
        if (Selected is null || _exams.GetById(Selected.Id) is not { } exam)
        {
            return;
        }

        var board = _exams.GetBoard(Selected.Id);
        if (board is null)
        {
            return;
        }

        var today = _clock.Today;
        var days = Math.Max(1, exam.ExamDate.DayNumber - today.DayNumber - 1);
        var needed = (int)Math.Ceiling(board.RemainingMinutes / (double)days);

        _temporaryMaxDaily = Math.Max(exam.Policy.MaxDailyMinutes, needed);

        var relaxed = new ExamStudyPolicy(
            exam.Policy.ComfortableDailyMinutes,
            _temporaryMaxDaily,
            exam.Policy.MaxSessionMinutes);

        var last = exam.ExamDate > today ? exam.ExamDate.AddDays(-1) : today;

        ShowPlan(StudyPlanner.Plan(
            board.Graph, relaxed, today, exam.ExamDate,
            _planning.DayCapacities(Selected.Id, today, last)));
    }

    private void AcceptPlan()
    {
        if (Selected is null)
        {
            return;
        }

        _planning.ApplyPlan(Selected.Id, PlanRows.Select(r => r.ToSession()).ToList());
        ClosePlan();
        RefreshDetail();
        Changed?.Invoke();
    }

    private void ClosePlan()
    {
        IsPlanOpen = false;
        _temporaryMaxDaily = 0;
        PlanRows.Clear();
        PlanWaiting.Clear();
        PlanConflicts.Clear();
        PlanNotes.Clear();
    }
}
