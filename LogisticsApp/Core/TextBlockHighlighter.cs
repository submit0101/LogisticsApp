using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LogisticsApp.Core;

public static class TextBlockHighlighter
{
    public static readonly DependencyProperty SelectionProperty =
        DependencyProperty.RegisterAttached(
            "Selection",
            typeof(string),
            typeof(TextBlockHighlighter),
            new PropertyMetadata(default(string), OnSelectionChanged));

    public static string GetSelection(DependencyObject obj) => (string)obj.GetValue(SelectionProperty);
    public static void SetSelection(DependencyObject obj, string value) => obj.SetValue(SelectionProperty, value);

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text",
            typeof(string),
            typeof(TextBlockHighlighter),
            new PropertyMetadata(default(string), OnSelectionChanged));

    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock) return;

        string text = GetText(textBlock);
        string highlightText = GetSelection(textBlock);

        textBlock.Inlines.Clear();

        if (string.IsNullOrEmpty(text)) return;

        if (string.IsNullOrEmpty(highlightText))
        {
            textBlock.Text = text;
            return;
        }

        int index = text.IndexOf(highlightText, StringComparison.OrdinalIgnoreCase);
        int lastIndex = 0;

        while (index >= 0)
        {
            textBlock.Inlines.Add(new Run(text.Substring(lastIndex, index - lastIndex)));

            var highlightRun = new Run(text.Substring(index, highlightText.Length))
            {
                Background = Brushes.Yellow,
                Foreground = Brushes.Black,
                FontWeight = FontWeights.Bold
            };
            textBlock.Inlines.Add(highlightRun);

            lastIndex = index + highlightText.Length;
            index = text.IndexOf(highlightText, lastIndex, StringComparison.OrdinalIgnoreCase);
        }

        if (lastIndex < text.Length)
        {
            textBlock.Inlines.Add(new Run(text.Substring(lastIndex)));
        }
    }
}