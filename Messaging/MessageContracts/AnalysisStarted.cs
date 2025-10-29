namespace Messaging.MessageContracts;

/// <summary>
/// Event published when the analysis has started.
/// Produced by Analysis Orchestrator, consumed by AI Service.
/// </summary>
/// <param name="CorrelationId">Unique identifier tying all events in a single workflow.</param>
/// <param name="ObjectKey">Identifier of the analyzed image.</param>
public record AnalysisStarted(string CorrelationId, string ObjectKey);