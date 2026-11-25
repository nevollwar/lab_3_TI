using System.Collections.ObjectModel;
using System.Windows.Input;
using WpEngine.Core.Loops;
using WpEngine.UI.Models;

namespace WpEngine.UI.ViewModels;

/// <summary>
/// ViewModel для расширенной панели цикла ЛР-3:
/// управление данными + пошаговое исполнение + блок «Инвариант и вариант» + блок «Проверка сохранения (wp)».
/// Не изменяет LoopViewModel, создаётся параллельно как LoopPanelVM в Window.Resources.
/// </summary>
public class LoopPanelViewModel : ViewModelBase
{
    //зависимости
    private readonly LoopRunner   _runner   = new();
    private readonly LoopVerifier _verifier = new();

    //параметры генерации
    private int      _n         = 6;
    private int      _min       = -9;
    private int      _max       = 9;
    private int      _threshold = 3;
    private LoopMode _selectedMode = LoopMode.PrefixSum;

    //состояние исполнения
    private int    _currentIndex;
    private int    _result;
    private int    _variantT;
    private bool   _isFinished;
    private bool   _postHeld;
    private string _statusLine = string.Empty;

    //инвариант
    private string _invariantFormula   = string.Empty;
    private string _invariantWords     = string.Empty;
    private string _variantFormula     = string.Empty;
    private string _variantWords       = string.Empty;
    private bool   _invariantBeforeStep;
    private bool   _invariantAfterStep;
    private bool   _hasSteppedOnce;
    private int    _variantMax         = 1;   // для прогресс-бара (= n)

    //блок wp
    private string _wpPreservationFormula      = string.Empty;
    private string _wpPreservationExplanation  = string.Empty;
    private bool   _verificationDone;
    private string _verificationSummary        = string.Empty;
    private bool   _allConditionsValid;

    //  Публичные свойства

    // параметры
    public int N
    {
        get => _n;
        set { if (SetProperty(ref _n, value)) _variantMax = Math.Max(1, value); }
    }
    public int Min       { get => _min;       set => SetProperty(ref _min, value); }
    public int Max       { get => _max;       set => SetProperty(ref _max, value); }
    public int Threshold { get => _threshold; set => SetProperty(ref _threshold, value); }

    public LoopMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                ResetLoop();
                ApplySpec();
                RunVerification();
            }
        }
    }

    // массив 
    public ObservableCollection<ArrayItem2> Array { get; } = new();

    // состояние исполнения
    public int  CurrentIndex { get => _currentIndex; private set => SetProperty(ref _currentIndex, value); }
    public int  Result       { get => _result;       private set => SetProperty(ref _result, value); }
    public int  VariantT     { get => _variantT;     private set => SetProperty(ref _variantT, value); }
    public bool IsFinished   { get => _isFinished;   private set => SetProperty(ref _isFinished, value); }
    public bool PostHeld     { get => _postHeld;     private set => SetProperty(ref _postHeld, value); }
    public string StatusLine { get => _statusLine;   private set => SetProperty(ref _statusLine, value); }

    //инвариант и вариант
    public string InvariantFormula  { get => _invariantFormula;  private set => SetProperty(ref _invariantFormula, value); }
    public string InvariantWords    { get => _invariantWords;    private set => SetProperty(ref _invariantWords, value); }
    public string VariantFormula    { get => _variantFormula;    private set => SetProperty(ref _variantFormula, value); }
    public string VariantWords      { get => _variantWords;      private set => SetProperty(ref _variantWords, value); }

    /// <summary>Инвариант истинен ДО последнего шага.</summary>
    public bool InvariantBeforeStep { get => _invariantBeforeStep; private set => SetProperty(ref _invariantBeforeStep, value); }
    /// <summary>Инвариант истинен ПОСЛЕ последнего шага.</summary>
    public bool InvariantAfterStep  { get => _invariantAfterStep;  private set => SetProperty(ref _invariantAfterStep, value); }
    /// <summary>Показывать индикаторы только когда был хотя бы один шаг.</summary>
    public bool HasSteppedOnce      { get => _hasSteppedOnce;      private set => SetProperty(ref _hasSteppedOnce, value); }

    public int VariantMax
    {
        get => _variantMax;
        private set => SetProperty(ref _variantMax, Math.Max(1, value));
    }

    //блок wp
    public string WpPreservationFormula     { get => _wpPreservationFormula;     private set => SetProperty(ref _wpPreservationFormula, value); }
    public string WpPreservationExplanation { get => _wpPreservationExplanation; private set => SetProperty(ref _wpPreservationExplanation, value); }
    public bool   VerificationDone          { get => _verificationDone;          private set => SetProperty(ref _verificationDone, value); }
    public string VerificationSummary       { get => _verificationSummary;       private set => SetProperty(ref _verificationSummary, value); }
    public bool   AllConditionsValid        { get => _allConditionsValid;         private set => SetProperty(ref _allConditionsValid, value); }

    //коллекции
    public ObservableCollection<LoopStepItemViewModel>      StepHistory  { get; } = new();
    public ObservableCollection<LoopConditionItemViewModel> Conditions   { get; } = new();
    public ObservableCollection<string>                     VariantLog   { get; } = new();

    //доступные режимы для ComboBox
    public IReadOnlyList<LoopModeItem> AvailableModes { get; } =
    [
        new LoopModeItem(LoopMode.PrefixSum,        "Сумма префикса"),
        new LoopModeItem(LoopMode.CountGreaterThan, "Счётчик > T"),
        new LoopModeItem(LoopMode.PrefixMax,        "Максимум префикса"),
    ];

    //команды
    public ICommand GenerateCommand { get; }
    public ICommand StepCommand     { get; }
    public ICommand RunCommand      { get; }
    public ICommand ResetCommand    { get; }
    public ICommand VerifyCommand   { get; }

    //  Конструктор

    public LoopPanelViewModel()
    {
        GenerateCommand = new RelayCommand(_ => Generate());
        StepCommand     = new RelayCommand(_ => Step(),    _ => !IsFinished && Array.Count > 0);
        RunCommand      = new RelayCommand(async _ => await RunAsync(), _ => !IsFinished && Array.Count > 0);
        ResetCommand    = new RelayCommand(_ => { ResetLoop(); HasSteppedOnce = false; });
        VerifyCommand   = new RelayCommand(_ => RunVerification());

        Generate();
    }

    //  Генерация и инициализация

    private void Generate()
    {
        var rng = new Random();
        int n = Math.Max(1, N);
        var arr = new int[n];
        for (int i = 0; i < n; i++)
            arr[i] = rng.Next(Min, Max + 1);

        InitRunner(arr);
        ApplySpec();
        RunVerification();
    }

    private void InitRunner(int[] arr)
    {
        try
        {
            _runner.Reset(SelectedMode, arr, Threshold);
        }
        catch (ArgumentException)
        {
            // PrefixMax с пустым массивом — добавляем хотя бы один элемент
            if (arr.Length == 0)
            {
                arr = [0];
                _runner.Reset(SelectedMode, arr, Threshold);
            }
            else throw;
        }

        RefreshArray();
        UpdateState();
        StepHistory.Clear();
        VariantLog.Clear();
        HasSteppedOnce = false;
        VariantMax = arr.Length;
    }

    private void ResetLoop()
    {
        var arr = _runner.Array.Count > 0 ? _runner.Array.ToArray() : [.. Array.Select(a => a.Value)];
        if (arr.Length == 0) arr = [0];
        InitRunner(arr);
    }

    private void ApplySpec()
    {
        try
        {
            var spec = _verifier.GetSpec(SelectedMode);
            InvariantFormula = spec.InvariantFormula;
            InvariantWords   = spec.InvariantWords;
            VariantFormula   = spec.VariantFormula;
            VariantWords     = spec.VariantWords;
        }
        catch
        {
            InvariantFormula = string.Empty;
            InvariantWords   = string.Empty;
            VariantFormula   = string.Empty;
            VariantWords     = string.Empty;
        }
    }

    //  Верификация (wp-движок через LoopVerifier)

    private void RunVerification()
    {
        try
        {
            var report = _verifier.VerifyMode(SelectedMode);

            WpPreservationFormula     = report.PreservationFormula;
            WpPreservationExplanation = report.PreservationHumanExplanation;
            AllConditionsValid        = report.AllValid;
            VerificationSummary       = report.AllValid
                ? "✓ Все 4 условия тотальной корректности доказаны."
                : "✗ Часть условий не выполнена — проверьте инвариант.";

            Conditions.Clear();
            foreach (var c in report.AllConditions)
            {
                Conditions.Add(new LoopConditionItemViewModel
                {
                    Title            = c.Title,
                    Formula          = c.Formula,
                    ExpandedFormula  = c.ExpandedFormula,
                    HumanExplanation = c.HumanExplanation,
                    ProofSummary     = c.ProofSummary,
                    IsValid          = c.IsValid,
                });
            }

            VerificationDone = true;
        }
        catch (Exception ex)
        {
            VerificationDone          = false;
            WpPreservationFormula     = string.Empty;
            WpPreservationExplanation = $"Ошибка верификации: {ex.Message}";
        }
    }
    //  Пошаговое исполнение
    private void Step()
    {
        if (IsFinished) return;

        try
        {
            var snap = _runner.Step();

            StepHistory.Add(new LoopStepItemViewModel
            {
                StepNumber      = snap.StepNumber,
                J               = snap.J,
                Res             = snap.Res,
                Variant         = snap.Variant,
                InvariantBefore = snap.InvariantBefore,
                InvariantAfter  = snap.InvariantAfter,
                PostHeld        = snap.PostHeld,
                VariantLogEntry = snap.VariantLogEntry,
            });

            VariantLog.Add(snap.VariantLogEntry);

            InvariantBeforeStep = snap.InvariantBefore;
            InvariantAfterStep  = snap.InvariantAfter;
            HasSteppedOnce      = true;

            UpdateState();
            RefreshCurrentCell();

            StatusLine = snap.VariantLogEntry;
        }
        catch (InvalidOperationException ex)
        {
            StatusLine = ex.Message;
        }
    }

    private async Task RunAsync()
    {
        while (!IsFinished)
        {
            Step();
            await Task.Delay(280);
        }
    }
    //  Вспомогательные

    private void RefreshArray()
    {
        Array.Clear();
        for (int i = 0; i < _runner.Array.Count; i++)
            Array.Add(new ArrayItem2(i, _runner.Array[i], this));
    }

    private void RefreshCurrentCell()
    {
        foreach (var item in Array)
            item.Refresh();
    }

    private void UpdateState()
    {
        CurrentIndex = _runner.J;
        Result       = _runner.Res;
        VariantT     = _runner.Variant;
        IsFinished   = _runner.IsFinished;
        PostHeld     = IsFinished && LoopRunner.CheckPost(SelectedMode, _runner.Array.ToArray(), _runner.Res, _runner.Threshold);

        OnPropertyChanged(nameof(CurrentIndex));
        OnPropertyChanged(nameof(Result));
        OnPropertyChanged(nameof(VariantT));
        OnPropertyChanged(nameof(IsFinished));
        OnPropertyChanged(nameof(PostHeld));

        foreach (var item in Array)
            item.Refresh();
    }
}

//  Вспомогательные типы

/// <summary>Пара (enum-значение, отображаемое имя) для ComboBox режимов.</summary>
public record LoopModeItem(LoopMode Mode, string DisplayName);

/// <summary>
/// Ячейка массива для новой панели (аналог ArrayItem, но без зависимости от LoopViewModel).
/// </summary>
public class ArrayItem2 : System.ComponentModel.INotifyPropertyChanged
{
    private readonly LoopPanelViewModel _parent;

    public int Index { get; }
    public int Value { get; set; }

    public ArrayItem2(int index, int value, LoopPanelViewModel parent)
    {
        Index   = index;
        Value   = value;
        _parent = parent;
    }

    public bool IsCurrent => _parent.CurrentIndex == Index;

    public void Refresh() => OnPropertyChanged(nameof(IsCurrent));

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
