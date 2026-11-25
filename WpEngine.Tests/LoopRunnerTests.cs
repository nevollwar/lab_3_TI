using WpEngine.Core.Loops;

namespace WpEngine.Tests;

/// <summary>Проверки пошагового выполнения циклов и итоговых результатов.</summary>
public class LoopRunnerTests
{
    private readonly LoopRunner _runner = new();

    [Theory]
    [InlineData(new[] { 3 }, 3)]
    [InlineData(new[] { 1, 2, 3 }, 6)]
    [InlineData(new[] { -1, 5, -4 }, 0)]
    [InlineData(new[] { 0, 0, 0 }, 0)]
    [InlineData(new[] { int.MinValue, int.MaxValue }, -1)]
    public void Run_PrefixSum_ReturnsSum(int[] array, int expectedSum)
    {
        CheckRunResult(LoopMode.PrefixSum, array, expectedSum);
    }

    [Theory]
    [InlineData(new[] { 1, 5, 2, 8, 3 }, 4, 2)]
    [InlineData(new[] { 1, 2, 3 }, 5, 0)]
    [InlineData(new[] { 6, 7, 8 }, 5, 3)]
    [InlineData(new[] { 5, 5, 5 }, 5, 0)]
    [InlineData(new[] { -4, -3, -2 }, -3, 1)]
    [InlineData(new[] { int.MinValue, 0, int.MaxValue }, int.MinValue, 2)]
    [InlineData(new[] { int.MinValue, 0, int.MaxValue }, int.MaxValue, 0)]
    public void Run_CountGreaterThan_CountsOnlyValuesAboveThreshold(
        int[] array, int threshold, int expectedCount)
    {
        CheckRunResult(LoopMode.CountGreaterThan, array, expectedCount, threshold);
    }

    [Theory]
    [InlineData(new[] { 1, 5, 3, 2 }, 5)]
    [InlineData(new[] { -4, -1, -7 }, -1)]
    [InlineData(new[] { 9, 9, 9 }, 9)]
    [InlineData(new[] { int.MinValue, int.MaxValue }, int.MaxValue)]
    public void Run_PrefixMax_ReturnsMaximum(int[] array, int expectedMax)
    {
        CheckRunResult(LoopMode.PrefixMax, array, expectedMax);
    }

    [Theory]
    [InlineData(LoopMode.PrefixSum, new int[] { }, 0)]
    [InlineData(LoopMode.CountGreaterThan, new int[] { }, 0)]
    // В PrefixMax первый элемент учитывается при инициализации.
    [InlineData(LoopMode.PrefixMax, new[] { 42 }, 42)]
    public void Reset_WhenGuardIsFalse_AlreadyMeetsPostcondition(LoopMode mode, int[] array, int expected)
    {
        _runner.Reset(mode, array);

        Assert.False(_runner.CanStep);
        Assert.True(_runner.IsFinished);
        Assert.Equal(array.Length, _runner.J);
        Assert.Equal(0, _runner.Variant);
        Assert.Equal(expected, _runner.Res);
        Assert.Empty(_runner.Run());
        Assert.True(LoopRunner.CheckPost(mode, array, _runner.Res, threshold: 0));
    }

    [Fact]
    public void Step_PrefixSum_TracksRunningSum()
    {
        CheckSteps(LoopMode.PrefixSum, [2, -1, 5], initialIndex: 0, initialResult: 0,
            expectedResults: [2, 1, 6]);
    }

    [Fact]
    public void Step_CountGreaterThan_SkipsValuesAtOrBelowThreshold()
    {
        // Тройка равна порогу, поэтому после неё счётчик всё ещё равен единице.
        CheckSteps(LoopMode.CountGreaterThan, [4, 3, 2, 5], initialIndex: 0, initialResult: 0,
            expectedResults: [1, 1, 1, 2], threshold: 3);
    }

    [Theory]
    [InlineData(new[] { 2, 9, 1, 7 }, new[] { 9, 9, 9 })]
    [InlineData(new[] { 9, 1, 7 }, new[] { 9, 9 })]
    public void Step_PrefixMax_TracksRunningMaximum(int[] array, int[] expectedResults)
    {
        CheckSteps(LoopMode.PrefixMax, array, initialIndex: 1, initialResult: array[0],
            expectedResults: expectedResults);
    }

    [Fact]
    public void Run_AfterStep_ContinuesFromCurrentState()
    {
        _runner.Reset(LoopMode.CountGreaterThan, [1, 10, 2, 20], threshold: 5);
        var first = _runner.Step();

        var steps = _runner.Run().ToArray();

        Assert.Equal(first, steps[0]);
        Assert.Equal(new[] { 0, 1, 1, 2 }, steps.Select(step => step.Res));
        Assert.Equal(steps, _runner.History.ToArray());
        Assert.Equal(steps, _runner.Run().ToArray());
    }

    [Fact]
    public void Reset_SwitchesModeAndStartsNewHistory()
    {
        _runner.Reset(LoopMode.PrefixSum, [1, 2, 3]);
        _runner.Step();

        _runner.Reset(LoopMode.CountGreaterThan, [4, 5], threshold: 4);

        Assert.Equal(LoopMode.CountGreaterThan, _runner.Mode);
        Assert.Equal(4, _runner.Threshold);
        Assert.Equal(0, _runner.J);
        Assert.Equal(0, _runner.Res);
        Assert.Empty(_runner.History);
        Assert.Empty(_runner.VariantLog);

        var steps = _runner.Run();

        Assert.Equal(1, steps[0].StepNumber);
        Assert.Equal(1, steps[^1].Res);
    }

    [Fact]
    public void Reset_CopiesInputArray()
    {
        int[] array = [1, 2];
        _runner.Reset(LoopMode.PrefixSum, array);
        // Если Reset сохранил ссылку на массив, сумма получится 100.
        array[1] = 99;

        Assert.Equal(new[] { 1, 2 }, _runner.Array);
        _runner.Run();

        Assert.Equal(3, _runner.Res);
    }

    [Fact]
    public void Reset_PrefixMaxWithEmptyArray_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>("array", () => _runner.Reset(LoopMode.PrefixMax, []));
    }

    [Fact]
    public void Reset_NullArray_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("array", () => _runner.Reset(LoopMode.PrefixSum, null!));
    }

    [Fact]
    public void Step_AfterFinish_ThrowsInvalidOperationException()
    {
        _runner.Reset(LoopMode.PrefixSum, [1]);
        _runner.Step();

        Assert.Throws<InvalidOperationException>(() => _runner.Step());
        Assert.Single(_runner.History);
        Assert.Single(_runner.VariantLog);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 6, true)]
    [InlineData(2, 99, false)]
    [InlineData(-1, 0, false)]
    [InlineData(4, 6, false)]
    public void CheckInvariant_PrefixSum_ValidatesIndexAndSum(int j, int sum, bool expected)
    {
        Assert.Equal(expected, LoopRunner.CheckInvariant(LoopMode.PrefixSum, [1, 2, 3], j, sum, threshold: 0));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(-1, 0, false)]
    [InlineData(4, 1, false)]
    public void CheckInvariant_CountGreaterThan_ValidatesCount(int j, int count, bool expected)
    {
        Assert.Equal(expected, LoopRunner.CheckInvariant(LoopMode.CountGreaterThan, [1, 5, 2], j, count, threshold: 3));
    }

    [Theory]
    [InlineData(1, 3, true)]
    [InlineData(3, 4, true)]
    [InlineData(0, 3, false)]
    [InlineData(2, 1, false)]
    public void CheckInvariant_PrefixMax_ValidatesIndexAndMaximum(int j, int max, bool expected)
    {
        Assert.Equal(expected, LoopRunner.CheckInvariant(LoopMode.PrefixMax, [3, 1, 4], j, max, threshold: 0));
    }

    [Theory]
    [InlineData(LoopMode.PrefixSum, new[] { 1, 2 }, 1, 0)]
    [InlineData(LoopMode.CountGreaterThan, new[] { 2, 3 }, 2, 2)]
    [InlineData(LoopMode.PrefixMax, new[] { -4, -1 }, 0, 0)]
    [InlineData(LoopMode.PrefixMax, new int[] { }, 0, 0)]
    public void CheckPost_RejectsInvalidResult(LoopMode mode, int[] array, int res, int threshold)
    {
        Assert.False(LoopRunner.CheckPost(mode, array, res, threshold));
    }

    [Fact]
    public void Reset_UnknownMode_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>("mode", () => _runner.Reset((LoopMode)(-1), [1]));
    }

    [Fact]
    public void CheckInvariant_UnknownMode_ReturnsFalse()
    {
        Assert.False(LoopRunner.CheckInvariant((LoopMode)(-1), [1], j: 0, res: 0, threshold: 0));
    }

    [Fact]
    public void CheckPost_UnknownMode_ReturnsFalse()
    {
        Assert.False(LoopRunner.CheckPost((LoopMode)(-1), [1], res: 1, threshold: 0));
    }

    private void CheckRunResult(LoopMode mode, int[] array, int expected, int threshold = 0)
    {
        _runner.Reset(mode, array, threshold);

        var steps = _runner.Run();

        Assert.True(_runner.IsFinished);
        Assert.Equal(array.Length, _runner.J);
        Assert.Equal(0, _runner.Variant);
        Assert.Equal(expected, _runner.Res);
        Assert.True(steps[^1].PostHeld);
        Assert.True(LoopRunner.CheckPost(mode, array, _runner.Res, threshold));
    }

    private void CheckSteps(LoopMode mode, int[] array, int initialIndex, int initialResult,
        int[] expectedResults, int threshold = 0)
    {
        _runner.Reset(mode, array, threshold);

        Assert.Equal(initialIndex, _runner.J);
        Assert.Equal(initialResult, _runner.Res);
        Assert.Equal(expectedResults.Length, _runner.Variant);

        for (int i = 0; i < expectedResults.Length; i++)
        {
            Assert.True(_runner.CanStep);

            int stepNumber = i + 1;
            var step = _runner.Step();

            Assert.Equal(stepNumber, step.StepNumber);
            Assert.Equal(initialIndex + stepNumber, step.J);
            Assert.Equal(expectedResults[i], step.Res);
            Assert.Equal(expectedResults.Length - stepNumber, step.Variant);
            Assert.True(step.InvariantBefore);
            Assert.True(step.InvariantAfter);
            Assert.Equal(stepNumber == expectedResults.Length, step.PostHeld);
            Assert.Contains("∆ = -1", step.VariantLogEntry);
        }

        Assert.False(_runner.CanStep);
        Assert.Equal(expectedResults.Length, _runner.History.Count);
        Assert.Equal(_runner.History.Select(step => step.VariantLogEntry), _runner.VariantLog);
    }
}
