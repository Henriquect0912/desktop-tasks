namespace HojeWidget.Models;

/// <summary>Estado macro da lista, usado para escolher entre lista, vazio e "tudo concluido".</summary>
public enum ListState
{
    /// <summary>Nenhuma pendente e nenhuma concluida hoje.</summary>
    Empty = 0,

    /// <summary>Ha concluidas hoje e nenhuma pendente.</summary>
    AllDone = 1,

    /// <summary>Ha pelo menos uma pendente.</summary>
    Normal = 2,
}
