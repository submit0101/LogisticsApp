using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace LogisticsApp.Services;

public class RegNumberMaskBehavior : Behavior<TextBox>
{
    private readonly char[] _allowedChars = { 'А', 'В', 'Е', 'К', 'М', 'Н', 'О', 'Р', 'С', 'Т', 'У', 'Х' };
    private readonly Dictionary<char, char> _replacements = new()
    {
        {'A', 'А'}, {'B', 'В'}, {'E', 'Е'}, {'K', 'К'}, {'M', 'М'}, {'H', 'Н'},
        {'O', 'О'}, {'P', 'Р'}, {'C', 'С'}, {'T', 'Т'}, {'Y', 'У'}, {'X', 'Х'}
    };

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.TextChanged += OnTextChanged;
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.TextChanged -= OnTextChanged;
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Zа-яА-Я0-9]+$");
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = AssociatedObject;
        int caretIndex = tb.CaretIndex;
        string text = tb.Text.ToUpper();
        string newText = "";

        foreach (char c in text)
        {
            char upperC = char.ToUpper(c);
            char processed = _replacements.ContainsKey(upperC) ? _replacements[upperC] : upperC;

            int currentLen = newText.Length;
            if (currentLen < 2)
            {
                if (_allowedChars.Contains(processed)) newText += processed;
            }
            else if (currentLen >= 2 && currentLen < 5)
            {
                if (char.IsDigit(processed)) newText += processed;
            }
            else if (currentLen == 5)
            {
                if (_allowedChars.Contains(processed)) newText += processed;
            }
            else if (currentLen >= 6 && currentLen < 8)
            {
                if (char.IsDigit(processed)) newText += processed;
            }
        }

        if (newText.Length > 8) newText = newText.Substring(0, 8);

        if (tb.Text != newText)
        {
            tb.Text = newText;
            tb.CaretIndex = Math.Min(caretIndex, newText.Length);
        }
    }
}

public class FioMaskBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.TextChanged += OnTextChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.TextChanged -= OnTextChanged;
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = AssociatedObject;
        string text = tb.Text;
        if (string.IsNullOrEmpty(text)) return;

        string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 3) parts = parts.Take(3).ToArray();

        string formattedText = "";
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length > 0)
            {
                string capitalized = char.ToUpper(part[0]) + part.Substring(1).ToLower();
                formattedText += capitalized;
                if (i < parts.Length - 1 || text.EndsWith(" "))
                {
                    if (i < 2) formattedText += " ";
                }
            }
        }

        if (parts.Length == 3 && text.EndsWith(" ") && !formattedText.EndsWith(" ")) { }
        else if (tb.Text != formattedText)
        {
            int caret = tb.CaretIndex;
            tb.Text = formattedText;
            tb.CaretIndex = Math.Min(caret, formattedText.Length);
        }
    }
}

public class PhoneMaskBehavior : Behavior<TextBox>
{
    public static readonly DependencyProperty MaskTypeProperty =
        DependencyProperty.Register("MaskType", typeof(int), typeof(PhoneMaskBehavior), new PropertyMetadata(0, OnMaskChanged));

    public int MaskType
    {
        get { return (int)GetValue(MaskTypeProperty); }
        set { SetValue(MaskTypeProperty, value); }
    }

    private static void OnMaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var behavior = d as PhoneMaskBehavior;
        behavior?.FormatText();
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.TextChanged += OnTextChanged;
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.TextChanged -= OnTextChanged;
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !char.IsDigit(e.Text, 0);
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        FormatText();
    }

    private void FormatText()
    {
        if (AssociatedObject == null) return;

        string rawText = Regex.Replace(AssociatedObject.Text, @"[^\d]", "");
        if (rawText.StartsWith("7") || rawText.StartsWith("8")) rawText = rawText.Substring(1);
        if (rawText.Length > 10) rawText = rawText.Substring(0, 10);

        string formatted = "";
        if (MaskType == 0)
        {
            if (rawText.Length > 0) formatted = "+7 (" + rawText.Substring(0, Math.Min(3, rawText.Length));
            if (rawText.Length > 3) formatted += ") " + rawText.Substring(3, Math.Min(3, rawText.Length - 3));
            if (rawText.Length > 6) formatted += "-" + rawText.Substring(6, Math.Min(2, rawText.Length - 6));
            if (rawText.Length > 8) formatted += "-" + rawText.Substring(8, Math.Min(2, rawText.Length - 8));
        }
        else
        {
            if (rawText.Length > 0) formatted = "+7 (" + rawText.Substring(0, Math.Min(4, rawText.Length));
            if (rawText.Length > 4) formatted += ") " + rawText.Substring(4, Math.Min(2, rawText.Length - 4));
            if (rawText.Length > 6) formatted += "-" + rawText.Substring(6, Math.Min(2, rawText.Length - 6));
            if (rawText.Length > 8) formatted += "-" + rawText.Substring(8, Math.Min(2, rawText.Length - 8));
        }

        if (AssociatedObject.Text != formatted)
        {
            AssociatedObject.Text = formatted;
            AssociatedObject.CaretIndex = formatted.Length;
        }
    }
}

public class NumericInputBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(AssociatedObject, OnPaste);
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(AssociatedObject, OnPaste);
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsTextAllowed(e.Text);
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            string text = (string)e.DataObject.GetData(typeof(string));
            if (!IsTextAllowed(text)) e.CancelCommand();
        }
        else e.CancelCommand();
    }

    private bool IsTextAllowed(string text)
    {
        return Regex.IsMatch(text, @"^[0-9]+$");
    }
}

public class InnMaskBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(AssociatedObject, OnPaste);
        AssociatedObject.TextChanged += OnTextChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(AssociatedObject, OnPaste);
        AssociatedObject.TextChanged -= OnTextChanged;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
    }

    // ИСПРАВЛЕНИЕ: Перехватываем вставку, очищаем строку от мусора и вставляем вручную
    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        // Обязательно отменяем стандартную вставку WPF
        e.CancelCommand();

        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            string text = (string)e.DataObject.GetData(typeof(string));

            // Вырезаем абсолютно всё, кроме цифр
            string digitsOnly = Regex.Replace(text, @"[^\d]", "");

            if (!string.IsNullOrEmpty(digitsOnly))
            {
                var tb = AssociatedObject;
                if (tb != null)
                {
                    // Сохраняем текст до и после каретки (заменяя выделенный фрагмент)
                    string currentText = tb.Text ?? "";
                    string textBefore = currentText.Substring(0, tb.SelectionStart);
                    string textAfter = currentText.Substring(tb.SelectionStart + tb.SelectionLength);

                    // Собираем новую строку
                    string newText = textBefore + digitsOnly + textAfter;

                    // Обрезаем до 12 символов (максимум для ИП)
                    if (newText.Length > 12)
                    {
                        newText = newText.Substring(0, 12);
                    }

                    tb.Text = newText;

                    // Ставим каретку в конец вставленного текста
                    tb.CaretIndex = Math.Min(textBefore.Length + digitsOnly.Length, 12);
                }
            }
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = AssociatedObject;
        string text = Regex.Replace(tb.Text, @"[^\d]", "");

        if (text.Length > 12)
        {
            text = text.Substring(0, 12);
        }

        if (tb.Text != text)
        {
            int caret = tb.CaretIndex;
            tb.Text = text;
            tb.CaretIndex = Math.Min(caret, text.Length);
        }
    }
}

public class NameMaskBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(AssociatedObject, OnPaste);
        AssociatedObject.TextChanged += OnTextChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(AssociatedObject, OnPaste);
        AssociatedObject.TextChanged -= OnTextChanged;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^[a-zA-Zа-яА-ЯёЁ\-]+$");
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            string text = (string)e.DataObject.GetData(typeof(string));
            if (!Regex.IsMatch(text, @"^[a-zA-Zа-яА-ЯёЁ\-]+$")) e.CancelCommand();
        }
        else e.CancelCommand();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = AssociatedObject;
        string text = tb.Text;
        if (string.IsNullOrEmpty(text)) return;

        string raw = Regex.Replace(text, @"[^a-zA-Zа-яА-ЯёЁ\-]", "");
        if (raw.Length > 0)
        {
            var parts = raw.Split('-');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1).ToLower();
            }
            raw = string.Join("-", parts);
        }

        if (tb.Text != raw)
        {
            int caret = tb.CaretIndex;
            tb.Text = raw;
            tb.CaretIndex = Math.Min(caret, raw.Length);
        }
    }
}

public class PassportMaskBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(AssociatedObject, OnPaste);
        AssociatedObject.TextChanged += OnTextChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(AssociatedObject, OnPaste);
        AssociatedObject.TextChanged -= OnTextChanged;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            string text = (string)e.DataObject.GetData(typeof(string));
            if (!Regex.IsMatch(text, @"^[0-9]+$")) e.CancelCommand();
        }
        else e.CancelCommand();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = AssociatedObject;
        string raw = Regex.Replace(tb.Text, @"[^\d]", "");
        if (raw.Length > 10) raw = raw.Substring(0, 10);

        string formatted = "";
        for (int i = 0; i < raw.Length; i++)
        {
            if (i == 2 || i == 4) formatted += " ";
            formatted += raw[i];
        }

        if (tb.Text != formatted)
        {
            tb.Text = formatted;
            tb.CaretIndex = formatted.Length;
        }
    }
}

public class MedCertMaskBehavior : Behavior<TextBox>
{
    private const string Prefix = "003-В/у № ";

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        AssociatedObject.TextChanged += OnTextChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        AssociatedObject.TextChanged -= OnTextChanged;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]+$");
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = AssociatedObject;
        string text = tb.Text;

        if (text.StartsWith(Prefix))
        {
            text = text.Substring(Prefix.Length);
        }

        string raw = Regex.Replace(text, @"[^\d]", "");
        string formatted = raw.Length > 0 ? Prefix + raw : "";

        if (tb.Text != formatted)
        {
            tb.Text = formatted;
            tb.CaretIndex = formatted.Length;
        }
    }
}