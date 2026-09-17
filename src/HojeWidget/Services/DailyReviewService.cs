using HojeWidget.Services.Database;
using HojeWidget.Services.System;

namespace HojeWidget.Services;

/// <summary>
/// Aviso de revisao: aparece na primeira ativacao do widget em cada data local
/// e nunca reaparece depois de confirmado naquela data, mesmo apos reinicio.
/// </summary>
public sealed class DailyReviewService
{
    private readonly DailyReviewRepository _repository;
    private readonly IClock _clock;

    public DailyReviewService(DailyReviewRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>Deve mostrar o aviso na data local de hoje?</summary>
    public bool ShouldShow() => !_repository.IsDismissed(_clock.LocalDate);

    public bool ShouldShow(string localDate) => !_repository.IsDismissed(localDate);

    /// <summary>Grava a confirmacao da data local atual.</summary>
    public void Dismiss() => Dismiss(_clock.LocalDate);

    public void Dismiss(string localDate) => _repository.Dismiss(localDate, _clock.UtcNow);
}
