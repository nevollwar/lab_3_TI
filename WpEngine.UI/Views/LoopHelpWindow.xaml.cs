using System.Windows;

namespace WpEngine.UI.Views;

/// <summary>
/// Модальное окно справки по ЛР-3 (инварианты, вариант-функция, wp, EOD-аудит).
/// </summary>
public partial class LoopHelpWindow : Window
{
    public LoopHelpWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
