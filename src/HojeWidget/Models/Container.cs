namespace HojeWidget.Models;

/// <summary>
/// Os dois tipos de "todo maior" da V0.5.
///
/// Eles compartilham a mesma infraestrutura, mas continuam sendo conceitos
/// separados para o usuário: um projeto acompanha uma iniciativa longa, uma
/// entrega tem poucos passos e um resultado final definido.
/// </summary>
public enum ContainerKind
{
    /// <summary>Iniciativa maior, com várias etapas ao longo do tempo.</summary>
    Project = 0,

    /// <summary>Escopo menor, poucos passos rápidos, resultado final definido.</summary>
    Delivery = 1,
}

/// <summary>
/// Projeto ou Entrega. As subtarefas são <see cref="TaskItem"/> com
/// <see cref="TaskItem.ParentId"/> apontando para cá — por isso existe apenas
/// um nível de subtarefa: o pai nunca é outra tarefa.
/// </summary>
public sealed class Container
{
    public required string Id { get; init; }
    public required ContainerKind Kind { get; set; }
    public required string Title { get; set; }

    /// <summary>Prazo do todo. É a data final usada pela distribuição automática.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Concluir é sempre uma decisão do usuário: nem a última subtarefa encerra
    /// o projeto sozinha.
    /// </summary>
    public bool IsCompleted { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>V0.7 — prioridade manual, um fator entre vários.</summary>
    public Priority Priority { get; set; } = Priority.Normal;

    public long SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public string KindLabel => Kind == ContainerKind.Project ? "Projeto" : "Entrega";
}
