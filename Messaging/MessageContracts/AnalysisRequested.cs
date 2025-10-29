namespace Messaging.MessageContracts;

/// <summary>
/// Command asking the orchestrator to start an analysis.
/// Produced by Graph Gateway, consumed by Analysis Orchestrator.
/// </summary>
/// <param name="CorrelationId">Unique identifier tying all events in a single workflow.</param>
/// <param name="ObjectKey">Identifier of the analyzed image.</param>
public record RequestAnalysis(string CorrelationId, string ObjectKey);