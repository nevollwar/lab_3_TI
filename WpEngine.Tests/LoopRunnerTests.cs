using WpEngine.Core.Loops;

namespace WpEngine.Tests;

/// <summary>
/// xUnit-тесты для Роли 1 (LoopRunner): постусловие Post и убывание варианта t на малых массивах.
/// Покрывают все три режима: PrefixSum, CountGreaterThan, PrefixMax.
/// </summary>
public class LoopRunnerTests
{
    private readonly LoopRunner _runner = new();

    // 1. PrefixSum

    [Fact]
    public void PrefixSum_EmptyArray_RunImmediatelyFinishes()
    {
        _runner.Reset(LoopMode.PrefixSum, [], threshold: 0);

        Assert.False(_runner.CanStep);
        Assert.True(_runner.IsFinished);
        Assert.Equal(0, _runner.Res);
        Assert.Equal(0, _runner.Variant);
    }

    [Theory]
    [InlineData(new int[] { 3 },          3)]
    [InlineData(new int[] { 1, 2, 3 },    6)]
    [InlineData(new int[] { -1, 5, -4 },  0)]
    [InlineData(new int[] { 0, 0, 0 },    0)]
    public void PrefixSum_Run_PostConditionHolds(int[] array, int expectedSum)
    {
        _runner.Reset(LoopMode.PrefixSum, array);
        _runner.Run();

        Assert.True(_runner.IsFinished);
        Assert.Equal(expectedSum, _runner.Res);
        Assert.True(LoopRunner.CheckPost(LoopMode.PrefixSum, array, _runner.Res, threshold: 0));
    }

    [Fact]
    public void PrefixSum_VariantStrictlyDecreases()
    {
        int[] array = [10, 20, 30, 40, 50];
        _runner.Reset(LoopMode.PrefixSum, array);

        int prevVariant = _runner.Variant;   // t = n - j перед первым шагом
        while (_runner.CanStep)
        {
            var snap = _runner.Step();
            Assert.True(snap.Variant < prevVariant,
                $"Вариант не убывает на шаге {snap.StepNumber}: было {prevVariant}, стало {snap.Variant}");
            Assert.True(snap.Variant >= 0,
                $"Вариант стал отрицательным на шаге {snap.StepNumber}: t={snap.Variant}");
            prevVariant = snap.Variant;
        }
    }

    [Fact]
    public void PrefixSum_InvariantHoldsBeforeAndAfterEachStep()
    {
        int[] array = [2, 4, 6];
        _runner.Reset(LoopMode.PrefixSum, array);

        while (_runner.CanStep)
        {
            var snap = _runner.Step();
            Assert.True(snap.InvariantBefore,
                $"Инвариант ДО шага {snap.StepNumber} нарушен.");
            Assert.True(snap.InvariantAfter,
                $"Инвариант ПОСЛЕ шага {snap.StepNumber} нарушен.");
        }
    }

    [Fact]
    public void PrefixSum_History_HasExactlyNEntries()
    {
        int[] array = [1, 2, 3, 4];
        _runner.Reset(LoopMode.PrefixSum, array);
        _runner.Run();

        Assert.Equal(array.Length, _runner.History.Count);
    }

    [Fact]
    public void PrefixSum_VariantLog_ContainsDeltaMinus1OnEachStep()
    {
        int[] array = [7, 8, 9];
        _runner.Reset(LoopMode.PrefixSum, array);
        _runner.Run();

        foreach (string entry in _runner.VariantLog)
            Assert.Contains("∆ = -1", entry);
    }

    // 2. CountGreaterThan


    [Theory]
    [InlineData(new int[] { 1, 5, 2, 8, 3 }, 4, 2)]  // 5 и 8 > 4
    [InlineData(new int[] { 1, 2, 3 },        5, 0)]  // все ≤ 5
    [InlineData(new int[] { 6, 7, 8 },        5, 3)]  // все > 5
    [InlineData(new int[] { 5, 5, 5 },        5, 0)]  // строго >, равные не считаются
    [InlineData(new int[] { },                0, 0)]  // пустой массив
    public void CountGreaterThan_Run_PostConditionHolds(int[] array, int threshold, int expectedCount)
    {
        _runner.Reset(LoopMode.CountGreaterThan, array, threshold);
        _runner.Run();

        Assert.Equal(expectedCount, _runner.Res);
        Assert.True(LoopRunner.CheckPost(LoopMode.CountGreaterThan, array, _runner.Res, threshold));
    }

    [Fact]
    public void CountGreaterThan_VariantStrictlyDecreases()
    {
        int[] array = [3, 1, 4, 1, 5];
        _runner.Reset(LoopMode.CountGreaterThan, array, threshold: 2);

        int prevVariant = _runner.Variant;
        while (_runner.CanStep)
        {
            var snap = _runner.Step();
            Assert.True(snap.Variant < prevVariant);
            Assert.True(snap.Variant >= 0);
            prevVariant = snap.Variant;
        }
    }

    [Fact]
    public void CountGreaterThan_InvariantHoldsBeforeAndAfterEachStep()
    {
        int[] array = [10, 1, 20, 2, 30];
        _runner.Reset(LoopMode.CountGreaterThan, array, threshold: 5);

        while (_runner.CanStep)
        {
            var snap = _runner.Step();
            Assert.True(snap.InvariantBefore);
            Assert.True(snap.InvariantAfter);
        }
    }


    // 3. PrefixMax


    [Theory]
    [InlineData(new int[] { 3 },            3)]
    [InlineData(new int[] { 1, 5, 3, 2 },   5)]
    [InlineData(new int[] { -4, -1, -7 },   -1)]
    [InlineData(new int[] { 9, 9, 9 },      9)]
    public void PrefixMax_Run_PostConditionHolds(int[] array, int expectedMax)
    {
        _runner.Reset(LoopMode.PrefixMax, array);
        _runner.Run();

        Assert.Equal(expectedMax, _runner.Res);
        Assert.True(LoopRunner.CheckPost(LoopMode.PrefixMax, array, _runner.Res, threshold: 0));
    }

    [Fact]
    public void PrefixMax_SingleElement_LoopBodyNeverExecutes()
    {
        // Для массива длиной 1: init: res = a[0]; j = 1; B = (1 < 1) = false → цикл не выполняется
        int[] array = [42];
        _runner.Reset(LoopMode.PrefixMax, array);

        Assert.False(_runner.CanStep);
        Assert.Equal(42, _runner.Res);
        Assert.Equal(0, _runner.Variant);
    }

    [Fact]
    public void PrefixMax_EmptyArray_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            _runner.Reset(LoopMode.PrefixMax, []));
    }

    [Fact]
    public void PrefixMax_VariantStrictlyDecreases()
    {
        int[] array = [3, 1, 4, 1, 5, 9];
        _runner.Reset(LoopMode.PrefixMax, array);

        int prevVariant = _runner.Variant;
        while (_runner.CanStep)
        {
            var snap = _runner.Step();
            Assert.True(snap.Variant < prevVariant);
            Assert.True(snap.Variant >= 0);
            prevVariant = snap.Variant;
        }
    }

    [Fact]
    public void PrefixMax_InvariantHoldsBeforeAndAfterEachStep()
    {
        int[] array = [2, 9, 1, 7];
        _runner.Reset(LoopMode.PrefixMax, array);

        while (_runner.CanStep)
        {
            var snap = _runner.Step();
            Assert.True(snap.InvariantBefore);
            Assert.True(snap.InvariantAfter);
        }
    }


    // 4. Step и Run — поведение вокруг завершения


    [Fact]
    public void Step_AfterFinish_ThrowsInvalidOperationException()
    {
        _runner.Reset(LoopMode.PrefixSum, [1]);
        _runner.Run();

        Assert.Throws<InvalidOperationException>(() => _runner.Step());
    }

    [Fact]
    public void Reset_ClearsHistoryAndVariantLog()
    {
        _runner.Reset(LoopMode.PrefixSum, [1, 2, 3]);
        _runner.Run();
        Assert.NotEmpty(_runner.History);

        _runner.Reset(LoopMode.PrefixSum, [5]);
        Assert.Empty(_runner.History);
        Assert.Empty(_runner.VariantLog);
    }

    [Fact]
    public void Run_ReturnsSameSnapshotsAsHistory()
    {
        _runner.Reset(LoopMode.CountGreaterThan, [1, 10, 2, 20], threshold: 5);
        var snapshots = _runner.Run();

        Assert.Equal(_runner.History.Count, snapshots.Count);
        for (int i = 0; i < snapshots.Count; i++)
            Assert.Equal(_runner.History[i], snapshots[i]);
    }

    [Fact]
    public void Run_FinalSnapshot_PostHeld()
    {
        int[] array = [1, 2, 3, 4, 5];
        _runner.Reset(LoopMode.PrefixSum, array);
        var snapshots = _runner.Run();

        // Последний снимок — шаг, после которого цикл завершился
        Assert.True(snapshots[^1].PostHeld,
            "Постусловие должно выполняться после последнего шага.");
    }


    // 5. CheckInvariant — статический метод, используется в GUI


    [Theory]
    // PrefixSum: res = sum(a[0..j)), 0 ≤ j ≤ n
    [InlineData(LoopMode.PrefixSum, new int[] { 1, 2, 3 }, 0, 0,  0, true)]
    [InlineData(LoopMode.PrefixSum, new int[] { 1, 2, 3 }, 2, 3,  0, true)]
    [InlineData(LoopMode.PrefixSum, new int[] { 1, 2, 3 }, 3, 6,  0, true)]
    [InlineData(LoopMode.PrefixSum, new int[] { 1, 2, 3 }, 2, 99, 0, false)] // неверный res
    [InlineData(LoopMode.PrefixSum, new int[] { 1, 2, 3 }, 4, 6,  0, false)] // j > n
    // CountGreaterThan: res = |{i < j : a[i] > T}|, 0 ≤ j ≤ n
    [InlineData(LoopMode.CountGreaterThan, new int[] { 1, 5, 2 }, 0, 0, 3, true)]
    [InlineData(LoopMode.CountGreaterThan, new int[] { 1, 5, 2 }, 2, 1, 3, true)] // только 5 > 3
    [InlineData(LoopMode.CountGreaterThan, new int[] { 1, 5, 2 }, 2, 2, 3, false)]
    // PrefixMax: res = max(a[0..j)), 1 ≤ j ≤ n
    [InlineData(LoopMode.PrefixMax, new int[] { 3, 1, 4 }, 1, 3, 0, true)]
    [InlineData(LoopMode.PrefixMax, new int[] { 3, 1, 4 }, 3, 4, 0, true)]
    [InlineData(LoopMode.PrefixMax, new int[] { 3, 1, 4 }, 0, 3, 0, false)] // j < 1
    [InlineData(LoopMode.PrefixMax, new int[] { 3, 1, 4 }, 2, 1, 0, false)] // неверный max
    public void CheckInvariant_ReturnsExpectedResult(
        LoopMode mode, int[] array, int j, int res, int threshold, bool expected)
    {
        Assert.Equal(expected, LoopRunner.CheckInvariant(mode, array, j, res, threshold));
    }
}
