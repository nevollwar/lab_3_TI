using WpEngine.Core.AST;
using WpEngine.Core.Loops;
using WpEngine.Core.Parsing;
using WpEngine.Core.Services;

namespace WpEngine.Tests;

/// <summary>
/// Модульные тесты для Роли 2 (бэкенд: WpCalculatorEngine, LoopVerifier, проверка 4 условий и Def).
/// </summary>
public class LoopVerifierTests
{
    private readonly WpCalculatorEngine _wpEngine = new();
    private readonly LoopVerifier _verifier = new();

    [Theory]
    [InlineData(LoopMode.PrefixSum)]
    [InlineData(LoopMode.CountGreaterThan)]
    [InlineData(LoopMode.PrefixMax)]
    public void VerifyMode_AllFourConditionsAndDefAreValid(LoopMode mode)
    {
        LoopVerificationReport report = _verifier.VerifyMode(mode);

        Assert.True(report.AllValid);
        Assert.True(report.Initialization.IsValid);
        Assert.True(report.Preservation.IsValid);
        Assert.True(report.Exit.IsValid);
        Assert.True(report.Termination.IsValid);
        Assert.True(report.Definedness.IsValid);
        Assert.Equal(5, report.AllConditions.Count);

        // Проверяем, что wp(S, Inv) реально вычислен и содержит пошаговый трейс
        Assert.NotEmpty(report.WpPreservationResult.Trace);
        Assert.NotEmpty(report.WpVariantDecreaseResult.Trace);

        // Проверяем наличие формулы (Inv ∧ B) ⇒ wp(S, Inv) и русского разбора
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

        // Подстановка res -> res + a[j], j -> j + 1 и Def (0 <= j && j < n)
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

        // Ветвление if (a[j] > T) даёт дизъюнкцию двух веток + Def (0 <= j && j < n)
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
        // Для PrefixMax предусловие требует n >= 1 (непустой массив)
        Assert.Equal("n >= 1", Humanizer.Format(report.Spec.PreCondition));
    }

    [Fact]
    public void WpCalculatorEngine_ExtractsDefForDivisionSqrtAndArrayAccess()
    {
        // y := sqrt(x) / d + a[j]
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
    public void CheckDefinednessAtState_ValidatesBoundsCorrectly(
        LoopMode mode, int length, int j, bool expected)
    {
        Assert.Equal(expected, _verifier.CheckDefinednessAtState(mode, length, j));
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
