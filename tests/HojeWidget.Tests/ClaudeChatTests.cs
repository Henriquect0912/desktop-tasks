using HojeWidget.Services.System;
using HojeWidget.ViewModels;

namespace HojeWidget.Tests;

/// <summary>
/// O atalho "Pedir ideias ao Claude" no menu do "+".
///
/// O widget só pede; quem abre o navegador é o controlador. Abrir de verdade não
/// é testado aqui — seria abrir uma aba no navegador de quem roda os testes.
/// </summary>
public class ClaudeChatTests
{
    private static readonly DateTime Now = new(2026, 10, 25, 9, 0, 0, DateTimeKind.Local);

    [Fact]
    public void OAtalhoFechaOMenuEPedeParaAbrir()
    {
        using var fixture = new RoutineFixture(Now);
        var widget = new MainViewModel(
            fixture.Tasks, fixture.Routines, fixture.Exams, fixture.Planning, fixture.Review, fixture.Clock);

        var requests = 0;
        widget.OpenClaudeChatRequested += () => requests++;

        widget.ToggleAddMenu();
        Assert.True(widget.IsAddMenuOpen);

        widget.OpenClaudeChatCommand.Execute(null);

        Assert.Equal(1, requests);
        Assert.False(widget.IsAddMenuOpen);

        // Nada é criado nem aberto dentro do widget.
        Assert.Empty(fixture.Tasks.GetAll());
        Assert.False(widget.IsAdding);
        Assert.False(widget.IsAddingReminder);
    }

    [Fact]
    public void OEnderecoEUmaConversaNovaENaoLevaNadaDoAplicativo()
    {
        var uri = new Uri(ClaudeChat.NewChatUrl);

        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal("claude.ai", uri.Host);
        Assert.Equal("/new", uri.AbsolutePath);

        // Nenhum parâmetro: tarefa, prova ou texto do usuário nunca vão no endereço.
        Assert.Empty(uri.Query);
        Assert.Empty(uri.Fragment);
    }
}
