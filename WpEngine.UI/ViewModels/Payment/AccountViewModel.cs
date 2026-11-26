using PaymentApp.Domain;

namespace WpEngine.UI.ViewModels.Payment;

public class AccountViewModel : ViewModelBase
{
    public Account Model { get; }

    public string Number => Model.Number;
    public string Owner => Model.Owner;
    public decimal Balance => Model.Balance;
    public string DisplayName => $"{Number} — {Owner} ({Balance:N2} ₽)";

    public AccountViewModel(Account model)
    {
        Model = model;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Balance));
        OnPropertyChanged(nameof(DisplayName));
    }
}
