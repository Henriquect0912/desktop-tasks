using HojeWidget.Models;

namespace HojeWidget.Tests;

public class TaskListServiceTests
{
    [Fact]
    public void CriarTarefaGuardaOTextoNormalizado()
    {
        using var fixture = new TaskFixture();

        var created = fixture.Tasks.AddForToday("  Academia  ");

        Assert.NotNull(created);
        Assert.Equal("Academia", created!.Title);
        Assert.Equal(new[] { "Academia" }, fixture.PendingTitles());
    }

    [Fact]
    public void TextoVazioNaoCriaTarefa()
    {
        using var fixture = new TaskFixture();

        Assert.Null(fixture.Tasks.AddForToday("   "));
        Assert.Empty(fixture.PendingTitles());
    }

    [Fact]
    public void NovaTarefaEntraNoFimDasPendentes()
    {
        using var fixture = new TaskFixture();

        fixture.Tasks.AddForToday("Primeira");
        fixture.Tasks.AddForToday("Segunda");
        fixture.Tasks.AddForToday("Terceira");

        Assert.Equal(new[] { "Primeira", "Segunda", "Terceira" }, fixture.PendingTitles());
    }

    [Fact]
    public void EdicaoPersisteDepoisDeReiniciar()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("Comprar comida");

        Assert.True(fixture.Tasks.Rename(fixture.IdOf("Comprar comida"), "Comprar comida para a semana"));

        fixture.Restart();

        Assert.Equal(new[] { "Comprar comida para a semana" }, fixture.PendingTitles());
    }

    [Fact]
    public void EdicaoComTextoVazioENegada()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("Academia");

        Assert.False(fixture.Tasks.Rename(fixture.IdOf("Academia"), "   "));
        Assert.Equal(new[] { "Academia" }, fixture.PendingTitles());
    }

    [Fact]
    public void ExcluirRemoveDefinitivamente()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("Academia");

        fixture.Tasks.Delete(fixture.IdOf("Academia"));
        fixture.Restart();

        Assert.Empty(fixture.Tasks.GetSnapshot().Display);
    }

    [Fact]
    public void ConcluirMoveParaOGrupoInferior()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("Primeira");
        fixture.Tasks.AddForToday("Segunda");

        fixture.Tasks.SetCompleted(fixture.IdOf("Primeira"), true);

        var snapshot = fixture.Tasks.GetSnapshot();
        Assert.Equal(new[] { "Segunda" }, snapshot.Pending.Select(t => t.Title));
        Assert.Equal(new[] { "Primeira" }, snapshot.CompletedToday.Select(t => t.Title));
        Assert.Equal(new[] { "Segunda", "Primeira" }, snapshot.Display.Select(t => t.Title));
    }

    [Fact]
    public void ConcluirGravaEstadoDataLocalEInstanteUtc()
    {
        using var fixture = new TaskFixture(new DateTime(2026, 3, 10, 21, 30, 0, DateTimeKind.Local));
        fixture.Tasks.AddForToday("Academia");

        fixture.Tasks.SetCompleted(fixture.IdOf("Academia"), true);

        var item = fixture.Tasks.GetSnapshot().CompletedToday.Single();
        Assert.True(item.IsCompleted);
        Assert.Equal("2026-03-10", item.CompletedLocalDate);
        Assert.NotNull(item.CompletedAtUtc);
    }

    [Fact]
    public void DesmarcarLimpaOsCamposDeConclusao()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("Academia");
        var id = fixture.IdOf("Academia");

        fixture.Tasks.SetCompleted(id, true);
        fixture.Tasks.SetCompleted(id, false);

        var item = fixture.Tasks.GetSnapshot().Pending.Single();
        Assert.False(item.IsCompleted);
        Assert.Null(item.CompletedLocalDate);
        Assert.Null(item.CompletedAtUtc);
    }

    [Fact]
    public void DesmarcarDevolveATarefaAPosicaoManualAnterior()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("A");
        fixture.Tasks.AddForToday("B");
        fixture.Tasks.AddForToday("C");

        var idB = fixture.IdOf("B");
        fixture.Tasks.SetCompleted(idB, true);
        Assert.Equal(new[] { "A", "C" }, fixture.PendingTitles());

        fixture.Tasks.SetCompleted(idB, false);

        Assert.Equal(new[] { "A", "B", "C" }, fixture.PendingTitles());
    }

    [Fact]
    public void ConcluidasDeHojeSaoOrdenadasPorConclusao()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("A");
        fixture.Tasks.AddForToday("B");

        fixture.Tasks.SetCompleted(fixture.IdOf("B"), true);
        fixture.Clock.AdvanceMinutes(5);
        fixture.Tasks.SetCompleted(fixture.IdOf("A"), true);

        Assert.Equal(new[] { "B", "A" }, fixture.CompletedTitles());
    }

    [Fact]
    public void OrdemManualPersisteDepoisDeReiniciar()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("A");
        fixture.Tasks.AddForToday("B");
        fixture.Tasks.AddForToday("C");

        var ids = fixture.Tasks.GetSnapshot().Pending.Select(t => t.Id).ToList();
        fixture.Tasks.ReorderPending(new[] { ids[2], ids[0], ids[1] });

        Assert.Equal(new[] { "C", "A", "B" }, fixture.PendingTitles());

        fixture.Restart();

        Assert.Equal(new[] { "C", "A", "B" }, fixture.PendingTitles());
    }

    [Fact]
    public void ReordenarNaoAfetaAsConcluidas()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("A");
        fixture.Tasks.AddForToday("B");
        fixture.Tasks.AddForToday("C");

        fixture.Tasks.SetCompleted(fixture.IdOf("B"), true);

        var pending = fixture.Tasks.GetSnapshot().Pending.Select(t => t.Id).ToList();
        fixture.Tasks.ReorderPending(new[] { pending[1], pending[0] });

        Assert.Equal(new[] { "C", "A" }, fixture.PendingTitles());
        Assert.Equal(new[] { "B" }, fixture.CompletedTitles());
    }

    [Fact]
    public void PendenciaNaoExpiraNaViradaDoDia()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("Fazer lista de Termodinâmica");

        fixture.Clock.AdvanceDays(1);

        Assert.Equal(new[] { "Fazer lista de Termodinâmica" }, fixture.PendingTitles());
    }

    [Fact]
    public void ConcluidaDeOntemDesapareceSemReiniciar()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("Academia");
        fixture.Tasks.SetCompleted(fixture.IdOf("Academia"), true);

        Assert.Single(fixture.Tasks.GetSnapshot().CompletedToday);

        fixture.Clock.AdvanceDays(1);

        var snapshot = fixture.Tasks.GetSnapshot();
        Assert.Empty(snapshot.CompletedToday);
        Assert.Empty(snapshot.Pending);
        Assert.Equal(ListState.Empty, snapshot.State);
    }

    [Fact]
    public void ConcluidaAntigaContinuaNoBancoMasNaoAparece()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("Academia");
        fixture.Tasks.SetCompleted(fixture.IdOf("Academia"), true);

        fixture.Clock.AdvanceDays(3);
        fixture.Restart();

        Assert.Empty(fixture.Tasks.GetSnapshot().Display);

        // Reabrir no dia da conclusao mostra a tarefa de novo: o registro nao foi apagado.
        fixture.Clock.AdvanceDays(-3);
        Assert.Equal(new[] { "Academia" }, fixture.CompletedTitles());
    }

    [Fact]
    public void ContagemConsideraSomentePendentes()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("A");
        fixture.Tasks.AddForToday("B");
        fixture.Tasks.SetCompleted(fixture.IdOf("A"), true);

        var snapshot = fixture.Tasks.GetSnapshot();
        Assert.Equal(1, snapshot.PendingCount);
        Assert.Equal("1 pendente", snapshot.PendingCountText);
    }

    [Fact]
    public void ListaVaziaEDiferenteDeTudoConcluido()
    {
        using var fixture = new TaskFixture();
        Assert.Equal(ListState.Empty, fixture.Tasks.GetSnapshot().State);

        fixture.Tasks.AddForToday("Academia");
        Assert.Equal(ListState.Normal, fixture.Tasks.GetSnapshot().State);

        fixture.Tasks.SetCompleted(fixture.IdOf("Academia"), true);
        Assert.Equal(ListState.AllDone, fixture.Tasks.GetSnapshot().State);
    }

    [Fact]
    public void NormalizarCompactaAsPosicoesSemMudarAOrdem()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("A");
        fixture.Tasks.AddForToday("B");
        fixture.Tasks.AddForToday("C");

        var ids = fixture.Tasks.GetSnapshot().Pending.Select(t => t.Id).ToList();
        fixture.Tasks.ReorderPending(new[] { ids[2], ids[1], ids[0] });

        fixture.Tasks.Normalize();

        var pending = fixture.Tasks.GetSnapshot().Pending;
        Assert.Equal(new[] { "C", "B", "A" }, pending.Select(t => t.Title));
        Assert.Equal(new long[] { 0, 1, 2 }, pending.Select(t => t.SortOrder));
    }

    [Fact]
    public void TarefasSobrevivemAoReinicioDoAplicativo()
    {
        using var fixture = new TaskFixture();
        fixture.Tasks.AddForToday("Preparar aula do CASD");
        fixture.Tasks.AddForToday("Academia");
        fixture.Tasks.SetCompleted(fixture.IdOf("Academia"), true);

        fixture.Restart();

        var snapshot = fixture.Tasks.GetSnapshot();
        Assert.Equal(new[] { "Preparar aula do CASD" }, snapshot.Pending.Select(t => t.Title));
        Assert.Equal(new[] { "Academia" }, snapshot.CompletedToday.Select(t => t.Title));
    }
}
