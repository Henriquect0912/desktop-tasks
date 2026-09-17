using HojeWidget.Domain.Planning;
using HojeWidget.ViewModels;

namespace HojeWidget.Tests;

/// <summary>
/// A data-limite para começar uma entrega ou um projeto.
///
/// O plano encosta no prazo em vez de começar hoje: ocupa os últimos dias
/// possíveis, e o primeiro deles é o limite. Ninguém começa hoje uma entrega
/// que vence daqui a oito dias só porque hoje existe.
///
/// É a mesma regra que <see cref="StudyStartDateTests"/> guarda para as provas.
/// </summary>
public class DistributionStartDateTests
{
    private static readonly DateOnly Today = new(2026, 9, 1);

    private static DistributableItem Item(string id, string title, int? minutes, bool canSplit = true, long order = 0)
        => new(id, title, minutes, canSplit, order);

    [Fact]
    public void EntregaDistanteNaoComecaHoje()
    {
        // O caso da tela: 2h de trabalho, prazo em 16/09, capacidade de 3h por dia.
        // A janela entregue ao distribuidor termina na véspera do prazo.
        var today = new DateOnly(2026, 9, 8);
        var plan = Distributor.Distribute(
            new[] { Item("1", "fazer com o que temos", 120) },
            today,
            new DateOnly(2026, 9, 15),
            new TestCapacity(180));

        // Último dia de trabalho: 14/09, com 15/09 de margem.
        Assert.Equal(new DateOnly(2026, 9, 14), plan.RecommendedStart);
        Assert.Equal(new DateOnly(2026, 9, 15), plan.MarginDate);
        Assert.True(plan.MarginPreserved);

        var slot = Assert.Single(plan.Slots);
        Assert.Equal(new DateOnly(2026, 9, 14), slot.Date);
        Assert.DoesNotContain(plan.Slots, s => s.Date == today);
    }

    [Fact]
    public void AsEtapasEncostamNoPrazoNaOrdemCerta()
    {
        var items = new[]
        {
            Item("1", "Etapa 1", 60, order: 0),
            Item("2", "Etapa 2", 60, order: 1),
            Item("3", "Etapa 3", 60, order: 2),
        };

        // Dias úteis 01/09 a 09/09, margem em 10/09: uma etapa por dia nos três
        // últimos, e não nos três primeiros.
        var plan = Distributor.Distribute(items, Today, new DateOnly(2026, 9, 10), new TestCapacity(120));

        Assert.Equal(new DateOnly(2026, 9, 7), plan.RecommendedStart);
        Assert.Equal(
            new[] { new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 9) },
            plan.Slots.Select(s => s.Date));

        // Encostar no prazo não pode inverter as etapas.
        Assert.Equal(new[] { "Etapa 1", "Etapa 2", "Etapa 3" }, plan.Slots.Select(s => s.Title));
    }

    [Fact]
    public void UmDiaDepoisDoLimiteNaoCaberiaMais()
    {
        var plan = Distributor.Distribute(
            new[] { Item("1", "Escrever relatório", 240) },
            Today,
            new DateOnly(2026, 9, 10),
            new TestCapacity(120));

        var limite = plan.RecommendedStart!.Value;
        Assert.Equal(new DateOnly(2026, 9, 8), limite);

        // Começando um dia depois do limite sobra um único dia útil antes da
        // margem, e 2h não dão conta de 4h de trabalho.
        var ultimoDiaUtil = new DateOnly(2026, 9, 9);
        var diasRestantes = ultimoDiaUtil.DayNumber - limite.AddDays(1).DayNumber + 1;
        Assert.True(diasRestantes * 120 < 240, "começar um dia depois deveria estourar a janela");
    }

    [Fact]
    public void SessoesSaoNumeradasNaOrdemDoCalendario()
    {
        // Os dias são escolhidos de trás para frente; a numeração, não.
        var plan = Distributor.Distribute(
            new[] { Item("1", "Escrever relatório", 240) },
            Today,
            new DateOnly(2026, 9, 10),
            new TestCapacity(120));

        Assert.Equal(new[] { 1, 2 }, plan.Slots.Select(s => s.SessionIndex));
        Assert.Equal(
            new[] { new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 9) },
            plan.Slots.Select(s => s.Date));
    }

    [Fact]
    public void DiaFechadoRecuaOInicio()
    {
        var capacity = new TestCapacity(120).Closed(new DateOnly(2026, 9, 8));

        var plan = Distributor.Distribute(
            new[] { Item("1", "Escrever relatório", 240) },
            Today,
            new DateOnly(2026, 9, 10),
            capacity);

        // Sem o dia 08, a segunda metade recua para o 07.
        Assert.Equal(new DateOnly(2026, 9, 7), plan.RecommendedStart);
        Assert.DoesNotContain(plan.Slots, s => s.Date == new DateOnly(2026, 9, 8));
        Assert.Equal(240, plan.Slots.Sum(s => s.Minutes ?? 0));
    }

    [Fact]
    public void IndivisivelPegaODiaMaisTardeQueOComporta()
    {
        // Dois dias comportam as 3h20 inteiras; o plano fica com o mais tarde.
        var capacity = new TestCapacity(120)
            .On(new DateOnly(2026, 9, 5), 240)
            .On(new DateOnly(2026, 9, 8), 240);

        var plan = Distributor.Distribute(
            new[] { Item("1", "Levar o carro à oficina", 200, canSplit: false) },
            Today,
            new DateOnly(2026, 9, 10),
            capacity);

        var slot = Assert.Single(plan.Slots);
        Assert.Equal(new DateOnly(2026, 9, 8), slot.Date);
        Assert.Empty(plan.Conflicts);
    }

    [Fact]
    public void DuracaoDesconhecidaTambemEncosta()
    {
        var items = new[]
        {
            Item("1", "Etapa 1", null, order: 0),
            Item("2", "Etapa 2", null, order: 1),
        };

        var plan = Distributor.Distribute(items, Today, new DateOnly(2026, 9, 10), new TestCapacity(180));

        Assert.Equal(new DateOnly(2026, 9, 8), plan.RecommendedStart);
        Assert.Equal(new[] { "Etapa 1", "Etapa 2" }, plan.Slots.Select(s => s.Title));
    }

    [Fact]
    public void PlanoVazioNaoRecomendaDiaNenhum()
    {
        Assert.Null(DistributionPlan.Empty.RecommendedStart);
    }

    [Fact]
    public void ATelaAnunciaADataLimiteEDeixaAsDatasEmLeitura()
    {
        var today = new DateOnly(2026, 9, 8);
        var plan = Distributor.Distribute(
            new[] { Item("1", "fazer com o que temos", 120) },
            today,
            new DateOnly(2026, 9, 15),
            new TestCapacity(180));

        var proposal = new DistributionProposalViewModel();
        proposal.Open("Lab MEB-23 · prazo 16/09", plan, today, _ => { });

        Assert.True(proposal.HasStart);
        Assert.Contains("14/09", proposal.StartText);
        Assert.Contains("limite", proposal.StartText);

        // A data continua sendo do programa até que se peça para ajustar.
        Assert.True(proposal.IsReadOnly);
    }

    [Fact]
    public void QuandoOLimiteEHojeATelaDizIsso()
    {
        var today = new DateOnly(2026, 9, 8);
        var plan = Distributor.Distribute(
            new[] { Item("1", "Entregar relatório", 120) },
            today,
            today,
            new TestCapacity(180));

        var proposal = new DistributionProposalViewModel();
        proposal.Open("Entrega de hoje", plan, today, _ => { });

        Assert.Equal("Para caber antes do prazo, é para começar hoje.", proposal.StartText);
    }
}
