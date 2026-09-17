using HojeWidget.Domain;

namespace HojeWidget.Tests;

public class PendingCounterTests
{
    [Theory]
    [InlineData(0, "0 pendentes")]
    [InlineData(1, "1 pendente")]
    [InlineData(2, "2 pendentes")]
    [InlineData(4, "4 pendentes")]
    [InlineData(11, "11 pendentes")]
    public void SingularEPluralCorretos(int count, string expected)
        => Assert.Equal(expected, PendingCounter.Format(count));

    [Fact]
    public void ValorNegativoNaoQuebraOTexto()
        => Assert.Equal("0 pendentes", PendingCounter.Format(-3));
}
