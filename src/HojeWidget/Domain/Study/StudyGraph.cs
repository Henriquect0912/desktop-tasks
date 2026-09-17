using HojeWidget.Models;

namespace HojeWidget.Domain.Study;

/// <summary>Em que estado um nó está, para a árvore e para o planejador.</summary>
public enum StudyNodeState
{
    /// <summary>Pronto para ser estudado: pendente e sem pré-requisito em aberto.</summary>
    Pending = 0,

    /// <summary>Começado, mas não terminado — parte das sessões já foi concluída.</summary>
    Partial = 1,

    Completed = 2,

    /// <summary>
    /// Tem pré-requisito ainda pendente. Isso não impede o item de ser planejado
    /// nem de ser feito: serve para o planejador colocá-lo depois do que vem antes.
    /// </summary>
    Waiting = 3,
}

/// <summary>
/// V0.7.1 — a árvore e o grafo de uma prova, em memória e sem dependência de banco.
///
/// São duas estruturas sobre os mesmos nós, e elas não se confundem:
///
/// - a **árvore** (<c>ParentNodeId</c>) é como o usuário organiza o que cai na
///   prova; ela existe só para a tela;
/// - o **grafo** (<c>StudyDependency</c>) diz em que **ordem** as coisas fazem
///   sentido, é dirigido e precisa continuar acíclico.
///
/// A dependência é uma regra de ordenação, não uma tranca: um item com
/// pré-requisito pendente continua sendo planejado e continua podendo ser feito
/// a qualquer momento — o planejador só cuida de colocá-lo depois do que vem
/// antes dele.
///
/// Mover um nó de assunto muda a árvore e não toca no grafo.
/// </summary>
public sealed class StudyGraph
{
    private readonly Dictionary<string, StudyNode> _byId;
    private readonly Dictionary<string, List<string>> _dependsOn;
    private readonly Dictionary<string, List<string>> _unlocks;

    public StudyGraph(IReadOnlyList<StudyNode> nodes, IReadOnlyList<StudyDependency> dependencies)
    {
        Nodes = nodes;
        Dependencies = dependencies;

        _byId = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        _dependsOn = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        _unlocks = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var edge in dependencies)
        {
            if (!_byId.ContainsKey(edge.NodeId) || !_byId.ContainsKey(edge.DependsOnNodeId))
            {
                continue;
            }

            Add(_dependsOn, edge.NodeId, edge.DependsOnNodeId);
            Add(_unlocks, edge.DependsOnNodeId, edge.NodeId);
        }
    }

    public IReadOnlyList<StudyNode> Nodes { get; }

    public IReadOnlyList<StudyDependency> Dependencies { get; }

    public StudyNode? Find(string id) => _byId.TryGetValue(id, out var node) ? node : null;

    /// <summary>Filhos diretos de um assunto, ou os nós de raiz quando o pai é null.</summary>
    public IReadOnlyList<StudyNode> ChildrenOf(string? parentId)
        => Nodes.Where(n => string.Equals(n.ParentNodeId, parentId, StringComparison.Ordinal))
                .OrderBy(n => n.SortOrder)
                .ToList();

    /// <summary>Ids dos quais este nó depende.</summary>
    public IReadOnlyList<string> DependenciesOf(string nodeId)
        => _dependsOn.TryGetValue(nodeId, out var list) ? list : Array.Empty<string>();

    /// <summary>Ids que este nó destrava ao ser concluído.</summary>
    public IReadOnlyList<string> UnlockedBy(string nodeId)
        => _unlocks.TryGetValue(nodeId, out var list) ? list : Array.Empty<string>();

    /// <summary>
    /// Pré-requisitos que ainda não foram concluídos. Lista vazia significa
    /// caminho livre; é esta lista que a interface mostra em "Depois de: …".
    /// </summary>
    public IReadOnlyList<StudyNode> PendingPrerequisites(string nodeId)
        => DependenciesOf(nodeId)
            .Select(Find)
            .Where(n => n is { IsCompleted: false })
            .Select(n => n!)
            .ToList();

    /// <summary>
    /// O caminho está livre: nenhum pré-requisito pendente. Ter data planejada,
    /// ter sessão criada ou ter sessão parcialmente feita não conta como concluir.
    /// </summary>
    public bool IsReady(string nodeId)
    {
        var node = Find(nodeId);
        return node is { IsCompleted: false } && PendingPrerequisites(nodeId).Count == 0;
    }

    public bool IsReady(StudyNode node) => IsReady(node.Id);

    /// <summary>
    /// Tudo o que ainda falta fazer, na ordem em que faz sentido fazer.
    ///
    /// Um item com pré-requisito pendente entra na lista do mesmo jeito — só vem
    /// depois dele. É isso que diferencia ordenar de trancar.
    /// </summary>
    public IReadOnlyList<StudyNode> PlannableNodes()
        => Nodes.Where(n => n.IsPlannable && !n.IsCompleted)
                .OrderBy(DependencyDepth)
                .ThenBy(n => n.SortOrder)
                .ToList();

    /// <summary>
    /// Quantos pré-requisitos pendentes existem em cadeia antes deste nó.
    /// Ordenar por esta profundidade já dá uma ordem topológica: um item sempre
    /// vem depois de tudo de que depende.
    /// </summary>
    public int DependencyDepth(StudyNode node) => Depth(node.Id, new HashSet<string>(StringComparer.Ordinal));

    private int Depth(string nodeId, HashSet<string> visiting)
    {
        if (!visiting.Add(nodeId))
        {
            return 0;
        }

        var depth = 0;
        foreach (var prerequisite in PendingPrerequisites(nodeId))
        {
            depth = Math.Max(depth, 1 + Depth(prerequisite.Id, visiting));
        }

        visiting.Remove(nodeId);
        return depth;
    }

    /// <summary>Nós que esperam a vez de outro. Aparecem normalmente, com a ordem dita.</summary>
    public IReadOnlyList<StudyNode> WaitingNodes()
        => Nodes.Where(n => n.IsPlannable && !n.IsCompleted && PendingPrerequisites(n.Id).Count > 0)
                .OrderBy(n => n.SortOrder)
                .ToList();

    public StudyNodeState StateOf(StudyNode node, bool hasCompletedSessions = false)
    {
        if (node.IsCompleted)
        {
            return StudyNodeState.Completed;
        }

        if (PendingPrerequisites(node.Id).Count > 0)
        {
            return StudyNodeState.Waiting;
        }

        return hasCompletedSessions ? StudyNodeState.Partial : StudyNodeState.Pending;
    }

    /// <summary>
    /// Quantos nós esta conclusão destrava, contando em cadeia. É uma preferência
    /// suave do planejador: entre dois itens igualmente possíveis, prefere o que
    /// abre caminho — nunca a ponto de furar uma restrição dura.
    /// </summary>
    public int UnlockPotential(string nodeId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(UnlockedBy(nodeId));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!seen.Add(current))
            {
                continue;
            }

            foreach (var next in UnlockedBy(current))
            {
                queue.Enqueue(next);
            }
        }

        return seen.Count;
    }

    private static void Add(Dictionary<string, List<string>> map, string key, string value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<string>();
            map[key] = list;
        }

        if (!list.Contains(value, StringComparer.Ordinal))
        {
            list.Add(value);
        }
    }
}

/// <summary>Por que uma dependência não pode ser criada.</summary>
public enum DependencyRejection
{
    None = 0,
    SelfDependency = 1,
    Duplicate = 2,
    Cycle = 3,
    UnknownNode = 4,
}

/// <summary>
/// V0.7.1 — validação das arestas do grafo.
///
/// O grafo precisa continuar acíclico: se `A → B → C → A` fosse aceito, nenhum
/// dos três jamais ficaria elegível e o planejador travaria em silêncio.
/// </summary>
public static class DependencyValidation
{
    public static DependencyRejection Check(StudyGraph graph, string nodeId, string dependsOnId)
    {
        if (string.Equals(nodeId, dependsOnId, StringComparison.Ordinal))
        {
            return DependencyRejection.SelfDependency;
        }

        if (graph.Find(nodeId) is null || graph.Find(dependsOnId) is null)
        {
            return DependencyRejection.UnknownNode;
        }

        if (graph.DependenciesOf(nodeId).Contains(dependsOnId, StringComparer.Ordinal))
        {
            return DependencyRejection.Duplicate;
        }

        // A aresta nova é nodeId → dependsOnId. Ela fecha um ciclo se, saindo de
        // dependsOnId pelas dependências que já existem, dá para voltar a nodeId.
        return Reaches(graph, dependsOnId, nodeId) ? DependencyRejection.Cycle : DependencyRejection.None;
    }

    public static bool IsAllowed(StudyGraph graph, string nodeId, string dependsOnId)
        => Check(graph, nodeId, dependsOnId) == DependencyRejection.None;

    public static string Describe(DependencyRejection rejection, string nodeTitle, string dependsOnTitle)
        => rejection switch
        {
            DependencyRejection.SelfDependency => "Um item não pode depender de si mesmo.",
            DependencyRejection.Duplicate => $"\"{nodeTitle}\" já depende de \"{dependsOnTitle}\".",
            DependencyRejection.Cycle =>
                $"\"{dependsOnTitle}\" já depende de \"{nodeTitle}\", direta ou indiretamente. " +
                "Aceitar isso deixaria os dois travados para sempre.",
            DependencyRejection.UnknownNode => "Um dos itens não existe mais.",
            _ => string.Empty,
        };

    private static bool Reaches(StudyGraph graph, string from, string target)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(from);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (string.Equals(current, target, StringComparison.Ordinal))
            {
                return true;
            }

            if (!seen.Add(current))
            {
                continue;
            }

            foreach (var next in graph.DependenciesOf(current))
            {
                stack.Push(next);
            }
        }

        return false;
    }
}
