using System.Diagnostics;
using WpEngine.Core.AST;

namespace WpEngine.Core.Loops;

/// <summary>
/// Запись снимка состояния цикла после одного шага.
/// Содержит все переменные, нужные для отображения в GUI и проверки инварианта.
/// </summary>
public record LoopStepSnapshot(
    int   StepNumber,
    int   J,
    int   Res,
    int   Variant,          // t = n - j
    bool  InvariantBefore,  // инвариант выполнен ДО шага
    bool  InvariantAfter,   // инвариант выполнен ПОСЛЕ шага
    bool  GuardHeld,        // B (j < n) выполнен перед шагом
    bool  PostHeld,         // Post выполнен (только когда !Guard)
    string VariantLogEntry  // строка лога: "Шаг N: t до = X → t после = Y (∆ = -1)"
);

/// <summary>
/// Контракт пошагового исполнителя цикла (Роль 1).
/// Хранит массив a, индекс j, результат res, порог T и текущий режим.
/// </summary>
public interface ILoopRunner
{
    /// <summary>Текущий режим (PrefixSum / CountGreaterThan / PrefixMax).</summary>
    LoopMode Mode { get; }

    /// <summary>Массив данных. Задаётся через Reset().</summary>
    IReadOnlyList<int> Array { get; }

    /// <summary>Порог T для режима CountGreaterThan.</summary>
    int Threshold { get; }

    /// <summary>Текущий индекс j.</summary>
    int J { get; }

    /// <summary>Текущее значение res.</summary>
    int Res { get; }

    /// <summary>Вариант-функция t = n − j.</summary>
    int Variant { get; }

    /// <summary>true, если цикл ещё не завершился (j &lt; n).</summary>
    bool CanStep { get; }

    /// <summary>true, если цикл завершился (¬B).</summary>
    bool IsFinished { get; }

    /// <summary>История снимков состояния (один элемент на каждый вызов Step()).</summary>
    IReadOnlyList<LoopStepSnapshot> History { get; }

    /// <summary>Лог убывания варианта-функции (одна строка на шаг).</summary>
    IReadOnlyList<string> VariantLog { get; }

    /// <summary>
    /// Инициализирует / сбрасывает цикл.
    /// </summary>
    /// <param name="mode">Режим работы.</param>
    /// <param name="array">Данные для обработки.</param>
    /// <param name="threshold">Порог T (только для CountGreaterThan, для остальных игнорируется).</param>
    void Reset(LoopMode mode, IReadOnlyList<int> array, int threshold = 0);

    /// <summary>
    /// Выполняет один шаг тела цикла S.
    /// Бросает <see cref="InvalidOperationException"/>, если цикл уже завершился.
    /// </summary>
    /// <returns>Снимок состояния, сделанный на этом шаге.</returns>
    LoopStepSnapshot Step();

    /// <summary>
    /// Запускает цикл до конца и возвращает все снимки.
    /// </summary>
    IReadOnlyList<LoopStepSnapshot> Run();
}

/// <summary>
/// Реализация пошагового исполнителя цикла (Роль 1).
///
/// Поддерживаемые режимы:
///   • PrefixSum       — res = sum(a[0..j))
///   • CountGreaterThan — res = |{i &lt; j : a[i] &gt; T}|
///   • PrefixMax       — res = max(a[0..j))
///
/// На каждом шаге:
///   1. Debug.Assert — инвариант выполнен ДО шага.
///   2. Выполняется тело S.
///   3. Debug.Assert — инвариант выполнен ПОСЛЕ шага.
///   4. Вычисляется t = n − j; Assert t' &lt; t_prev и t' ≥ 0.
///   5. Добавляется запись в VariantLog.
/// </summary>
public sealed class LoopRunner : ILoopRunner
{
    //  внутреннее состояние 
    private int[]  _array    = [];
    private int    _j;
    private int    _res;
    private int    _threshold;
    private LoopMode _mode;
    private int    _stepNumber;

    private readonly List<LoopStepSnapshot> _history    = [];
    private readonly List<string>           _variantLog = [];

    // публичный интерфейс 

    public LoopMode             Mode      => _mode;
    public IReadOnlyList<int>   Array     => _array;
    public int                  Threshold => _threshold;
    public int                  J         => _j;
    public int                  Res       => _res;
    public int                  Variant   => _array.Length - _j;   // t = n − j
    public bool                 CanStep   => _j < _array.Length;
    public bool                 IsFinished => !CanStep;

    public IReadOnlyList<LoopStepSnapshot> History    => _history;
    public IReadOnlyList<string>           VariantLog => _variantLog;

    // Reset 

    /// <inheritdoc/>
    public void Reset(LoopMode mode, IReadOnlyList<int> array, int threshold = 0)
    {
        ArgumentNullException.ThrowIfNull(array);

        _mode      = mode;
        _array     = array.ToArray();   // собственная копия, защита от мутации снаружи
        _threshold = threshold;
        _history.Clear();
        _variantLog.Clear();
        _stepNumber = 0;

        //  Инициализация (init)
        switch (mode)
        {
            case LoopMode.PrefixSum:
                // init: res := 0; j := 0
                _res = 0;
                _j   = 0;
                break;

            case LoopMode.CountGreaterThan:
                // init: res := 0; j := 0
                _res = 0;
                _j   = 0;
                break;

            case LoopMode.PrefixMax:
                // init: res := a[0]; j := 1    (Pre: n ≥ 1)
                if (_array.Length == 0)
                    throw new ArgumentException(
                        "PrefixMax требует непустой массив (Pre: n ≥ 1).", nameof(array));
                _res = _array[0];
                _j   = 1;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Неизвестный режим цикла.");
        }

        // После инициализации инвариант обязан выполняться (Pre ⇒ Inv)
        Debug.Assert(
            CheckInvariant(_mode, _array, _j, _res, _threshold),
            $"[LoopRunner] Нарушение инварианта ПОСЛЕ ИНИЦИАЛИЗАЦИИ: mode={mode}, j={_j}, res={_res}, n={_array.Length}."
        );
    }

    // Step 

    /// <inheritdoc/>
    public LoopStepSnapshot Step()
    {
        if (IsFinished)
            throw new InvalidOperationException(
                $"Цикл уже завершён (j = {_j} = n = {_array.Length}). Вызовите Reset() для нового прогона.");

        _stepNumber++;
        int tBefore = Variant;   // t = n − j перед шагом

        // Assert: инвариант ДО шага 
        bool invBefore = CheckInvariant(_mode, _array, _j, _res, _threshold);
        Debug.Assert(
            invBefore,
            $"[LoopRunner] Нарушение инварианта ДО шага {_stepNumber}: mode={_mode}, j={_j}, res={_res}, n={_array.Length}."
        );

        // Тело цикла S 
        ExecuteBody();

        //  Assert: инвариант ПОСЛЕ шага 
        bool invAfter = CheckInvariant(_mode, _array, _j, _res, _threshold);
        Debug.Assert(
            invAfter,
            $"[LoopRunner] Нарушение инварианта ПОСЛЕ шага {_stepNumber}: mode={_mode}, j={_j}, res={_res}, n={_array.Length}."
        );

        int tAfter = Variant;   // t' = n − j после шага

        //  Assert: вариант-функция строго убывает и остаётся ≥ 0 
        Debug.Assert(
            tAfter < tBefore,
            $"[LoopRunner] Вариант-функция НЕ убывает на шаге {_stepNumber}: t до={tBefore}, t после={tAfter}."
        );
        Debug.Assert(
            tAfter >= 0,
            $"[LoopRunner] Вариант-функция стала отрицательной на шаге {_stepNumber}: t'={tAfter}."
        );

        //  Лог убывания 
        string logEntry = $"Шаг {_stepNumber}: t до = {tBefore} → t после = {tAfter} (∆ = {tAfter - tBefore})";
        _variantLog.Add(logEntry);

        //  Постусловие (только когда цикл завершился на ЭТОМ шаге) 
        bool postHeld = IsFinished && CheckPost(_mode, _array, _res, _threshold);

        var snapshot = new LoopStepSnapshot(
            StepNumber:     _stepNumber,
            J:              _j,
            Res:            _res,
            Variant:        tAfter,
            InvariantBefore: invBefore,
            InvariantAfter:  invAfter,
            GuardHeld:       false,     // Guard уже был ложным — мы шагнули ВНУТРЬ, значит он был истинным; помечаем false как "до следующего шага"
            PostHeld:        postHeld,
            VariantLogEntry: logEntry
        );

        _history.Add(snapshot);
        return snapshot;
    }

    // Run 

    /// <inheritdoc/>
    public IReadOnlyList<LoopStepSnapshot> Run()
    {
        while (CanStep)
            Step();
        return _history;
    }

    // Вспомогательные методы 
    /// <summary>
    /// Выполняет тело цикла для текущего режима (одна итерация).
    /// </summary>
    private void ExecuteBody()
    {
        int j = _j;   // текущий индекс до инкремента

        switch (_mode)
        {
            case LoopMode.PrefixSum:
                // S: res += a[j]; j++
                _res += _array[j];
                _j++;
                break;

            case LoopMode.CountGreaterThan:
                // S: if (a[j] > T) res++; j++
                if (_array[j] > _threshold)
                    _res++;
                _j++;
                break;

            case LoopMode.PrefixMax:
                // S: res = max(res, a[j]); j++
                if (_array[j] > _res)
                    _res = _array[j];
                _j++;
                break;

            default:
                throw new InvalidOperationException($"Неизвестный режим: {_mode}");
        }
    }

    // Проверка инварианта 

    /// <summary>
    /// Вычисляет истинность инварианта цикла конкретно на переданных значениях.
    /// Используется и внутри Step(), и публично из тестов.
    ///
    /// PrefixSum:        res = sum(a[0..j))  ∧  0 ≤ j ≤ n
    /// CountGreaterThan: res = |{i&lt;j : a[i]&gt;T}|  ∧  0 ≤ j ≤ n
    /// PrefixMax:        res = max(a[0..j))  ∧  1 ≤ j ≤ n
    /// </summary>
    public static bool CheckInvariant(
        LoopMode mode, int[] array, int j, int res, int threshold)
    {
        int n = array.Length;

        return mode switch
        {
            LoopMode.PrefixSum =>
                j >= 0 && j <= n &&
                res == PrefixSum(array, j),

            LoopMode.CountGreaterThan =>
                j >= 0 && j <= n &&
                res == CountGreaterThan(array, j, threshold),

            LoopMode.PrefixMax =>
                j >= 1 && j <= n &&
                res == PrefixMax(array, j),

            _ => false
        };
    }

    /// <summary>
    /// Проверяет постусловие по завершении цикла (j = n).
    ///
    /// PrefixSum:        res = sum(a[0..n))
    /// CountGreaterThan: res = |{i&lt;n : a[i]&gt;T}|
    /// PrefixMax:        res = max(a[0..n))
    /// </summary>
    public static bool CheckPost(
        LoopMode mode, int[] array, int res, int threshold)
    {
        int n = array.Length;

        return mode switch
        {
            LoopMode.PrefixSum        => res == PrefixSum(array, n),
            LoopMode.CountGreaterThan => res == CountGreaterThan(array, n, threshold),
            LoopMode.PrefixMax        => n >= 1 && res == PrefixMax(array, n),
            _                         => false
        };
    }

    // Опорные вычисления (эталонные, для инварианта и постусловия) 

    /// <summary>Сумма первых k элементов массива: sum(a[0..k)).</summary>
    private static int PrefixSum(int[] a, int k)
    {
        int s = 0;
        for (int i = 0; i < k; i++) s += a[i];
        return s;
    }

    /// <summary>Число элементов среди первых k, строго больших threshold.</summary>
    private static int CountGreaterThan(int[] a, int k, int threshold)
    {
        int c = 0;
        for (int i = 0; i < k; i++)
            if (a[i] > threshold) c++;
        return c;
    }

    /// <summary>Максимум среди первых k элементов (k ≥ 1).</summary>
    private static int PrefixMax(int[] a, int k)
    {
        int m = a[0];
        for (int i = 1; i < k; i++)
            if (a[i] > m) m = a[i];
        return m;
    }
}
