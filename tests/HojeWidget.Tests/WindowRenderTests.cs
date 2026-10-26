using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HojeWidget.Models;
using HojeWidget.Services;
using HojeWidget.ViewModels;
using HojeWidget.Views;

namespace HojeWidget.Tests;

/// <summary>
/// Desenha a janela fora da tela e confere que ela realmente produz pixels.
/// Protege contra regressoes silenciosas de tema e recursos, em que a janela
/// continuaria abrindo mas apareceria totalmente transparente.
///
/// Tudo roda em uma unica thread STA: pinceis do WPF tem afinidade de thread,
/// entao os dois cenarios sao renderizados no mesmo lugar.
/// </summary>
[Collection(WpfCollection.Name)]
public class WindowRenderTests
{
    private const int Width = 410;
    private const int Height = 300;

    [Fact]
    public void JanelaRenderizaConteudoNosDoisTemas()
    {
        var report = RunOnStaThread(() =>
        {
            EnsureApplication();

            using var fixture = new RoutineFixture(new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Local));
            fixture.Tasks.AddForToday("Fazer lista de Termodinâmica");
            fixture.Tasks.AddForToday("Preparar aula do CASD");
            var comida = fixture.Tasks.AddForToday("Comprar comida")!;
            fixture.Tasks.SetCompleted(comida.Id, true);
            fixture.Routines.Create("Academia", HojeWidget.Models.RecurrenceType.Daily);

            // Lembretes: o terço de baixo do widget precisa desenhar as linhas.
            fixture.Tasks.Add("Renovar passaporte", plannedDate: null);
            fixture.Tasks.Add("Comprar pilha para o controle", plannedDate: null);

            // V0.5 a V0.7: cada seção nova precisa ter conteúdo de verdade para
            // que o desenho prove alguma coisa.
            var projeto = fixture.Containers.Create(
                HojeWidget.Models.ContainerKind.Project,
                "Preparação para mestrado",
                fixture.Clock.Today.AddDays(20))!;
            fixture.Containers.AddSubtask(projeto.Id, "Pesquisar programas", fixture.Clock.Today, estimatedMinutes: 60);
            fixture.Containers.AddSubtask(projeto.Id, "Ler editais", estimatedMinutes: 90);

            var entrega = fixture.Containers.Create(
                HojeWidget.Models.ContainerKind.Delivery,
                "Relatório de laboratório",
                fixture.Clock.Today.AddDays(6))!;
            fixture.Containers.AddSubtask(entrega.Id, "Organizar dados", estimatedMinutes: 45);

            // V0.7.1: a prova é uma árvore com assunto, teoria e lista — e a lista
            // nasce dependendo da teoria, então ela aparece bloqueada.
            var prova = fixture.Exams.Create("MPP-31", "P1", fixture.Clock.Today.AddDays(4))!;
            var torcao = fixture.Graph.AddSubject(prova.Id, "Torção")!;
            fixture.Graph.AddNode(prova.Id, "Teoria", HojeWidget.Models.StudyNodeType.Theory, torcao.Id, 60);
            var lista = fixture.Graph.AddExerciseList(prova.Id, "Lista 1", torcao.Id, 90, 12)!;
            fixture.Exams.AddSession(prova.Id, "MPP-31 — estudar Lista 1", lista.Id, 90, fixture.Clock.Today);

            var viewModel = new MainViewModel(
                fixture.Tasks, fixture.Routines, fixture.Exams, fixture.Planning, fixture.Review, fixture.Clock);
            viewModel.EvaluateDailyReview();

            var window = new MainWindow { DataContext = viewModel };
            var content = (FrameworkElement)window.Content;

            ThemeManager.Apply(AppTheme.Black);
            var black = Render(content, "preto.png");

            // A divisão dois terços / um terço é medida no próprio desenho.
            var reminders = (FrameworkElement)window.FindName("RemindersHost");
            var reminderTop = reminders.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
            var remindersDrawn = reminders.ActualHeight > 0 && viewModel.Reminders.Count == 2;

            // O menu do "+", com as três opções.
            viewModel.ToggleAddMenu();
            Render(content, "preto-menu.png");
            var claudeButton = (FrameworkElement)window.FindName("ClaudeChatButton");
            var claudeTop = claudeButton.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
            var menuDrawn = claudeButton.ActualHeight > 0 && claudeTop + claudeButton.ActualHeight <= Height;
            viewModel.ToggleAddMenu();

            ThemeManager.Apply(AppTheme.Translucent);
            var translucent = Render(content, "translucido.png");

            // O aplicativo principal também precisa montar e desenhar: um recurso
            // de XAML faltando só apareceria quando o usuário clicasse em "Abrir aplicativo".
            ThemeManager.Apply(AppTheme.Black);
            var appViewModel = new MainAppViewModel(
                fixture.Tasks, fixture.Routines, fixture.Containers, fixture.Exams,
                fixture.ExamPlanning, fixture.Planning, fixture.Clock);
            var appWindow = new MainAppWindow { DataContext = appViewModel };
            var appContent = (FrameworkElement)appWindow.Content;

            // O calendário é desenhado com o dia da prova selecionado: o lembrete
            // da prova no painel lateral só apareceria clicando naquele dia.
            appViewModel.SelectDay(fixture.Clock.Today.AddDays(4));

            // Cada seção é desenhada: um recurso de XAML faltando em Calendário,
            // Prazos ou Rotinas só apareceria quando o usuário clicasse ali.
            var sections = new Dictionary<AppSection, RenderReport>();
            foreach (var section in Enum.GetValues<AppSection>())
            {
                appViewModel.SelectedSection = section;
                sections[section] = Render(appContent, $"app-{section}.png".ToLowerInvariant(), 980, 640);
            }

            // O editor inline da árvore de estudo: três caixas de texto que
            // precisam dizer sozinhas o que esperam.
            appViewModel.SelectedSection = AppSection.Exams;
            var subject = appViewModel.Exams.Tree.First(n => n.IsSubject);
            appViewModel.Exams.BeginAdd(subject, HojeWidget.Models.StudyNodeType.ExerciseList);
            var inline = Render(appContent, "app-exams-inline.png", 980, 640);

            // O painel do nó selecionado, com os campos e as dependências.
            appViewModel.Exams.CloseInline();
            appViewModel.Exams.SelectNode(appViewModel.Exams.Tree.First(n => n.IsList));
            var panel = Render(appContent, "app-exams-node.png", 980, 640);

            // O plano de estudo proposto: a data-limite para começar em destaque
            // e as datas em modo de leitura.
            appViewModel.Exams.GeneratePlanCommand.Execute(null);
            var proposal = Render(appContent, "app-exams-plan.png", 980, 640);

            // A distribuição de uma entrega usa a sobreposição do plano sugerido,
            // que agora também anuncia a data-limite para começar.
            appViewModel.SelectedSection = AppSection.Deliveries;
            appViewModel.Deliveries.DistributeCommand.Execute(null);
            var distribution = Render(appContent, "app-delivery-plan.png", 980, 640);

            return (Black: black, Translucent: translucent, Sections: sections,
                    ReminderTop: reminderTop, RemindersDrawn: remindersDrawn, MenuDrawn: menuDrawn,
                    Inline: inline, Panel: panel, Proposal: proposal,
                    Distribution: distribution, StartText: appViewModel.Proposal.StartText);
        });

        // Os dois temas precisam pintar uma superficie sólida com texto claro por cima.
        // A transparencia do tema Translucido nao aparece aqui de proposito: ela e
        // aplicada a janela inteira por SetLayeredWindowAttributes, fora da arvore
        // visual, porque UpdateLayeredWindow (o caminho do AllowsTransparency) nao
        // funciona com o widget hospedado na area de trabalho.
        foreach (var (nome, tema) in new[] { ("Preto", report.Black), ("Translúcido", report.Translucent) })
        {
            Assert.True(tema.DistinctColors > 20, $"{nome}: esperava conteudo desenhado, veio {tema.DistinctColors} cores");
            Assert.True(tema.OpaquePixelRatio > 0.90, $"{nome}: a superficie deveria ser solida, veio {tema.OpaquePixelRatio:P0}");
            Assert.True(tema.LightPixels > 200, $"{nome}: esperava texto claro, veio {tema.LightPixels} pixels");
            Assert.InRange(tema.DominantLuminance, 0, 40);
        }

        // Os lembretes começam depois de dois terços da altura — não antes.
        Assert.True(report.RemindersDrawn, "a lista de lembretes não foi desenhada");
        Assert.InRange(report.ReminderTop, Height * 0.60, Height * 0.80);

        Assert.True(report.MenuDrawn, "a opção do Claude no menu do + não foi desenhada inteira");

        // Os dois temas são distintos entre si.
        Assert.True(report.Translucent.DominantLuminance > report.Black.DominantLuminance,
            "o tema Translúcido deveria ter uma superfície mais clara que a do tema Preto");

        // O editor inline desenha as dicas dentro dos campos.
        Assert.True(report.Inline.LightPixels > 200, "o editor inline não desenhou texto");
        Assert.True(report.Panel.LightPixels > 200, "o painel do nó não desenhou texto");
        Assert.True(report.Proposal.LightPixels > 200, "o plano proposto não desenhou texto");
        Assert.True(report.Distribution.LightPixels > 200, "a distribuição proposta não desenhou texto");

        // Se a caixa da data-limite sumir do XAML, o texto continua existindo no
        // modelo; por isso o desenho e o texto são conferidos juntos.
        Assert.Contains("limite", report.StartText);

        // Aplicativo principal: cada seção monta, desenha e mostra texto.
        foreach (var (section, render) in report.Sections)
        {
            Assert.True(render.DistinctColors > 20, $"a seção {section} não desenhou conteúdo");
            Assert.True(render.LightPixels > 200, $"a seção {section} não desenhou texto claro");
        }
    }

    private sealed record RenderReport(
        int DistinctColors,
        int LightPixels,
        int SemiTransparentPixels,
        double OpaquePixelRatio,
        double DominantLuminance);

    /// <summary>
    /// Pasta onde cada execucao deixa o PNG renderizado. Serve para inspecionar o
    /// visual sem precisar abrir o aplicativo.
    /// </summary>
    private static string DumpDirectory => Path.Combine(Path.GetTempPath(), "HojeWidgetRender");

    private static void Dump(RenderTargetBitmap bitmap, string name)
    {
        try
        {
            Directory.CreateDirectory(DumpDirectory);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(DumpDirectory, name));
            encoder.Save(stream);
        }
        catch (IOException)
        {
            // O PNG e apenas um apoio de diagnostico; falhar aqui nao invalida o teste.
        }
    }

    private static RenderReport Render(FrameworkElement content, string dumpName, int width = Width, int height = Height)
    {
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(content);
        Dump(target, dumpName);

        var stride = width * 4;
        var pixels = new byte[stride * height];
        target.CopyPixels(pixels, stride, 0);

        var distinct = new HashSet<uint>();
        var light = 0;
        var semiTransparent = 0;
        var opaque = 0;
        var histogram = new Dictionary<uint, int>();

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var b = pixels[i];
            var g = pixels[i + 1];
            var r = pixels[i + 2];
            var a = pixels[i + 3];

            var key = (uint)((a << 24) | (r << 16) | (g << 8) | b);
            distinct.Add(key);
            histogram[key] = histogram.TryGetValue(key, out var count) ? count + 1 : 1;

            if (a == 255)
            {
                opaque++;
            }
            else if (a > 0)
            {
                semiTransparent++;
            }

            // Pbgra32 e pre-multiplicado: um pixel claro exige alfa alto e canais altos.
            if (a > 200 && r > 180 && g > 180 && b > 180)
            {
                light++;
            }
        }

        var dominant = histogram.OrderByDescending(p => p.Value).First().Key;
        var dominantLuminance =
            0.2126 * ((dominant >> 16) & 0xFF) +
            0.7152 * ((dominant >> 8) & 0xFF) +
            0.0722 * (dominant & 0xFF);

        return new RenderReport(
            distinct.Count,
            light,
            semiTransparent,
            (double)opaque / (width * height),
            dominantLuminance);
    }

    private static void EnsureApplication()
    {
        if (Application.Current is null)
        {
            var app = new App();
            app.InitializeComponent();
        }
    }

    private static T RunOnStaThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("Falha ao renderizar a janela.", failure);
        }

        return result;
    }
}
