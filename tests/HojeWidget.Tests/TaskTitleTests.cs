using HojeWidget.Domain;

namespace HojeWidget.Tests;

public class TaskTitleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n  ")]
    public void TextoVazioNaoEValido(string? raw)
    {
        Assert.False(TaskTitle.TryNormalize(raw, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void EspacosDasPontasSaoRemovidos()
    {
        Assert.True(TaskTitle.TryNormalize("   Academia   ", out var normalized));
        Assert.Equal("Academia", normalized);
    }

    [Fact]
    public void AcentosEUnicodeSaoPreservados()
    {
        Assert.True(TaskTitle.TryNormalize("Preparar aula do CASD — revisão ✅", out var normalized));
        Assert.Equal("Preparar aula do CASD — revisão ✅", normalized);
    }

    [Fact]
    public void QuebrasDeLinhaViramEspacos()
    {
        Assert.True(TaskTitle.TryNormalize("Fazer lista\r\nde Termodinâmica", out var normalized));
        Assert.Equal("Fazer lista de Termodinâmica", normalized);
    }

    [Fact]
    public void LimiteDefensivoDe500Caracteres()
    {
        var raw = new string('a', 700);

        Assert.True(TaskTitle.TryNormalize(raw, out var normalized));
        Assert.Equal(TaskTitle.MaxLength, normalized.Length);
    }
}
