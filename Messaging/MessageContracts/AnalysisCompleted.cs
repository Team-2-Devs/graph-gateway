namespace Messaging.MessageContracts;

/// <summary>
/// Event published when the analysis has finished (success or failure).
/// Produced by Analysis Orchestrator, consumed by the Graph Gateway,
/// where GraphQL subscriptions broadcast updates to clients
/// </summary>
/// <param name="CorrelationId">Unique identifier tying all events in a single workflow.</param>
/// <param name="ObjectKey">Identifier of the analyzed image.</param>
/// <param name="Success">Indicates whether the analysis completed successfully.</param>
public record AnalysisCompleted(string CorrelationId, string ObjectKey, bool Success);