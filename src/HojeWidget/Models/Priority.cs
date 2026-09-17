namespace HojeWidget.Models;

/// <summary>
/// V0.7 — prioridade manual. É apenas um fator entre vários no motor de
/// planejamento; sozinha ela não decide a ordem do dia.
/// </summary>
public enum Priority
{
    Low = 0,
    Normal = 1,
    High = 2,
}

/// <summary>Textos de prioridade em português.</summary>
public static class PriorityLabels
{
    public static string Of(Priority priority) => priority switch
    {
        Priority.High => "alta",
        Priority.Low => "baixa",
        _ => "normal",
    };

    public static string Title(Priority priority) => priority switch
    {
        Priority.High => "Alta",
        Priority.Low => "Baixa",
        _ => "Normal",
    };
}
