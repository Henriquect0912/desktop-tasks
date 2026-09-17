using System.Windows;
using HojeWidget.Models;

namespace HojeWidget.Services;

/// <summary>
/// Troca o dicionario de cores ativo. Existem exatamente dois temas e nenhum
/// ajuste fino de cor ou opacidade.
/// </summary>
public static class ThemeManager
{
    private const int ThemeDictionaryIndex = 0;

    public static void Apply(AppTheme theme)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var source = new Uri(
            theme == AppTheme.Translucent
                ? "pack://application:,,,/HojeWidget;component/Themes/Translucent.xaml"
                : "pack://application:,,,/HojeWidget;component/Themes/Black.xaml",
            UriKind.Absolute);

        var dictionaries = application.Resources.MergedDictionaries;
        var replacement = new ResourceDictionary { Source = source };

        if (dictionaries.Count > ThemeDictionaryIndex)
        {
            dictionaries[ThemeDictionaryIndex] = replacement;
        }
        else
        {
            dictionaries.Insert(ThemeDictionaryIndex, replacement);
        }
    }
}
