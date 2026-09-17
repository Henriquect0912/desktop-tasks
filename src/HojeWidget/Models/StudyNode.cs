namespace HojeWidget.Models;

/// <summary>
/// V0.7.1 — o que um nó da árvore de estudo é.
///
/// <see cref="Topic"/> é o assunto que agrupa ("Torção"); os outros são folhas,
/// as coisas que de fato se estudam.
/// </summary>
public enum StudyNodeType
{
    /// <summary>Assunto: agrupa teoria, listas e o mais que houver.</summary>
    Topic = 0,

    Theory = 1,
    ExerciseList = 2,
    Review = 3,
    Custom = 4,
}

/// <summary>
/// V0.7.1 — um nó da árvore de estudo de uma prova.
///
/// A hierarquia (<see cref="ParentNodeId"/>) é só organização visual. Quem manda
/// na ordem de execução é o grafo de dependências, que é outra coisa: mover um
/// nó de lugar na árvore não cria nem remove dependência nenhuma.
/// </summary>
public sealed class StudyNode
{
    public required string Id { get; init; }
    public required string ExamId { get; init; }

    /// <summary>Assunto a que este nó pertence. Null é um nó de raiz.</summary>
    public string? ParentNodeId { get; set; }

    public StudyNodeType NodeType { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    /// <summary>Null significa duração desconhecida — e isso é uma resposta válida.</summary>
    public int? EstimatedMinutes { get; set; }

    public bool CanSplit { get; set; } = true;

    /// <summary>Só faz sentido em folhas de conteúdo. Assunto não tem domínio próprio.</summary>
    public Mastery? MasteryLevel { get; set; }

    /// <summary>Preservado da V0.6 para listas de exercícios.</summary>
    public int? QuestionCount { get; set; }

    public long SortOrder { get; set; }

    /// <summary>
    /// Concluído de verdade. É a única coisa que destrava um dependente —
    /// nem data planejada, nem sessão criada, nem sessão parcialmente feita.
    /// </summary>
    public bool IsCompleted { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public bool IsSubject => NodeType == StudyNodeType.Topic;

    /// <summary>Assunto não gera sessão: ele só agrupa o que gera.</summary>
    public bool IsPlannable => !IsSubject;

    public string TypeLabel => NodeType switch
    {
        StudyNodeType.Topic => "Assunto",
        StudyNodeType.Theory => "Teoria",
        StudyNodeType.ExerciseList => "Lista",
        StudyNodeType.Review => "Revisão",
        _ => "Outro",
    };
}

/// <summary>
/// V0.7.1 — uma aresta do grafo: <see cref="NodeId"/> só pode ser executado
/// depois que <see cref="DependsOnNodeId"/> estiver concluído.
/// </summary>
public sealed class StudyDependency
{
    public required string Id { get; init; }
    public required string ExamId { get; init; }
    public required string NodeId { get; init; }
    public required string DependsOnNodeId { get; init; }
}

/// <summary>
/// V0.7.1 — os limites humanos de estudo de uma prova.
///
/// Eles são coisa diferente da capacidade geral do dia. Ter 8h livres não
/// significa conseguir estudar 8h da mesma matéria.
/// </summary>
public sealed record ExamStudyPolicy(
    int ComfortableDailyMinutes,
    int MaxDailyMinutes,
    int MaxSessionMinutes)
{
    /// <summary>Padrões da especificação, mostrados ao usuário — nunca invisíveis.</summary>
    public static readonly ExamStudyPolicy Default = new(120, 180, 90);

    /// <summary>
    /// A meta confortável é uma preferência; o máximo diário e o máximo por
    /// sessão são limites duros. Por isso a meta nunca pode passar do máximo, e
    /// uma sessão nunca pode passar do que cabe no dia.
    /// </summary>
    public string? Validate()
    {
        if (ComfortableDailyMinutes <= 0 || MaxDailyMinutes <= 0 || MaxSessionMinutes <= 0)
        {
            return "Os três limites de estudo precisam ser maiores que zero.";
        }

        if (ComfortableDailyMinutes > MaxDailyMinutes)
        {
            return "A meta confortável não pode ser maior que o máximo diário.";
        }

        if (MaxSessionMinutes > MaxDailyMinutes)
        {
            return "O máximo por sessão não pode ser maior que o máximo diário.";
        }

        return null;
    }

    public bool IsValid => Validate() is null;
}
