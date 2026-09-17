using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using HojeWidget.Models;
using HojeWidget.ViewModels;

using Point = System.Windows.Point;

namespace HojeWidget.Views;

/// <summary>
/// Aplicativo principal: onde o usuário organiza e planeja.
/// Janela tradicional, com barra de título e presença na barra de tarefas —
/// ao contrário do widget, que vive na área de trabalho.
/// Fechar apenas esconde: quem encerra o processo é "Sair" na bandeja.
/// </summary>
public partial class MainAppWindow : Window
{
    private const string TaskDragFormat = "HojeWidget.TaskId";
    private const double DragThreshold = 5;

    private Point _pressPoint;
    private TaskItemViewModel? _dragCandidate;

    public MainAppWindow()
    {
        InitializeComponent();
    }

    private MainAppViewModel? Model => DataContext as MainAppViewModel;

    private bool _forceClose;

    protected override void OnClosing(CancelEventArgs e)
    {
        // Fechar a janela não encerra o aplicativo: ela só se esconde.
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }

    /// <summary>Fecha de verdade, no encerramento do processo.</summary>
    public void CloseForShutdown()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key != Key.Escape || Model is null)
        {
            return;
        }

        if (Model.IsDeleteConfirmationVisible)
        {
            Model.CancelDeleteCommand.Execute(null);
            e.Handled = true;
        }
        else if (Model.IsRoutineDeleteVisible)
        {
            Model.CancelRoutineDeleteCommand.Execute(null);
            e.Handled = true;
        }
        else if (Model.IsRoutineFormOpen)
        {
            Model.CancelRoutineFormCommand.Execute(null);
            e.Handled = true;
        }
        else if (Model.IsTaskFormOpen)
        {
            Model.CancelTaskForm();
            e.Handled = true;
        }
        else if (Model.Proposal.IsOpen)
        {
            Model.Proposal.CancelCommand.Execute(null);
            e.Handled = true;
        }
        else if (Model.IsMovePickerOpen)
        {
            Model.CancelMoveCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ---------------- Nova tarefa ----------------

    public void FocusNewTaskField()
        => Dispatcher.BeginInvoke(
            new Action(() => FocusBehavior.Focus(NewTaskBox)),
            global::System.Windows.Threading.DispatcherPriority.Input);

    private void NewTaskBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (Model is null)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            Model.CommitTaskForm();
            e.Handled = true;
        }
    }

    // ---------------- Ações de linha ----------------

    /// <summary>
    /// Concluir uma linha. Dentro de um projeto ou de uma prova a conclusão passa
    /// pela seção correspondente — é ela que sabe perguntar se o todo acabou, ou
    /// que a linha é uma sessão de estudo e não uma tarefa.
    /// </summary>
    private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskItemViewModel item })
        {
            return;
        }

        if (OwnerOf<ContainerSectionViewModel>(sender) is { } section)
        {
            section.ToggleSubtask(item);
            return;
        }

        if (OwnerOf<ExamSectionViewModel>(sender) is { } exams)
        {
            exams.ToggleSession(item);
            return;
        }

        Model?.ToggleCompleted(item);
    }

    /// <summary>
    /// Sobe pela árvore procurando o modelo dono do elemento. É assim que uma
    /// mesma linha de tarefa serve Hoje, a semana, um projeto e uma prova.
    /// </summary>
    private static T? OwnerOf<T>(object sender) where T : class
    {
        var current = sender as DependencyObject;

        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: T match })
            {
                return match;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }

    private void RowMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TaskItemViewModel item } || Model is null)
        {
            return;
        }

        var open = !item.IsMenuOpen;
        foreach (var other in AllItems())
        {
            other.IsMenuOpen = false;
        }

        item.IsMenuOpen = open;
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskItemViewModel item })
        {
            Model?.BeginEdit(item);
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskItemViewModel item })
        {
            Model?.RequestDelete(item);
        }
    }

    private void SomedayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskItemViewModel item })
        {
            item.IsMenuOpen = false;
            Model?.MoveToSomeday(item.Id);
        }
    }

    // ---------------- Rotinas ----------------

    private void RoutineMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: RoutineListItemViewModel item } || Model is null)
        {
            return;
        }

        var open = !item.IsMenuOpen;
        foreach (var other in Model.Routines)
        {
            other.IsMenuOpen = false;
        }

        item.IsMenuOpen = open;
    }

    private void EditRoutine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RoutineListItemViewModel item })
        {
            Model?.BeginEditRoutine(item);
        }
    }

    private void DeleteRoutine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RoutineListItemViewModel item })
        {
            Model?.RequestRoutineDelete(item);
        }
    }

    // ---------------- Projetos, entregas e provas ----------------

    /// <summary>V0.5 — abrir a escolha de projeto ou entrega para uma tarefa.</summary>
    private void MoveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskItemViewModel item })
        {
            Model?.RequestMove(item);
        }
    }

    private void MoveTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MoveTargetViewModel target })
        {
            Model?.MoveTo(target);
        }
    }

    private void ContainerItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ContainerListItemViewModel item }
            && OwnerOf<ContainerSectionViewModel>(sender) is { } section)
        {
            section.Select(item);
        }
    }

    private void EditContainer_Click(object sender, RoutedEventArgs e)
        => OwnerOf<ContainerSectionViewModel>(sender)?.BeginEdit();

    private void ExamItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ExamListItemViewModel item }
            && OwnerOf<ExamSectionViewModel>(sender) is { } section)
        {
            section.Select(item);
        }
    }

    /// <summary>Selecionar um nó da árvore abre o painel de edição dele.</summary>
    private void StudyNode_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: StudyNodeViewModel node }
            && OwnerOf<ExamSectionViewModel>(sender) is { } section)
        {
            section.SelectNode(node);
        }
    }

    /// <summary>
    /// "+ Teoria", "+ Lista" e "+ Outro" abrem o editor inline embaixo do próprio
    /// assunto. O tipo vem no Tag do botão.
    /// </summary>
    private void AddStudyNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StudyNodeViewModel subject } element
            || OwnerOf<ExamSectionViewModel>(sender) is not { } section)
        {
            return;
        }

        var type = (element.Tag as string) switch
        {
            "Theory" => StudyNodeType.Theory,
            "ExerciseList" => StudyNodeType.ExerciseList,
            _ => StudyNodeType.Custom,
        };

        section.BeginAdd(subject, type);
    }

    private void RemoveOverride_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CapacityOverrideViewModel item })
        {
            Model?.Capacity.RemoveOverride(item.Date);
        }
    }

    /// <summary>Clique num dia do calendario mostra o que ha nele.</summary>
    private void CalendarDay_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CalendarDayViewModel day })
        {
            Model?.SelectDay(day.Date);
        }
    }

    private IEnumerable<TaskItemViewModel> AllItems()
    {
        if (Model is null)
        {
            return Enumerable.Empty<TaskItemViewModel>();
        }

        return Model.TodayItems
            .Concat(Model.SomedayItems)
            .Concat(Model.WeekDays.SelectMany(d => d.Items))
            .Concat(Model.SelectedDay.Planned)
            .Concat(Model.Projects.Subtasks)
            .Concat(Model.Deliveries.Subtasks)
            .Concat(Model.Exams.Sessions);
    }

    // ---------------- Arrastar entre dias ----------------

    private void TaskRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressPoint = e.GetPosition(this);
        _dragCandidate = null;

        if (sender is not FrameworkElement { DataContext: TaskItemViewModel item })
        {
            return;
        }

        // Cliques em controles não iniciam arrasto.
        if (e.OriginalSource is DependencyObject source
            && (FindAncestor<ButtonBase>(source) is not null || FindAncestor<TextBoxBase>(source) is not null))
        {
            return;
        }

        // Ocorrência de rotina não muda de dia: quem manda é a regra da rotina.
        _dragCandidate = item.CanEdit ? item : null;
    }

    private void TaskRow_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _pressPoint.X) < DragThreshold && Math.Abs(position.Y - _pressPoint.Y) < DragThreshold)
        {
            return;
        }

        var data = new DataObject(TaskDragFormat, _dragCandidate.Id);
        var source = (DependencyObject)sender;
        _dragCandidate = null;

        DragDrop.DoDragDrop(source, data, DragDropEffects.Move);
    }

    private void DayColumn_DragOver(object sender, DragEventArgs e)
    {
        var accepted = e.Data.GetDataPresent(TaskDragFormat);
        e.Effects = accepted ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;

        if (accepted && sender is Border border)
        {
            border.Background = (Brush)FindResource("HoverBrush");
        }
    }

    private void DayColumn_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = Brushes.Transparent;
        }
    }

    private void DayColumn_Drop(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = Brushes.Transparent;
        }

        if (sender is not FrameworkElement { DataContext: DayColumnViewModel column }
            || e.Data.GetData(TaskDragFormat) is not string taskId)
        {
            return;
        }

        e.Handled = true;
        Model?.MoveToDay(taskId, column.Date);
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        var current = source;

        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
