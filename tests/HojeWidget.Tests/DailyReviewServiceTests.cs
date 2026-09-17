namespace HojeWidget.Tests;

public class DailyReviewServiceTests
{
    [Fact]
    public void AvisoApareceNaPrimeiraAtivacaoDoDia()
    {
        using var fixture = new TaskFixture();

        Assert.True(fixture.Review.ShouldShow());
    }

    [Fact]
    public void TudoCertoImpedeQueOAvisoVolteNoMesmoDia()
    {
        using var fixture = new TaskFixture();

        fixture.Review.Dismiss();

        Assert.False(fixture.Review.ShouldShow());
    }

    [Fact]
    public void ConfirmacaoSobreviveAoReinicio()
    {
        using var fixture = new TaskFixture();
        fixture.Review.Dismiss();

        fixture.Restart();

        Assert.False(fixture.Review.ShouldShow());
    }

    [Fact]
    public void AvisoVoltaNaPrimeiraAtivacaoDeUmaNovaData()
    {
        using var fixture = new TaskFixture();
        fixture.Review.Dismiss();
        Assert.False(fixture.Review.ShouldShow());

        fixture.Clock.AdvanceDays(1);

        Assert.True(fixture.Review.ShouldShow());
    }

    [Fact]
    public void ConfirmacaoDeUmDiaNaoAfetaOutroDia()
    {
        using var fixture = new TaskFixture();

        fixture.Review.Dismiss("2026-03-10");

        Assert.False(fixture.Review.ShouldShow("2026-03-10"));
        Assert.True(fixture.Review.ShouldShow("2026-03-11"));
    }
}
