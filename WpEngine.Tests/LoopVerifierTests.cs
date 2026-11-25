using WpEngine.Core.AST;
using WpEngine.Core.Loops;
using WpEngine.Core.Parsing;
using WpEngine.Core.Services;

namespace WpEngine.Tests;

/// <summary>Проверки формул и условий корректности, которые строит LoopVerifier.</summary>
public class LoopVerifierTests
{
    private readonly WpCalculatorEngine _wpEngine = new();
    private readonly LoopVerifier _verifier = new();

    [Theory]
    [InlineData(LoopMode.PrefixSum)]
    [InlineData(LoopMode.CountGreaterThan)]
    [InlineData(LoopMode.PrefixMax)]
    public void VerifyMode_ReportsAllConditionsAndTheirTraces(LoopMode mode)
    {
        LoopVerificationReport report = _verifier.VerifyMode(mode);

        Assert.True(report.AllValid);
        Assert.True(report.Initialization.IsValid);
        Assert.True(report.Preservation.IsValid);
        Assert.True(report.Exit.IsValid);
        Assert.True(report.Termination.IsValid);
        Assert.True(report.Definedness.IsValid);
        Assert.Equal(
            new[] { "Init", "Preservation", "Exit", "Termination", "Def" },
            report.AllConditions.Select(condition => condition.Id));

        Assert.NotEmpty(report.WpPreservationResult.Trace);
        Assert.NotEmpty(report.WpVariantDecreaseResult.Trace);

        Assert.Contains("⇒", report.PreservationFormula);
        Assert.Contains("Пошаговый расчёт wp(S, Inv)", report.PreservationHumanExplanation);
        Assert.Contains("Разбор формулы словами", report.PreservationHumanExplanation);
        Assert.Contains("Значения знаков", report.PreservationHumanExplanation);
    }

    [Fact]
    public void VerifyMode_PrefixSum_ComputesExactWpWithSubstitutionAndDef()
    {
        LoopVerificationReport report = _verifier.VerifyMode(LoopMode.PrefixSum);
        string wpText = report.WpPreservationResult.FinalConditionShort;

        Assert.Contains("0 <= j", wpText);
        Assert.Contains("j < n", wpText);
        Assert.Contains("res + a[j] == sum(a[0..j + 1))", wpText);
        Assert.Contains("j + 1 <= n", wpText);
    }

    [Fact]
    public void VerifyMode_CountGreaterThan_ComputesIfBranchingAndDef()
    {
        LoopVerificationReport report = _verifier.VerifyMode(LoopMode.CountGreaterThan);
        string wpText = report.WpPreservationResult.FinalConditionShort;

        Assert.Contains("0 <= j", wpText);
        Assert.Contains("j < n", wpText);
        Assert.Contains("a[j] > T", wpText);
        Assert.Contains("||", wpText);
        Assert.Contains("res + 1 == count(a[0..j + 1) > T)", wpText);
        Assert.Contains("res == count(a[0..j + 1) > T)", wpText);
    }

    [Fact]
    public void VerifyMode_PrefixMax_ComputesMaxSubstitutionAndNonEmptyPre()
    {
        LoopVerificationReport report = _verifier.VerifyMode(LoopMode.PrefixMax);
        string wpText = report.WpPreservationResult.FinalConditionShort;

        Assert.Contains("max(res, a[j]) == max(a[0..j + 1))", wpText);
        Assert.Contains("1 <= j + 1", wpText);
        Assert.Contains("0 <= j", wpText);
        Assert.Contains("j < n", wpText);
        Assert.Equal("n >= 1", Humanizer.Format(report.Spec.PreCondition));
    }

    [Fact]
    public void WpCalculatorEngine_ExtractsDefForDivisionSqrtAndArrayAccess()
    {
        var expr = new BinaryExpr(
            new BinaryExpr(new SqrtExpr(new VariableExpr("x")), BinaryOp.Div, new VariableExpr("d")),
            BinaryOp.Add,
            new ArrayAccessExpr("a", new VariableExpr("j"))
        );

        var stmt = new AssignmentStatement("y", expr);
        var post = new RelationalCondition(new VariableExpr("y"), RelationalOp.Greater, new NumberExpr(0));

        var result = _wpEngine.CalculateWp(stmt, post);
        string wpText = result.FinalConditionShort;

        Assert.Contains("d != 0", wpText);
        Assert.Contains("x >= 0", wpText);
        Assert.Contains("0 <= j", wpText);
        Assert.Contains("j < n", wpText);
    }

    [Theory]
    [InlineData((LoopMode)(-1), 1, 0, false)]
    [InlineData(LoopMode.PrefixSum, -1, 0, false)]
    [InlineData(LoopMode.PrefixSum, 0, 0, true)]
    [InlineData(LoopMode.PrefixSum, 5, 3, true)]
    [InlineData(LoopMode.PrefixSum, 5, -1, false)]
    [InlineData(LoopMode.PrefixSum, 5, 6, false)]
    [InlineData(LoopMode.CountGreaterThan, 0, 0, true)]
    [InlineData(LoopMode.CountGreaterThan, 4, 4, true)]
    [InlineData(LoopMode.PrefixMax, 0, 0, false)]
    [InlineData(LoopMode.PrefixMax, 1, 1, true)]
    [InlineData(LoopMode.PrefixMax, 5, 0, false)]
    [InlineData(LoopMode.PrefixMax, 5, 5, true)]
    [InlineData(LoopMode.PrefixMax, 5, 6, false)]
    public void CheckDefinednessAtState_ValidatesBoundsCorrectly(
        LoopMode mode, int length, int j, bool expected)
    {
        Assert.Equal(expected, _verifier.CheckDefinednessAtState(mode, length, j));
    }

    [Theory]
    [InlineData(LoopMode.PrefixSum)]
    [InlineData(LoopMode.CountGreaterThan)]
    [InlineData(LoopMode.PrefixMax)]
    public void VerifyMode_TerminationUsesNextIndex(LoopMode mode)
    {
        var report = _verifier.VerifyMode(mode);

        // t0 — значение варианта до шага.
        Assert.Contains("n - (j + 1) < t0", report.WpVariantDecreaseResult.FinalConditionShort);
        Assert.Contains("t0 = n - j", report.Termination.ExpandedFormula);
        Assert.Contains("n - j ≥ 0", report.Termination.ExpandedFormula);
    }

    [Fact]
    public void VerifyMode_PrefixMax_IncludesBoundsForInitializationAndBody()
    {
        var report = _verifier.VerifyMode(LoopMode.PrefixMax);

        // При инициализации читаем a[0], а в теле цикла — a[j].
        Assert.Contains("0 < n", report.Initialization.ExpandedFormula);
        Assert.Contains("0 < n", report.Definedness.ExpandedFormula);
        Assert.Contains("0 ≤ j", report.Definedness.ExpandedFormula);
        Assert.Contains("j < n", report.Definedness.ExpandedFormula);
    }

    [Fact]
    public void VerifySpec_UsesProvidedPostconditionInExitFormula()
    {
        var postcondition = new CodeParser().ParseCondition("res >= 0");
        var spec = _verifier.GetSpec(LoopMode.CountGreaterThan) with { PostCondition = postcondition };

        var report = _verifier.VerifySpec(spec);

        Assert.EndsWith("⇒ res ≥ 0", report.Exit.ExpandedFormula);
    }

    [Theory]
    [InlineData(true, "Ограничения определённости Def")]
    [InlineData(false, "Дополнительных ограничений определённости")]
    public void VerifySpec_UnknownMode_RejectsSpecAndReportsDefinedness(
        bool bodyReadsArray, string expectedExplanation)
    {
        var preset = _verifier.GetSpec(LoopMode.PrefixSum);
        var spec = preset with { Mode = (LoopMode)(-1) };

        // В теле пресета есть a[j]. Для второго случая оставляем только j := j + 1.
        if (!bodyReadsArray)
        {
            const string bodyCode = "j := j + 1";
            spec = spec with { LoopBody = new CodeParser().ParseProgram(bodyCode), LoopBodyCode = bodyCode };
        }

        var report = _verifier.VerifySpec(spec);

        Assert.False(report.AllValid);
        Assert.False(report.Initialization.IsValid);
        Assert.False(report.Preservation.IsValid);
        Assert.False(report.Exit.IsValid);
        Assert.False(report.Termination.IsValid);
        Assert.Contains(expectedExplanation, report.Definedness.HumanExplanation);
    }

    [Fact]
    public void DescribeFormulaRussian_ExplainsNonStrictComparisonAndInequality()
    {
        var condition = new CodeParser().ParseCondition("x >= 0 && x != 1");

        string description = _verifier.DescribeFormulaRussian(condition);

        Assert.Contains("«≥» — больше или равно", description);
        Assert.Contains("«≠» — не равно", description);
    }

    [Fact]
    public void DescribeFormulaRussian_TrueHasNoOperators()
    {
        string description = _verifier.DescribeFormulaRussian(new BooleanLiteralCondition(true));

        Assert.Contains("логических операторов нет.", description);
    }

    [Fact]
    public void DescribeFormulaRussian_IncludesFormulaWordsAndSymbolLegend()
    {
        var cond = new ImpliesCondition(
            new RelationalCondition(new VariableExpr("j"), RelationalOp.Less, new VariableExpr("n")),
            new RelationalCondition(new VariableExpr("j"), RelationalOp.LessOrEqual, new VariableExpr("n"))
        );

        string description = _verifier.DescribeFormulaRussian(cond);

        Assert.Contains("⇒", description);
        Assert.Contains("если", description);
        Assert.Contains("Разбор знаков:", description);
        Assert.Contains("следование", description);
    }
}
