using System.Collections.ObjectModel;
using HojeWidget.Domain;
using HojeWidget.Domain.Planning;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.Services.System;

namespace HojeWidget.ViewModels;

/// <summary>Uma linha do plano proposto, com a data ainda editável.</summary>
public sealed class ProposalRowViewModel : ObservableObject
{
    private DateTime _date;

    public ProposalRowViewModel(PlannedSlot slot)
    {
        ItemId = slot.ItemId;
        Title = slot.Title;
        Minutes = slot.Minutes;
        SessionIndex = slot.SessionIndex;
        SessionCount = slot.SessionCount;
        _date = slot.Date.ToDateTime(TimeOnly.MinValue);
    }

    public string ItemId { get; }
    public string Title { get; }
    public int? Minutes { get; }
    public int SessionIndex { get; }
    public int SessionCount { get; }

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

    public string DetailText
    {
        get
        {
            var parts = new List<string>(2);
            if (Estimate.Format(Minutes) is { } estimate)
            {
                parts.Add(estimate);
            }

            if (SessionCount > 1)
            {
                parts.Add($"sessão {SessionIndex} de {SessionCount}");
            }

            return string.Join(" · ", parts);
        }
    }

    public bool HasDetail => DetailText.Length > 0;

    public PlannedSlot ToSlot() => new(ItemId, Title, DateOnly.FromDateTime(_date), Minutes, SessionIndex, SessionCount);
}

/// <summary>
/// A tela do plano sugerido, usada tanto pela distribuição de um projeto quanto
/// pelo plano de estudo de uma prova.
///
/// Existe para uma coisa só: nada é aplicado antes de o usuário ver e concordar.
/// </summary>
public sealed class DistributionProposalViewModel : ObservableObject
{
    private Action<IReadOnlyList<PlannedSlot>>? _onAccepted;
    private bool _isOpen;
    private bool _isAdjusting;
    private string _header = "PLANO SUGERIDO";
    private string _subtitle = string.Empty;
    private string? _marginText;
    private string _startText = string.Empty;

    public DistributionProposalViewModel()
    {
        AcceptCommand = new RelayCommand(Accept);
        AdjustCommand = new RelayCommand(() => IsAdjusting = true);
        CancelCommand = new RelayCommand(Close);
    }

    public ObservableCollection<ProposalRowViewModel> Rows { get; } = new();

    public ObservableCollection<string> Conflicts { get; } = new();

    public RelayCommand AcceptCommand { get; }
    public RelayCommand AdjustCommand { get; }
    public RelayCommand CancelCommand { get; }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    /// <summary>Enquanto não se pede para ajustar, o plano é só leitura.</summary>
    public bool IsAdjusting
    {
        get => _isAdjusting;
        private set
        {
            if (SetProperty(ref _isAdjusting, value))
            {
                OnPropertyChanged(nameof(IsReadOnly));
            }
        }
    }

    public bool IsReadOnly => !_isAdjusting;

    public string Header
    {
        get => _header;
        private set => SetProperty(ref _header, value);
    }

    public string Subtitle
    {
        get => _subtitle;
        private set => SetProperty(ref _subtitle, value);
    }

    /// <summary>"09/09 — Margem de segurança", quando a margem foi preservada.</summary>
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

    /// <summary>
    /// A data-limite para começar. É a recomendação principal do plano: o usuário
    /// não escolhe dia nenhum, só confere.
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

    public bool HasConflicts => Conflicts.Count > 0;

    public bool IsEmpty => Rows.Count == 0;

    public void Open(
        string subtitle,
        DistributionPlan plan,
        DateOnly today,
        Action<IReadOnlyList<PlannedSlot>> onAccepted)
    {
        Header = "PLANO SUGERIDO";
        Subtitle = subtitle;
        _onAccepted = onAccepted;
        IsAdjusting = false;

        Rows.Clear();
        foreach (var slot in plan.Slots)
        {
            Rows.Add(new ProposalRowViewModel(slot));
        }

        Conflicts.Clear();

        if (!plan.MarginPreserved && plan.Slots.Count > 0)
        {
            Conflicts.Add("Não sobrou dia de margem antes do prazo: o plano usa todos os dias disponíveis.");
        }

        foreach (var conflict in plan.Conflicts)
        {
            Conflicts.Add($"{conflict.Title}: {conflict.Message}");
        }

        MarginText = plan is { MarginPreserved: true, MarginDate: { } margin }
            ? $"{LocalDate.ToShortText(margin)} — Margem de segurança"
            : null;

        StartText = plan.RecommendedStart is { } start && start > today
            ? $"Comece em {LocalDate.ToShortText(start)}. Esse é o limite: " +
              "começando depois disso, o cronograma aperta."
            : plan.RecommendedStart is not null
                ? "Para caber antes do prazo, é para começar hoje."
                : string.Empty;

        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(IsEmpty));
        IsOpen = true;
    }

    private void Accept()
    {
        var slots = Rows.Select(r => r.ToSlot()).ToList();
        var callback = _onAccepted;
        Close();
        callback?.Invoke(slots);
    }

    private void Close()
    {
        IsOpen = false;
        _onAccepted = null;
        Rows.Clear();
        Conflicts.Clear();
    }
}

/// <summary>Uma linha do plano do dia, com a explicação de por que está ali.</summary>
public sealed class PlanItemViewModel : ObservableObject
{
    public PlanItemViewModel(ScoredItem scored, int position)
    {
        Id = scored.Item.Id;
        Kind = scored.Item.Kind;
        Title = scored.Item.Title;
        Explanation = scored.Explanation;
        EstimateText = Estimate.Format(scored.Item.EstimatedMinutes) ?? "sem estimativa";
        Position = position;
        ParentTitle = scored.Item.ParentTitle;
        CanReschedule = scored.Item.Kind != PlannableKind.Routine;
    }

    public string Id { get; }
    public PlannableKind Kind { get; }
    public string Title { get; }

    /// <summary>Por que este item está alto na lista. Nunca um número solto.</summary>
    public string Explanation { get; }

    public string EstimateText { get; }
    public int Position { get; }
    public string PositionText => $"{Position}.";
    public string? ParentTitle { get; }
    public bool HasParent => !string.IsNullOrEmpty(ParentTitle);

    /// <summary>Ocorrência de rotina não sai do dia: quem manda é a regra da rotina.</summary>
    public bool CanReschedule { get; }
}

/// <summary>Uma mudança de dia proposta, esperando aceite.</summary>
public sealed class MoveViewModel
{
    public MoveViewModel(ProposedMove move)
    {
        Move = move;
        Title = move.Title;
        Text = move.From is { } from
            ? $"{LocalDate.ToShortText(from)} → {LocalDate.ToShortText(move.To)}"
            : LocalDate.ToShortText(move.To);
        Reason = move.Reason;
    }

    public ProposedMove Move { get; }
    public string Title { get; }
    public string Text { get; }
    public string Reason { get; }
}

/// <summary>Carga de um dia do horizonte, para a barra de sobrecarga.</summary>
public sealed class DayLoadViewModel
{
    public DayLoadViewModel(DayLoad load, DateOnly today)
    {
        Date = load.Date;
        DayText = load.Date == today
            ? "hoje"
            : $"{LocalDate.DayOfWeekShortName(load.Date)} {LocalDate.ToShortText(load.Date)}";

        LoadText = load switch
        {
            { IsRestDay: true } => "dia sem planejamento automático",
            { PlannedMinutes: 0 } => $"livre · {Estimate.Format(load.CapacityMinutes)}",
            _ => $"{Estimate.Format(load.PlannedMinutes)} de {Estimate.Format(load.CapacityMinutes)}",
        };

        IsOverloaded = load.IsOverloaded;
        IsRestDay = load.IsRestDay;
        Fraction = load.CapacityMinutes == 0
            ? 0
            : Math.Min(1.5, (double)load.PlannedMinutes / load.CapacityMinutes);

        ItemCountText = load.ItemCount == 1 ? "1 atividade" : $"{load.ItemCount} atividades";
    }

    public DateOnly Date { get; }
    public string DayText { get; }
    public string LoadText { get; }
    public string ItemCountText { get; }
    public bool IsOverloaded { get; }
    public bool IsRestDay { get; }
    public double Fraction { get; }

    /// <summary>Largura da barra, em pontos, com 180 de trilho.</summary>
    public double BarWidth => Math.Max(0, Math.Min(180, 180 * Fraction));
}

/// <summary>
/// V0.7 — a seção "Plano do dia".
///
/// Mostra a ordem sugerida com a explicação de cada item, a carga contra a
/// capacidade, a sobrecarga quando existe e os conflitos que não têm solução.
/// Toda proposta passa por um botão: o motor nunca replaneja sozinho.
/// </summary>
public sealed class DailyPlanViewModel : ObservableObject
{
    private readonly PlanningService _planning;
    private readonly IClock _clock;

    private string _loadText = string.Empty;
    private string _capacityText = string.Empty;
    private bool _isOverloaded;
    private string _overloadText = string.Empty;
    private bool _isAllDone;
    private bool _isTomorrowVisible;
    private string _tomorrowText = string.Empty;
    private bool _isMoveProposalOpen;
    private string _moveProposalHeader = string.Empty;
    private string _carriedForwardText = string.Empty;

    public DailyPlanViewModel(PlanningService planning, IClock clock)
    {
        _planning = planning;
        _clock = clock;

        RedistributeCommand = new RelayCommand(OpenRedistribution);
        AcceptMovesCommand = new RelayCommand(AcceptMoves);
        CancelMovesCommand = new RelayCommand(() => IsMoveProposalOpen = false);
        ShowTomorrowCommand = new RelayCommand(ToggleTomorrow);
    }

    public ObservableCollection<PlanItemViewModel> Items { get; } = new();
    public ObservableCollection<DayLoadViewModel> Days { get; } = new();
    public ObservableCollection<string> Conflicts { get; } = new();
    public ObservableCollection<MoveViewModel> Moves { get; } = new();
    public ObservableCollection<PlanItemViewModel> Tomorrow { get; } = new();

    public RelayCommand RedistributeCommand { get; }
    public RelayCommand AcceptMovesCommand { get; }
    public RelayCommand CancelMovesCommand { get; }
    public RelayCommand ShowTomorrowCommand { get; }

    /// <summary>Disparado quando algo foi realmente gravado e as outras telas precisam recarregar.</summary>
    public event Action? Changed;

    public bool IsEmpty => Items.Count == 0;

    public string LoadText
    {
        get => _loadText;
        private set => SetProperty(ref _loadText, value);
    }

    public string CapacityText
    {
        get => _capacityText;
        private set => SetProperty(ref _capacityText, value);
    }

    public bool IsOverloaded
    {
        get => _isOverloaded;
        private set => SetProperty(ref _isOverloaded, value);
    }

    public string OverloadText
    {
        get => _overloadText;
        private set => SetProperty(ref _overloadText, value);
    }

    /// <summary>Tudo concluído. Não puxa trabalho de amanhã por conta própria.</summary>
    public bool IsAllDone
    {
        get => _isAllDone;
        private set => SetProperty(ref _isAllDone, value);
    }

    public bool IsTomorrowVisible
    {
        get => _isTomorrowVisible;
        private set => SetProperty(ref _isTomorrowVisible, value);
    }

    public string TomorrowText
    {
        get => _tomorrowText;
        private set => SetProperty(ref _tomorrowText, value);
    }

    public bool HasConflicts => Conflicts.Count > 0;

    public bool IsMoveProposalOpen
    {
        get => _isMoveProposalOpen;
        private set => SetProperty(ref _isMoveProposalOpen, value);
    }

    public string MoveProposalHeader
    {
        get => _moveProposalHeader;
        private set => SetProperty(ref _moveProposalHeader, value);
    }

    /// <summary>
    /// O que veio de dias anteriores. Trazer é automático — pendência não expira —,
    /// mas o usuário fica sabendo que a lista de hoje mudou por isso.
    /// </summary>
    public string CarriedForwardText
    {
        get => _carriedForwardText;
        private set
        {
            if (SetProperty(ref _carriedForwardText, value))
            {
                OnPropertyChanged(nameof(HasCarriedForward));
            }
        }
    }

    public bool HasCarriedForward => _carriedForwardText.Length > 0;

    public void Reload()
    {
        var today = _clock.Today;
        var result = _planning.Plan();
        var plan = _planning.TodayPlan();

        Items.Clear();
        var position = 1;
        foreach (var scored in plan)
        {
            Items.Add(new PlanItemViewModel(scored, position++));
        }

        Days.Clear();
        foreach (var day in result.Days.Take(8))
        {
            Days.Add(new DayLoadViewModel(day, today));
        }

        Conflicts.Clear();
        foreach (var conflict in result.Conflicts)
        {
            Conflicts.Add(conflict.Message);
        }

        var capacity = result.TodayCapacityMinutes;
        var load = result.TodayPlannedMinutes;

        LoadText = Items.Count == 0
            ? "Nada planejado para hoje."
            : $"{Items.Count} {(Items.Count == 1 ? "atividade" : "atividades")} · {Estimate.Load(load)}";

        CapacityText = capacity == 0
            ? "Hoje está marcado como dia sem planejamento automático."
            : $"Capacidade de hoje: {Estimate.Format(capacity)}";

        IsOverloaded = capacity > 0 && load > capacity;
        OverloadText = IsOverloaded
            ? $"Carga planejada: {Estimate.Format(load)} · Capacidade: {Estimate.Format(capacity)}"
            : string.Empty;

        IsAllDone = Items.Count == 0;

        Tomorrow.Clear();
        var tomorrow = today.AddDays(1);
        var tomorrowItems = PriorityEngine.Rank(
            _planning.BuildInput().Items.Where(i => i.PlannedDate == tomorrow), today);

        var next = 1;
        foreach (var scored in tomorrowItems)
        {
            Tomorrow.Add(new PlanItemViewModel(scored, next++));
        }

        TomorrowText = Tomorrow.Count == 0 ? "Amanhã também está livre." : string.Empty;

        var carried = _planning.CarriedForwardToday();
        CarriedForwardText = carried switch
        {
            0 => string.Empty,
            1 => "1 atividade que não foi concluída antes veio para hoje.",
            _ => $"{carried} atividades que não foram concluídas antes vieram para hoje.",
        };

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasConflicts));
    }

    private void ToggleTomorrow() => IsTomorrowVisible = !IsTomorrowVisible;

    private void OpenRedistribution() => ShowMoves(
        "Redistribuir os dias sobrecarregados",
        _planning.BuildRedistribution());

    private void ShowMoves(string header, IReadOnlyList<ProposedMove> moves)
    {
        Moves.Clear();
        foreach (var move in moves)
        {
            Moves.Add(new MoveViewModel(move));
        }

        MoveProposalHeader = Moves.Count == 0
            ? "Não há mudança possível que respeite capacidade, prazos e margens."
            : header;

        IsMoveProposalOpen = true;
    }

    private void AcceptMoves()
    {
        if (Moves.Count > 0)
        {
            _planning.ApplyMoves(Moves.Select(m => m.Move).ToList());
        }

        IsMoveProposalOpen = false;
        Reload();
        Changed?.Invoke();
    }
}

/// <summary>Um dia da semana na tela de capacidade.</summary>
public sealed class WeeklyCapacityViewModel : ObservableObject
{
    private string _minutes;

    public WeeklyCapacityViewModel(int index, int minutes)
    {
        Index = index;
        Name = CapacityService.DayName(index);
        _minutes = minutes.ToString();
    }

    public int Index { get; }
    public string Name { get; }

    /// <summary>Aceita "180", "3h", "2h30".</summary>
    public string Minutes
    {
        get => _minutes;
        set
        {
            if (SetProperty(ref _minutes, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public int ParsedMinutes => Estimate.TryParse(_minutes) ?? 0;

    public string Summary => ParsedMinutes == 0
        ? "sem planejamento automático"
        : Estimate.Format(ParsedMinutes);
}

/// <summary>Uma exceção de capacidade por data.</summary>
public sealed class CapacityOverrideViewModel
{
    public CapacityOverrideViewModel(DateOnly date, int minutes)
    {
        Date = date;
        DateText = LocalDate.ToShortText(date);
        Summary = minutes == 0 ? "sem planejamento automático" : Estimate.Format(minutes);
    }

    public DateOnly Date { get; }
    public string DateText { get; }
    public string Summary { get; }
}

/// <summary>
/// V0.7 — a seção "Capacidade".
///
/// Zero num dia significa "não planejar automaticamente"; o usuário continua
/// podendo colocar o que quiser lá à mão.
/// </summary>
public sealed class CapacityViewModel : ObservableObject
{
    private readonly CapacityService _capacity;
    private readonly IClock _clock;

    private DateTime? _overrideDate;
    private string _overrideMinutes = "2h";

    public CapacityViewModel(CapacityService capacity, IClock clock)
    {
        _capacity = capacity;
        _clock = clock;

        SaveWeeklyCommand = new RelayCommand(SaveWeekly);
        AddOverrideCommand = new RelayCommand(AddOverride);

        Reload();
    }

    public ObservableCollection<WeeklyCapacityViewModel> Week { get; } = new();
    public ObservableCollection<CapacityOverrideViewModel> Overrides { get; } = new();

    public RelayCommand SaveWeeklyCommand { get; }
    public RelayCommand AddOverrideCommand { get; }

    public event Action? Changed;

    public bool IsOverridesEmpty => Overrides.Count == 0;

    public DateTime? OverrideDate
    {
        get => _overrideDate;
        set => SetProperty(ref _overrideDate, value);
    }

    public string OverrideMinutes
    {
        get => _overrideMinutes;
        set => SetProperty(ref _overrideMinutes, value);
    }

    public void Reload()
    {
        Week.Clear();
        var minutes = _capacity.WeeklyMinutes();
        for (var i = 0; i < 7; i++)
        {
            Week.Add(new WeeklyCapacityViewModel(i, minutes[i]));
        }

        Overrides.Clear();
        foreach (var (date, value) in _capacity.Overrides())
        {
            Overrides.Add(new CapacityOverrideViewModel(date, value));
        }

        OverrideDate ??= _clock.LocalNow.Date;
        OnPropertyChanged(nameof(IsOverridesEmpty));
    }

    private void SaveWeekly()
    {
        foreach (var day in Week)
        {
            _capacity.SetWeekly(day.Index, day.ParsedMinutes);
        }

        Reload();
        Changed?.Invoke();
    }

    private void AddOverride()
    {
        if (OverrideDate is not { } date)
        {
            return;
        }

        _capacity.SetOverride(DateOnly.FromDateTime(date), Estimate.TryParse(OverrideMinutes) ?? 0);
        Reload();
        Changed?.Invoke();
    }

    public void RemoveOverride(DateOnly date)
    {
        _capacity.RemoveOverride(date);
        Reload();
        Changed?.Invoke();
    }
}
