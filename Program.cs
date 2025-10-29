using GraphGateway.Auth;
using GraphGateway.GraphQL;
using GraphGateway.GraphQL.Mutations;
using GraphGateway.GraphQL.Subscriptions;
using GraphGateway.Workers;
using Messaging.RabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;
using HotChocolate.Subscriptions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Read required environment variables (fail fast if missing)
static string RequireEnv(string name) =>
    Environment.GetEnvironmentVariable(name)
    ?? throw new InvalidOperationException($"Missing environment variable: {name}");

var rabbitHost = RequireEnv("RABBIT_HOST");
var rabbitUser = RequireEnv("RABBIT_USER");
var rabbitPass = RequireEnv("RABBIT_PASS");

builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType(d => d.Name("Query"))
        .AddTypeExtension<QueryRoot>()
    .AddMutationType(d => d.Name("Mutation"))
        .AddTypeExtension<AnalysisMutations>()
    .AddSubscriptionType(d => d.Name("Subscription"))
        .AddTypeExtension<AnalysisSubscriptions>()
    .AddInMemorySubscriptions();

builder.Services.AddGatewayJwtAuth(builder.Configuration);

builder.Services.AddSingleton(sp => new RabbitMqPublisher(rabbitHost, rabbitUser, rabbitPass));
builder.Services.AddSingleton<ICommandPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
builder.Services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());

builder.Services.AddHostedService(sp =>
    new RabbitToSubscriptions(
        sp.GetRequiredService<ITopicEventSender>(),
        rabbitHost, rabbitUser, rabbitPass));

// Health checks (live/ready)
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddRabbitMQ(
        sp => new ConnectionFactory { HostName = rabbitHost, UserName = rabbitUser, Password = rabbitPass }
                    .CreateConnectionAsync(),
        name: "rabbitmq",
        tags: new[] { "ready" }
    );

// Graceful shutdown: Give background services time to stop cleanly
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(10));

var app = builder.Build();

app.UseWebSockets();
app.UseGatewayJwtAuth();

app.MapGraphQL("/graphql");

// By filtering by tag, we prevent all checks from failing together.
// This way, each endpoint only runs its own checks.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

app.Run();