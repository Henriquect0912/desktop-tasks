using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HojeWidget.Views;

/// <summary>Visivel quando o valor e false.</summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

/// <summary>
/// Liga um RadioButton a um valor de enum. O parâmetro é o nome do valor.
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is string name && string.Equals(value.ToString(), name, StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is string name && targetType.IsEnum
            ? Enum.Parse(targetType, name)
            : global::System.Windows.Data.Binding.DoNothing;
}

/// <summary>Visivel quando a string esta vazia. Usado pelo placeholder do campo de criacao.</summary>
public sealed class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Transforma o recuo de um nó da árvore de estudo numa margem à esquerda.
/// A árvore é achatada numa lista, então a hierarquia aparece pelo recuo.
/// </summary>
public sealed class IndentToMarginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new Thickness(value is double indent ? indent : 0, 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
