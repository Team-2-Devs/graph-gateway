namespace GraphGateway.GraphQL;

[ExtendObjectType("Query")]
public class QueryRoot
{
    public string Ping() => "pong";
}