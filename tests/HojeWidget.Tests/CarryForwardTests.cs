using HojeWidget.Models;

namespace HojeWidget.Tests;

/// <summary>
/// O que ficou para trás vem para hoje sozinho.
///
/// Pendência não expira desde a V0.1; deixar a data velha só obrigava o usuário
/// a arrastar tudo à mão todo dia. Isto roda na abertura do aplicativo e a cada
/// virada de dia.
/// </summary>
public class CarryForwardTests
{
    private static readonly DateTime Reference = new(2026, 9, 6, 9, 0, 0, DateTimeKind.Local);

    private static RoutineFixture NewFixture() => new(Reference);

    [Fact]
    public void TarefaDeOntemVemParaHoje()
    {
        using var fixture = NewFixture();
        var ontem = fixture.Clock.Today.AddDays(-1);

        var task = fixture.Tasks.Add("Fazer relatório", ontem)!;

        Assert.Equal(1, fixture.Planning.CarryForwardOverdue());
        Assert.Equal(fixture.Clock.Today, fixture.Tasks.GetAll().Single(t => t.Id == task.Id).PlannedDate);
    }

    [Fact]
    public void TarefaDeSemanasAtrasTambemVem()
    {
        using var fixture = NewFixture();

        fixture.Tasks.Add("Antiga", fixture.Clock.Today.AddDays(-21));
        fixture.Tasks.Add("Menos antiga", fixture.Clock.Today.AddDays(-3));

        Assert.Equal(2, fixture.Planning.CarryForwardOverdue());
        Assert.All(fixture.Tasks.GetAll(), t => Assert.Equal(fixture.Clock.Today, t.PlannedDate));
    }

    [Fact]
    public void ConcluidaFuturaESemDataFicamComoEstao()
    {
        using var fixture = NewFixture();
        var ontem = fixture.Clock.Today.AddDays(-1);
        var amanha = fixture.Clock.Today.AddDays(1);

        var feita = fixture.Tasks.Add("Já feita", ontem)!;
        fixture.Tasks.SetCompleted(feita.Id, true);

        var futura = fixture.Tasks.Add("De amanhã", amanha)!;
        var solta = fixture.Tasks.Add("Algum dia", plannedDate: null)!;

        Assert.Equal(0, fixture.Planning.CarryForwardOverdue());

        var all = fixture.Tasks.GetAll().ToDictionary(t => t.Id);
        Assert.Equal(ontem, all[feita.Id].PlannedDate);
        Assert.Equal(amanha, all[futura.Id].PlannedDate);
        Assert.Null(all[solta.Id].PlannedDate);
    }

    [Fact]
    public void OcorrenciaDeRotinaNaoEhArrastada()
    {
        using var fixture = NewFixture();
        fixture.Routines.Create("Academia", RecurrenceType.Daily, startDate: fixture.Clock.Today.AddDays(-3));

        var ontem = fixture.Clock.Today.AddDays(-1);
        var antes = fixture.Routines.GetForDay(ontem).Count;

        fixture.Planning.CarryForwardOverdue();

        // A ocorrência de ontem é de ontem: quem manda no dia dela é a regra da
        // rotina, e trazê-la duplicaria a de hoje.
        Assert.Equal(antes, fixture.Routines.GetForDay(ontem).Count);
        Assert.Single(fixture.Routines.GetForDay(fixture.Clock.Today));
    }

    [Fact]
    public void SessaoDeEstudoAtrasadaTambemVem()
    {
        using var fixture = NewFixture();
        var exam = fixture.Exams.Create("MPP-31", "P1", fixture.Clock.Today.AddDays(6))!;

        var atrasada = fixture.Exams.AddSession(
            exam.Id, "MPP-31 — Lista 1", null, 60, fixture.Clock.Today.AddDays(-2))!;
        var futura = fixture.Exams.AddSession(
            exam.Id, "MPP-31 — Lista 2", null, 60, fixture.Clock.Today.AddDays(2))!;

        Assert.Equal(1, fixture.Planning.CarryForwardOverdue());

        var sessions = fixture.Exams.GetSessions(exam.Id).ToDictionary(s => s.Id);
        Assert.Equal(fixture.Clock.Today, sessions[atrasada.Id].PlannedDate);
        Assert.Equal(fixture.Clock.Today.AddDays(2), sessions[futura.Id].PlannedDate);
    }

    [Fact]
    public void AMarcaDeEscolhaManualEhPreservada()
    {
        using var fixture = NewFixture();
        var ontem = fixture.Clock.Today.AddDays(-1);

        var manual = fixture.Tasks.Add("Escolhida à mão", ontem)!;
        Assert.True(fixture.Tasks.GetAll().Single().ManualPlanning);

        fixture.Planning.CarryForwardOverdue();

        // A decisão continua sendo do usuário: ela só passou a valer para hoje.
        var moved = fixture.Tasks.GetAll().Single(t => t.Id == manual.Id);
        Assert.Equal(fixture.Clock.Today, moved.PlannedDate);
        Assert.True(moved.ManualPlanning);
    }

    [Fact]
    public void OQueVeioEhContadoParaOAvisoDoDia()
    {
        using var fixture = NewFixture();

        fixture.Tasks.Add("Uma", fixture.Clock.Today.AddDays(-1));
        fixture.Tasks.Add("Outra", fixture.Clock.Today.AddDays(-2));

        Assert.Equal(0, fixture.Planning.CarriedForwardToday());

        fixture.Planning.CarryForwardOverdue();
        Assert.Equal(2, fixture.Planning.CarriedForwardToday());

        // O aviso é do dia de hoje, não histórico: amanhã ele já não vale.
        fixture.Clock.AdvanceDays(1);
        Assert.Equal(0, fixture.Planning.CarriedForwardToday());
    }

    [Fact]
    public void RodarDuasVezesNaoMudaNadaNaSegunda()
    {
        using var fixture = NewFixture();
        fixture.Tasks.Add("Fazer relatório", fixture.Clock.Today.AddDays(-1));

        Assert.Equal(1, fixture.Planning.CarryForwardOverdue());
        Assert.Equal(0, fixture.Planning.CarryForwardOverdue());
    }

    [Fact]
    public void ATarefaTrazidaEntraNoPlanoDeHoje()
    {
        using var fixture = NewFixture();
        fixture.Tasks.Add("Fazer relatório", fixture.Clock.Today.AddDays(-1), estimatedMinutes: 60);

        fixture.Planning.CarryForwardOverdue();

        var plan = fixture.Planning.Plan();
        Assert.Single(plan.TodayPlan);
        Assert.Equal(60, plan.TodayPlannedMinutes);

        // E deixa de aparecer como atrasada, porque não está mais atrasada.
        Assert.Empty(fixture.Planning.Unfinished());
    }
}
