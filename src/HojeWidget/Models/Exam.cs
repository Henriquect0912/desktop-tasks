namespace HojeWidget.Models;

/// <summary>
/// V0.6 — nível de domínio de um conteúdo. Quatro degraus, escolhidos à mão.
/// Não existe nota prevista nem pontuação mágica de preparo.
/// </summary>
public enum Mastery
{
    NotStudied = 0,
    Weak = 1,
    Fair = 2,
    Good = 3,
}

/// <summary>V0.6 — a prova em si.</summary>
public sealed class Exam
{
    public required string Id { get; init; }

    /// <summary>Disciplina: MPP-31, Cálculo III…</summary>
    public required string Subject { get; set; }

    /// <summary>Título livre: "P1", "Prova final".</summary>
    public required string Title { get; set; }

    public DateOnly ExamDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// V0.7.1 — os limites humanos de estudo desta prova. Eles são coisa
    /// diferente da capacidade geral do dia.
    /// </summary>
    public ExamStudyPolicy Policy { get; set; } = ExamStudyPolicy.Default;

    /// <summary>Como a prova aparece nas listas: "MPP-31 — P1".</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Title) ? Subject : $"{Subject} — {Title}";
}

/// <summary>
/// V0.6 — uma sessão de estudo planejada para um dia. Entra no mesmo ecossistema
/// de Hoje e Esta semana que as tarefas e as ocorrências de rotina.
/// </summary>
public sealed class StudySession
{
    public required string Id { get; init; }
    public required string ExamId { get; init; }

    /// <summary>V0.7.1 — nó da árvore de estudo que esta sessão executa.</summary>
    public string? NodeId { get; set; }

    /// <summary>
    /// V0.7.1 — a sessão deixou de ser compatível com as regras atuais (uma
    /// dependência nova, um limite reduzido) e precisa ser revista. Nada é
    /// apagado em silêncio; ela fica marcada.
    /// </summary>
    public bool NeedsReview { get; set; }

    public required string Title { get; set; }
    public int? EstimatedMinutes { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public bool CanSplit { get; set; } = true;
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>V0.7 — prioridade manual.</summary>
    public Priority Priority { get; set; } = Priority.Normal;

    /// <summary>V0.7 — o usuário escolheu este dia à mão.</summary>
    public bool ManualPlanning { get; set; }

    public long SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Sessão já acompanhada dos dados da prova, para exibição.</summary>
public sealed record StudySessionView(StudySession Session, string ExamName, DateOnly ExamDate, Mastery TopicMastery);
