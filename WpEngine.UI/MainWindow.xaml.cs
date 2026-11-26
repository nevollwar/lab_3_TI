using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PaymentApp.Domain;

namespace WpEngine.UI;

public partial class MainWindow : Window
{
    private readonly PersistentBank _sharedBank;
    private int _activeLabIndex;

    public MainWindow()
    {
        InitializeComponent();
        _sharedBank = PersistentBank.Create("payment.db");
        PaymentView.AttachBank(_sharedBank);
        LoopsViewControl.AttachBank(_sharedBank);
        SelectLab(0);
    }

    private void LabTab_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && int.TryParse(rb.Tag?.ToString(), out int index))
        {
            SelectLab(index);
        }
    }

    private void SelectLab(int index)
    {
        _activeLabIndex = index;

        if (PaymentView == null || WpView == null || LoopsViewControl == null)
        {
            return;
        }

        PaymentView.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        WpView.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        LoopsViewControl.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

        Title = index switch
        {
            0 => "ЛР-1 · Платёжный модуль — Комплекс формальной верификации",
            1 => "ЛР-2 · Калькулятор слабейшего предусловия (wp) — Комплекс формальной верификации",
            2 => "ЛР-3 · Верификатор корректности циклов — Комплекс формальной верификации",
            _ => "Комплекс формальной верификации"
        };
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.D1:
            case Key.NumPad1:
                TabLab1.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                TabLab2.IsChecked = true;
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                TabLab3.IsChecked = true;
                e.Handled = true;
                break;
            case Key.R:
            case Key.Return:
                if (_activeLabIndex == 1 && WpView.ViewModel.CalculateCommand.CanExecute(null))
                {
                    WpView.ViewModel.CalculateCommand.Execute(null);
                    e.Handled = true;
                }
                break;
            case Key.S:
                if (_activeLabIndex == 1 && WpView.ViewModel.ExportReportCommand.CanExecute(null))
                {
                    WpView.ViewModel.ExportReportCommand.Execute(null);
                    e.Handled = true;
                }
                break;
        }
    }
}
