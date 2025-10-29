using HotChocolate.Authorization;
using Messaging.MessageContracts;

namespace GraphGateway.GraphQL.Subscriptions;

[ExtendObjectType("Subscription")]
public class AnalysisSubscriptions
{
    [Authorize(Policy = "RequireApiScope")]
    [Subscribe]
    [Topic("analysis/started")]
    public AnalysisStarted OnAnalysisStarted([EventMessage] AnalysisStarted evt) => evt;

    [Authorize(Policy = "RequireApiScope")]
    [Subscribe]
    [Topic("analysis/completed")]
    public AnalysisCompleted OnAnalysisCompleted([EventMessage] AnalysisCompleted evt) => evt;
}