namespace LexCalculus.Core.Calculators.Common;

/// <summary>
/// Marker base interface — useful for non-generic registry storage and DI.
/// Every concrete calculator implements ICalculator&lt;TInput, TResult&gt;
/// (the generic version below), which inherits from this.
/// </summary>
public interface ICalculator
{
    CalculatorMetadata Metadata { get; }
}

/// <summary>
/// Strongly-typed calculator. Each implementation defines its own input
/// (form values) and result (computed amounts + breakdown rows + notes).
///
/// Calculators MUST be:
/// - Pure with respect to their inputs and parameter values (same input + same
///   parameters at the same EffectiveDate => same output).
/// - Free of side effects (no DB writes, no logging beyond debug, no HTTP calls).
/// - Tolerant of invalid input — return validation errors in the result, do
///   not throw. Throw only for programmer errors (null input).
/// - Async only because parameter loading is async (DB roundtrip). Compute
///   logic itself is synchronous CPU work.
/// </summary>
public interface ICalculator<TInput, TResult> : ICalculator
    where TInput : class
    where TResult : class
{
    Task<TResult> CalculateAsync(TInput input, CancellationToken cancellationToken = default);
}
