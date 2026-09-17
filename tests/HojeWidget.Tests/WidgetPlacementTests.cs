using System.Drawing;
using HojeWidget.Models;
using HojeWidget.Services.DisplayLayout;

namespace HojeWidget.Tests;

public class WidgetPlacementTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SemLayoutSalvoUsaCantoSuperiorDireitoDoPrincipal()
    {
        var monitors = Monitors.Dual();
        var primary = monitors.First(m => m.IsPrimary);

        var placement = WidgetPlacement.Resolve(null, monitors);

        Assert.Equal(primary.DeviceName, placement.Monitor.DeviceName);

        var margin = (int)Math.Round(WidgetPlacement.DefaultMarginDip * primary.Scale);
        var expectedWidth = (int)Math.Round(WidgetPlacement.DefaultWidthDip * primary.Scale);

        Assert.Equal(primary.WorkArea.Right - expectedWidth - margin, placement.Bounds.X);
        Assert.Equal(primary.WorkArea.Y + margin, placement.Bounds.Y);
    }

    [Fact]
    public void TamanhoPadraoRespeitaAEscalaDoMonitor()
    {
        var placement = WidgetPlacement.Resolve(null, new[] { Monitors.Laptop(1.5) });

        Assert.Equal(615, placement.Bounds.Width);  // 410 * 1.5
        Assert.Equal(450, placement.Bounds.Height); // 300 * 1.5
    }

    [Fact]
    public void LayoutSalvoERestauradoNoMonitorCorreto()
    {
        var monitors = Monitors.Dual();
        var external = monitors.First(m => !m.IsPrimary);

        var layout = new StoredLayout("assinatura", external.DeviceName, 100, 60, 410, 300, Now);

        var placement = WidgetPlacement.Resolve(layout, monitors);

        Assert.Equal(external.DeviceName, placement.Monitor.DeviceName);
        Assert.Equal(external.WorkArea.X + 100, placement.Bounds.X);
        Assert.Equal(external.WorkArea.Y + 60, placement.Bounds.Y);
        Assert.Equal(410, placement.Bounds.Width);
    }

    [Fact]
    public void IdaEVoltaEntrePosicaoFisicaELayoutPreservaOValor()
    {
        var monitors = Monitors.Dual();
        var bounds = new Rectangle(1920 + 300, 200, 500, 380);

        var layout = WidgetPlacement.ToLayout(bounds, monitors, "assinatura", Now);
        var placement = WidgetPlacement.Resolve(layout, monitors);

        Assert.Equal(bounds, placement.Bounds);
    }

    [Fact]
    public void LayoutDeMonitorDesconectadoCaiNoPrincipal()
    {
        var layout = new StoredLayout("assinatura", @"\\.\DISPLAY2", 100, 60, 410, 300, Now);

        var placement = WidgetPlacement.Resolve(layout, Monitors.Single());

        Assert.Equal(@"\\.\DISPLAY1", placement.Monitor.DeviceName);
        Assert.True(Monitors.Laptop().WorkArea.Contains(placement.Bounds));
    }

    [Fact]
    public void PosicaoCompletamenteForaDaTelaEPuxadaParaDentro()
    {
        var monitors = Monitors.Single();
        var layout = new StoredLayout("assinatura", @"\\.\DISPLAY1", 9000, 9000, 410, 300, Now);

        var placement = WidgetPlacement.Resolve(layout, monitors);

        Assert.True(monitors[0].WorkArea.Contains(placement.Bounds));
    }

    [Fact]
    public void PosicaoNegativaEPuxadaParaDentro()
    {
        var monitors = Monitors.Single();
        var layout = new StoredLayout("assinatura", @"\\.\DISPLAY1", -5000, -5000, 410, 300, Now);

        var placement = WidgetPlacement.Resolve(layout, monitors);

        Assert.True(monitors[0].WorkArea.Contains(placement.Bounds));
    }

    [Fact]
    public void TamanhoMaiorQueAAreaUtilELimitado()
    {
        var monitors = Monitors.Single();
        var layout = new StoredLayout("assinatura", @"\\.\DISPLAY1", 0, 0, 5000, 5000, Now);

        var placement = WidgetPlacement.Resolve(layout, monitors);

        Assert.Equal(monitors[0].WorkArea.Width, placement.Bounds.Width);
        Assert.Equal(monitors[0].WorkArea.Height, placement.Bounds.Height);
        Assert.True(monitors[0].WorkArea.Contains(placement.Bounds));
    }

    [Fact]
    public void TamanhoMenorQueOMinimoEElevado()
    {
        var monitors = new[] { Monitors.Laptop(1.0) };
        var layout = new StoredLayout("assinatura", @"\\.\DISPLAY1", 0, 0, 50, 40, Now);

        var placement = WidgetPlacement.Resolve(layout, monitors);

        Assert.Equal((int)WidgetPlacement.MinWidthDip, placement.Bounds.Width);
        Assert.Equal((int)WidgetPlacement.MinHeightDip, placement.Bounds.Height);
    }

    [Fact]
    public void LayoutsDeConfiguracoesDiferentesSaoIndependentes()
    {
        var dual = Monitors.Dual();
        var single = Monitors.Single();

        // O monitor do notebook tem escala 1.5, entao o tamanho fisico equivalente
        // ao padrao de 410 x 300 DIPs e 615 x 450.
        var dualBounds = new Rectangle(1920 + 1400, 40, 410, 300);
        var singleBounds = new Rectangle(60, 400, 615, 450);

        var dualLayout = WidgetPlacement.ToLayout(dualBounds, dual, DisplaySignature.Compute(dual), Now);
        var singleLayout = WidgetPlacement.ToLayout(singleBounds, single, DisplaySignature.Compute(single), Now);

        Assert.NotEqual(dualLayout.DisplaySignature, singleLayout.DisplaySignature);
        Assert.Equal(dualBounds, WidgetPlacement.Resolve(dualLayout, dual).Bounds);
        Assert.Equal(singleBounds, WidgetPlacement.Resolve(singleLayout, single).Bounds);
    }

    [Fact]
    public void MudancaDeEscalaMantemOWidgetAcessivel()
    {
        // Salvo em 100%, restaurado com o mesmo monitor a 200%.
        var at100 = new[] { Monitors.Laptop(1.0) };
        var layout = WidgetPlacement.ToLayout(new Rectangle(1400, 700, 410, 300), at100, "assinatura", Now);

        var at200 = new[] { Monitors.Laptop(2.0) };
        var placement = WidgetPlacement.Resolve(layout, at200);

        Assert.True(at200[0].WorkArea.Contains(placement.Bounds));
    }

    [Fact]
    public void ClampEscolheOMonitorComMaiorIntersecao()
    {
        var monitors = Monitors.Dual();
        var mostlyOnExternal = new Rectangle(1920 + 1500, 100, 410, 300);

        var placement = WidgetPlacement.Clamp(mostlyOnExternal, monitors, monitors[0]);

        Assert.Equal(@"\\.\DISPLAY2", placement.Monitor.DeviceName);
    }

    [Fact]
    public void SemMonitoresEUmErroDeProgramacao()
        => Assert.Throws<ArgumentException>(() => WidgetPlacement.Resolve(null, Array.Empty<MonitorInfo>()));
}
