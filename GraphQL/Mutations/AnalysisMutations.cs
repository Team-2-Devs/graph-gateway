using HotChocolate.Authorization;
using GraphGateway.GraphQL.Types;
using Messaging.MessageContracts;
using Messaging.RabbitMQ;

namespace GraphGateway.GraphQL.Mutations;

[ExtendObjectType("Mutation")]
public class AnalysisMutations
{
    [Authorize(Policy = "RequireApiScope")]
    public async Task<AnalysisRequestPayload> RequestAnalysis(AnalysisRequestInput input,
        [Service] ICommandPublisher publisher, CancellationToken ct)
    {
        string correlationId = Guid.NewGuid().ToString("n");

        var command = new RequestAnalysis(correlationId, input.ObjectKey);

        // Publishes command to Routes.RequestAnalysis
        await publisher.SendAsync(Exchanges.AnalysisCommands, Routes.RequestAnalysis, command, ct: ct);

        return new AnalysisRequestPayload(correlationId);
    }
}