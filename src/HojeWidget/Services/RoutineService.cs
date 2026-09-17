using HojeWidget.Domain;
using HojeWidget.Models;
using HojeWidget.Services.Database;
using HojeWidget.Services.System;

namespace HojeWidget.Services;

/// <summary>
/// Rotinas e suas ocorrências.
///
/// Regra central: rotina é diferente de ocorrência. Concluir a ocorrência de um dia
/// nunca encerra a rotina nem transforma essa ocorrência na próxima — cada uma é
/// independente.
/// </summary>
public sealed class RoutineService
{
    /// <summary>
    /// Horizonte de geração. Ocorrências são criadas sob demanda para os próximos
    /// 90 dias e regeneradas quando preciso; nunca se gera anos de futuro.
    /// </summary>
    public const int HorizonDays = 90;

    private readonly RoutineRepository _repository;
    private readonly IClock _clock;

    public RoutineService(RoutineRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public IReadOnlyList<Routine> GetAll() => _repository.GetAll();

    public Routine? GetById(string id) => _repository.GetById(id);

    /// <summary>
    /// Cria a rotina e já gera as ocorrências do horizonte.
    /// Retorna null quando o título é inválido ou a regra está incompleta.
    /// </summary>
    public Routine? Create(
        string? rawTitle,
        RecurrenceType type,
        int daysOfWeek = 0,
        int? intervalDays = null,
        int? dayOfMonth = null,
        DateOnly? startDate = null)
    {
        if (!TaskTitle.TryNormalize(rawTitle, out var title) || !IsValidRule(type, daysOfWeek, intervalDays, dayOfMonth))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var routine = new Routine
        {
            Id = Guid.NewGuid().ToString("n"),
            Title = title,
            RecurrenceType = type,
            StartDate = startDate ?? _clock.Today,
            IntervalDays = type == RecurrenceType.EveryXDays ? intervalDays : null,
            DaysOfWeek = type == RecurrenceType.DaysOfWeek ? daysOfWeek : 0,
            DayOfMonth = type == RecurrenceType.Monthly ? dayOfMonth : null,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _repository.Insert(routine);
        GenerateFor(routine);
        return routine;
    }

    /// <summary>
    /// Altera a regra. O passado é mantido, as futuras pendentes são descartadas e
    /// novas ocorrências são geradas conforme a regra atualizada.
    /// </summary>
    public bool Update(
        string id,
        string? rawTitle,
        RecurrenceType type,
        int daysOfWeek = 0,
        int? intervalDays = null,
        int? dayOfMonth = null)
    {
        if (!TaskTitle.TryNormalize(rawTitle, out var title) || !IsValidRule(type, daysOfWeek, intervalDays, dayOfMonth))
        {
            return false;
        }

        var routine = _repository.GetById(id);
        if (routine is null)
        {
            return false;
        }

        routine.Title = title;
        routine.RecurrenceType = type;
        routine.IntervalDays = type == RecurrenceType.EveryXDays ? intervalDays : null;
        routine.DaysOfWeek = type == RecurrenceType.DaysOfWeek ? daysOfWeek : 0;
        routine.DayOfMonth = type == RecurrenceType.Monthly ? dayOfMonth : null;
        routine.UpdatedAtUtc = _clock.UtcNow;

        _repository.Update(routine);
        _repository.DeleteFuturePending(routine.Id, _clock.Today);
        GenerateFor(routine);
        return true;
    }

    /// <summary>Exclui a rotina. As ocorrências saem junto, por integridade referencial.</summary>
    public void Delete(string id)
    {
        _repository.DeleteFuturePending(id, _clock.Today);
        _repository.Delete(id);
    }

    /// <summary>
    /// Gera as ocorrências que faltam para todas as rotinas ativas, dentro do horizonte.
    /// Idempotente: pode rodar na inicialização e a cada virada de dia sem duplicar nada.
    /// </summary>
    public void GenerateAll()
    {
        foreach (var routine in _repository.GetAll().Where(r => r.IsActive))
        {
            GenerateFor(routine);
        }
    }

    private void GenerateFor(Routine routine)
    {
        var today = _clock.Today;
        var dates = RecurrenceCalculator.Occurrences(routine, today, today.AddDays(HorizonDays));
        _repository.EnsureOccurrences(routine.Id, dates);
    }

    /// <summary>Ocorrências de um dia, com o título da rotina.</summary>
    public IReadOnlyList<RoutineOccurrenceView> GetForDay(DateOnly date)
        => _repository.GetBetween(date, date);

    public IReadOnlyList<RoutineOccurrenceView> GetBetween(DateOnly from, DateOnly to)
        => _repository.GetBetween(from, to);

    /// <summary>
    /// Conclui ou reabre a ocorrência de um dia. A rotina não é afetada:
    /// as próximas ocorrências continuam existindo normalmente.
    /// </summary>
    public bool SetOccurrenceCompleted(string occurrenceId, bool completed)
    {
        var occurrence = _repository.GetOccurrence(occurrenceId);
        if (occurrence is null)
        {
            return false;
        }

        occurrence.IsCompleted = completed;
        occurrence.CompletedAtUtc = completed ? _clock.UtcNow : null;
        _repository.UpdateOccurrence(occurrence);
        return true;
    }

    private static bool IsValidRule(RecurrenceType type, int daysOfWeek, int? intervalDays, int? dayOfMonth) => type switch
    {
        RecurrenceType.Daily => true,
        RecurrenceType.DaysOfWeek => daysOfWeek != 0,
        RecurrenceType.EveryXDays => intervalDays is > 0,
        RecurrenceType.Monthly => dayOfMonth is >= 1 and <= 31,
        _ => false,
    };
}
