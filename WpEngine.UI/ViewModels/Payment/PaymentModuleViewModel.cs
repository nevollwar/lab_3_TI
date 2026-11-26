using System.Collections.ObjectModel;
using PaymentApp.Domain;

namespace WpEngine.UI.ViewModels.Payment;

public class PaymentModuleViewModel : ViewModelBase
{
    private OperationViewModelBase? _selectedOperation;

    public ObservableCollection<OperationViewModelBase> Operations { get; }

    public OperationViewModelBase? SelectedOperation
    {
        get => _selectedOperation;
        set
        {
            if (SetProperty(ref _selectedOperation, value))
            {
                _selectedOperation?.RefreshAccounts();
            }
        }
    }

    public Bank Bank { get; }

    public PaymentModuleViewModel()
    {
        Bank = PersistentBank.Create("payment.db");

        Operations = new ObservableCollection<OperationViewModelBase>
        {
            new TransferOperationViewModel(Bank),
            new DepositOperationViewModel(Bank),
            new ServicePaymentOperationViewModel(Bank)
        };

        SelectedOperation = Operations.First();
    }
}
