using System.Windows;
using System.Windows.Controls;

namespace HojeWidget.Views;

/// <summary>
/// Da foco a um campo assim que ele se torna visivel. Usado pelo campo de criacao
/// e pelo campo de edicao dentro do item.
/// </summary>
public static class FocusBehavior
{
    public static readonly DependencyProperty FocusOnVisibleProperty =
        DependencyProperty.RegisterAttached(
            "FocusOnVisible",
            typeof(bool),
            typeof(FocusBehavior),
            new PropertyMetadata(false, OnFocusOnVisibleChanged));

    public static void SetFocusOnVisible(DependencyObject element, bool value)
        => element.SetValue(FocusOnVisibleProperty, value);

    public static bool GetFocusOnVisible(DependencyObject element)
        => (bool)element.GetValue(FocusOnVisibleProperty);

    private static void OnFocusOnVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.IsVisibleChanged -= OnIsVisibleChanged;

        if (e.NewValue is true)
        {
            element.IsVisibleChanged += OnIsVisibleChanged;
        }
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || sender is not FrameworkElement element)
        {
            return;
        }

        element.Dispatcher.BeginInvoke(new Action(() => Focus(element)),
            global::System.Windows.Threading.DispatcherPriority.Input);
    }

    internal static void Focus(FrameworkElement element)
    {
        element.Focus();

        if (element is TextBox textBox)
        {
            textBox.CaretIndex = textBox.Text.Length;
            textBox.SelectAll();
        }
    }
}
