namespace WpEngine.UI.Models;

/// <summary>
/// Модель одной строки в таблице истории шагов цикла.
/// </summary>
public class LoopStepItemViewModel
{
    public int    StepNumber      { get; init; }
    public int    J               { get; init; }
    public int    Res             { get; init; }
    public int    Variant         { get; init; }
    public bool   InvariantBefore { get; init; }
    public bool   InvariantAfter  { get; init; }
    public bool   PostHeld        { get; init; }
    public string VariantLogEntry { get; init; } = string.Empty;

    /// <summary>Цвет строки: зелёный если оба инварианта истинны, иначе красный.</summary>
    public bool IsHealthy => InvariantBefore && InvariantAfter;
}
