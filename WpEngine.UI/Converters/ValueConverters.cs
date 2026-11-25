using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpEngine.UI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is true;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Конвертер: показывает элемент (Visible), если строка непустая; скрывает (Collapsed) если пустая.
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasText = !string.IsNullOrWhiteSpace(value as string);
        if (Invert) hasText = !hasText;
        return hasText ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
public class BoolToColorBrushConverter : IValueConverter
{
    public string TrueColor { get; set; } = "#1E8E3E";
    public string FalseColor { get; set; } = "#D93025";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool b = value is true;
        string hex = b ? TrueColor : FalseColor;
        return new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToStringConverter : IValueConverter
{
    public string TrueText { get; set; } = "Да";
    public string FalseText { get; set; } = "Нет";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? TrueText : FalseText;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
