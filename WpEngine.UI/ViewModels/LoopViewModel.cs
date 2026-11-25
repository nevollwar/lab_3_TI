using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpEngine.Core.Loops; // Твой namespace

namespace WpEngine.UI.ViewModels;

public class LoopViewModel : ViewModelBase // Наследуемся от вашего базового класса
{
    private readonly LoopRunner _runner = new();
    private int _n = 5;
    private int _min = -10;
    private int _max = 10;
    private int _threshold = 0;
    private LoopMode _selectedMode = LoopMode.PrefixSum;

    public LoopViewModel()
    {
        GenerateCommand = new RelayCommand(_ => Generate());
        StepCommand = new RelayCommand(_ => Step(), _ => !IsFinished);
        RunCommand = new RelayCommand(async _ => await RunAsync(), _ => !IsFinished);
        ResetCommand = new RelayCommand(_ => Reset());

        // Первичная генерация
        Generate();
    }

    // Параметры для панели
    public int N { get => _n; set => SetProperty(ref _n, value); }
    public int Min { get => _min; set => SetProperty(ref _min, value); }
    public int Max { get => _max; set => SetProperty(ref _max, value); }
    public int Threshold { get => _threshold; set => SetProperty(ref _threshold, value); }

    public LoopMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                Reset(); // При смене режима сбрасываем цикл
            }
        }
    }

    // Коллекция для отображения в UI
    public ObservableCollection<ArrayItem> Array { get; } = new();

    // Свойства состояния
    public int CurrentIndex => _runner.J;
    public int Result => _runner.Res;
    public int VariantT => _runner.Variant;
    public bool IsFinished => _runner.IsFinished;

    // Команды
    public ICommand GenerateCommand { get; }
    public ICommand StepCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand ResetCommand { get; }

    private void Generate()
    {
        var random = new Random();
        var arr = new int[N];
        for (int i = 0; i < N; i++)
        {
            arr[i] = random.Next(Min, Max + 1);
        }

        try
        {
            _runner.Reset(SelectedMode, arr, Threshold);
            RefreshArray();
            OnPropertyChanged(nameof(CurrentIndex));
            OnPropertyChanged(nameof(Result));
            OnPropertyChanged(nameof(VariantT));
            OnPropertyChanged(nameof(IsFinished));
        }
        catch (Exception)
        {
            // Игнорируем ошибку, если массив пустой для PrefixMax
        }
    }

    private void Step()
    {
        if (!IsFinished)
        {
            _runner.Step();
            UpdateState();
        }
    }

    private async Task RunAsync()
    {
        while (!IsFinished)
        {
            _runner.Step();
            UpdateState();
            await Task.Delay(300); // Задержка для визуализации
        }
    }

    private void Reset()
    {
        try
        {
            _runner.Reset(SelectedMode, _runner.Array.ToArray(), Threshold);
            RefreshArray();
            UpdateState();
        }
        catch
        {
            // Если массив пустой (для PrefixMax), просто игнорируем
        }
    }

    private void RefreshArray()
    {
        Array.Clear();
        for (int i = 0; i < _runner.Array.Count; i++)
        {
            Array.Add(new ArrayItem(i, _runner.Array[i], this));
        }
    }

    private void UpdateState()
    {
        OnPropertyChanged(nameof(CurrentIndex));
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(VariantT));
        OnPropertyChanged(nameof(IsFinished));

        foreach (var item in Array)
        {
            item.Refresh();
        }
    }
}

// Класс-обертка для ячейки массива
public class ArrayItem : INotifyPropertyChanged
{
    private readonly LoopViewModel _parent;
    public int Index { get; }
    public int Value { get; set; }

    public ArrayItem(int index, int value, LoopViewModel parent)
    {
        Index = index;
        Value = value;
        _parent = parent;
    }

    public bool IsCurrent => _parent.CurrentIndex == Index;

    public void Refresh() => OnPropertyChanged(nameof(IsCurrent));

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}