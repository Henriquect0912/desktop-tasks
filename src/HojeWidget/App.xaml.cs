using System.Windows;
using HojeWidget.Interop;
using HojeWidget.Services;
using HojeWidget.Services.Database;
using HojeWidget.Services.DisplayLayout;
using HojeWidget.Services.Startup;
using HojeWidget.Services.System;

namespace HojeWidget;

/// <summary>Ponto de entrada. Instancia unica, sem console e sem rede.</summary>
public partial class App : Application
{
    private SingleInstanceGuard? _instanceGuard;
    private WidgetController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DetachConsoleIfAny();
        AppLog.Initialize();

        _instanceGuard = SingleInstanceGuard.Acquire();
        if (!_instanceGuard.IsFirstInstance)
        {
            // Uma segunda execucao apenas acorda a instancia existente.
            SingleInstanceGuard.SignalExistingInstance();
            Shutdown();
            return;
        }

        AppDatabase database;
        try
        {
            database = OpenDatabaseWithRetries();
        }
        catch (Exception ex)
        {
            AppLog.Error("Nao foi possivel abrir o banco de dados.", ex);
            MessageBox.Show(
                $"Não foi possível abrir os dados do Hoje Widget em:\n{AppPaths.DatabaseFile}\n\nO arquivo não foi alterado. Feche outras cópias do aplicativo e tente novamente.",
                "Hoje Widget",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        try
        {
            var clock = new SystemClock();
            var settings = new SettingsRepository(database);
            var layouts = new DisplayLayoutRepository(database);
            var taskRepository = new TaskRepository(database);
            var reviewRepository = new DailyReviewRepository(database);

            var tasks = new TaskListService(taskRepository, clock);
            tasks.Normalize();

            var review = new DailyReviewService(reviewRepository, clock);
            var routines = new RoutineService(new RoutineRepository(database), clock);

            // V0.5 a V0.7: projetos e entregas, provas, e o motor de planejamento.
            var containers = new ContainerService(new ContainerRepository(database), taskRepository, clock);

            // V0.7.1: a árvore de estudo e o grafo de dependências das provas.
            var examRepository = new ExamRepository(database);
            var studyGraph = new StudyGraphService(new StudyNodeRepository(database), examRepository, clock);
            var exams = new ExamService(examRepository, studyGraph, clock);

            var capacity = new CapacityService(new CapacityRepository(database));
            var planning = new PlanningService(
                taskRepository,
                routines,
                exams,
                containers,
                capacity,
                new DailyPlanRepository(database),
                settings,
                clock);
            var examPlanning = new ExamPlanningService(exams, capacity, planning, clock);

            _controller = new WidgetController(
                database,
                settings,
                layouts,
                tasks,
                routines,
                containers,
                exams,
                examPlanning,
                planning,
                review,
                clock,
                new ScreenMonitorProvider(),
                new RegistryStartupRegistration());

            _controller.ExitRequested += Shutdown;
            _controller.Start();

            AppLog.Info("Aplicativo iniciado.");
        }
        catch (Exception ex)
        {
            AppLog.Error("Falha na inicializacao.", ex);
            database.Dispose();
            MessageBox.Show(
                "O Hoje Widget não conseguiu iniciar. Detalhes foram gravados em:\n" + AppPaths.LogDirectory,
                "Hoje Widget",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>
    /// O aplicativo é publicado como WinExe e não tem console próprio. Mas quando ele é
    /// iniciado pelo runtime compartilhado, o processo é o <c>dotnet.exe</c>, que é um
    /// programa de console, e o Windows abre uma janela de terminal junto. Essa janela
    /// fica visível e, pior, encerra o aplicativo quando alguém a fecha.
    ///
    /// Soltar o console faz o Windows fechá-lo, já que nenhum outro processo continua
    /// associado a ele. No modo self-contained não há console e a chamada não faz nada.
    /// </summary>
    private static void DetachConsoleIfAny()
    {
        try
        {
            if (NativeMethods.GetConsoleWindow() != IntPtr.Zero)
            {
                NativeMethods.FreeConsole();
            }
        }
        catch (Exception)
        {
            // Sem console, ou API indisponível: o aplicativo segue normalmente.
        }
    }

    /// <summary>
    /// Poucas tentativas curtas quando o arquivo esta temporariamente bloqueado.
    /// Nenhuma tentativa recria ou sobrescreve um banco existente.
    /// </summary>
    private static AppDatabase OpenDatabaseWithRetries()
    {
        AppPaths.EnsureCreated();

        Exception? last = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return new AppDatabase(AppPaths.DatabaseFile);
            }
            catch (Exception ex)
            {
                last = ex;
                AppLog.Warn($"Tentativa {attempt + 1} de abrir o banco falhou.", ex);
                Thread.Sleep(300);
            }
        }

        throw last ?? new InvalidOperationException("Falha desconhecida ao abrir o banco.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_controller is not null)
        {
            _controller.ExitRequested -= Shutdown;
            _controller.Dispose();
            _controller = null;
        }

        _instanceGuard?.Dispose();
        _instanceGuard = null;

        base.OnExit(e);
    }
}
