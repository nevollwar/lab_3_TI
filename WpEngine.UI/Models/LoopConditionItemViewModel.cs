using WpEngine.UI.ViewModels;

namespace WpEngine.UI.Models;

/// <summary>
/// Модель одного условия корректности цикла для отображения в списке.
/// </summary>
public class LoopConditionItemViewModel : ViewModelBase
{
    public required string Title                { get; init; }
    public required string Formula              { get; init; }
    public required string ExpandedFormula      { get; init; }
    public          string FormulaLatex         { get; init; } = string.Empty;
    public          string ExpandedFormulaLatex { get; init; } = string.Empty;
    public required string HumanExplanation     { get; init; }
    public required string ProofSummary         { get; init; }
    public          bool   IsValid              { get; init; }

    /// <summary>Метка «✓ Доказано» / «✗ Не доказано» для индикатора.</summary>
    public string StatusText => IsValid ? "✓ Доказано" : "✗ Не доказано";

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}
