using System.Drawing;
using HojeWidget.Models;
using HojeWidget.Services.DisplayLayout;

namespace HojeWidget.Tests;

public class DisplaySignatureTests
{
    [Fact]
    public void MesmaConfiguracaoProduzAMesmaAssinatura()
    {
        var first = DisplaySignature.Compute(Monitors.Dual());
        var second = DisplaySignature.Compute(Monitors.Dual());

        Assert.Equal(first, second);
    }

    [Fact]
    public void OrdemDeEnumeracaoNaoAlteraAAssinatura()
    {
        var ordered = DisplaySignature.Compute(new[] { Monitors.Laptop(), Monitors.External() });
        var reversed = DisplaySignature.Compute(new[] { Monitors.External(), Monitors.Laptop() });

        Assert.Equal(ordered, reversed);
    }

    [Fact]
    public void ConfiguracoesDiferentesProduzemAssinaturasDiferentes()
    {
        var single = DisplaySignature.Compute(Monitors.Single());
        var dual = DisplaySignature.Compute(Monitors.Dual());

        Assert.NotEqual(single, dual);
    }

    [Fact]
    public void MudarAEscalaMudaAAssinatura()
    {
        var at150 = DisplaySignature.Compute(new[] { Monitors.Laptop(1.5) });
        var at100 = DisplaySignature.Compute(new[] { Monitors.Laptop(1.0) });

        Assert.NotEqual(at150, at100);
    }

    [Fact]
    public void MudarAResolucaoMudaAAssinatura()
    {
        var original = DisplaySignature.Compute(Monitors.Single());
        var lower = DisplaySignature.Compute(new[]
        {
            new MonitorInfo(
                @"\\.\DISPLAY1",
                new Rectangle(0, 0, 1600, 900),
                new Rectangle(0, 0, 1600, 852),
                1.5,
                IsPrimary: true),
        });

        Assert.NotEqual(original, lower);
    }

    [Fact]
    public void AssinaturaEUmHashHexadecimalEstavel()
    {
        var signature = DisplaySignature.Compute(Monitors.Single());

        Assert.Equal(64, signature.Length);
        Assert.All(signature, c => Assert.True(char.IsAsciiHexDigitLower(c)));
    }
}
