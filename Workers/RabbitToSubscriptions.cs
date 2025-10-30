using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using HotChocolate.Subscriptions;
using Messaging.MessageContracts;
using Messaging.RabbitMQ;

namespace GraphGateway.Workers;

/// <summary>
/// Background service that bridges RabbitMQ events into GraphQL subscriptions.
/// Consumes analysis started/completed events from RabbitMQ exchanges and republishes
/// them as HotChocolate subscription events via ITopicEventSender.
/// </summary>
public sealed class RabbitToSubscriptions : BackgroundService
{
    private readonly ITopicEventSender _pub;
    private readonly string _host, _user, _pass;

    // RabbitMQ exchange names
    private const string StartedExchange = Exchanges.AnalysisStarted, CompletedExchange = Exchanges.AnalysisCompleted;

    // GraphQL-specific queues bound to the exchanges
    private const string GraphStartedQueue = "graph.subs.started", GraphCompletedQueue = "graph.subs.completed";

    // HotChocolate subscription topics (must match [Topic(...)] attributes in resolvers)
    private const string StartedTopic = "analysis/started", CompletedTopic = "analysis/completed";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public RabbitToSubscriptions(ITopicEventSender pub, string host, string user, string pass)
    {
        _pub = pub;
        (_host, _user, _pass) = (host, user, pass);
    }

    /// <summary>
    /// Main worker loop: connects to RabbitMQ, consumes from started/completed queues,
    /// and forwards deserialized events into GraphQL subscription topics.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _host,
            UserName = _user,
            Password = _pass
        };

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Establish connection & channel to RabbitMQ
                await using var conn = await factory.CreateConnectionAsync(ct);
                await using var ch = await conn.CreateChannelAsync();

                // --- Ensure exchanges and queues exist ---
                await ch.ExchangeDeclareAsync(StartedExchange, ExchangeType.Fanout, durable: true);
                await ch.ExchangeDeclareAsync(CompletedExchange, ExchangeType.Fanout, durable: true);

                await ch.QueueDeclareAsync(GraphStartedQueue, durable: true, exclusive: false, autoDelete: false);
                await ch.QueueDeclareAsync(GraphCompletedQueue, durable: true, exclusive: false, autoDelete: false);

                await ch.QueueBindAsync(GraphStartedQueue, StartedExchange, "");
                await ch.QueueBindAsync(GraphCompletedQueue, CompletedExchange, "");

                // Consumer for started events
                var started = new AsyncEventingBasicConsumer(ch);
                started.ReceivedAsync += async (_, ea) =>
                {
                    // Deserialize RabbitMQ message to contract
                    var evt = JsonSerializer.Deserialize<AnalysisStarted>(ea.Body.Span, JsonOpts);
                    if (evt is not null)
                    {
                        // Forward into GraphQL subscription pipeline
                        await _pub.SendAsync(StartedTopic, evt, ct);
                    }
                };

                // Consumer for completed events
                var completed = new AsyncEventingBasicConsumer(ch);
                completed.ReceivedAsync += async (_, ea) =>
                {
                    var evt = JsonSerializer.Deserialize<AnalysisCompleted>(ea.Body.Span, JsonOpts);
                    if (evt is not null) await _pub.SendAsync(CompletedTopic, evt, ct);
                };

                // Start consuming both queues (autoAck enabled)
                await ch.BasicConsumeAsync(GraphStartedQueue, autoAck: true, started, ct);
                await ch.BasicConsumeAsync(GraphCompletedQueue, autoAck: true, completed, ct);

                // Stay alive until shutdown or the connection drops
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break; // normal shutdown
            }
            catch
            {
                // On failure: wait a bit and retry
                try { await Task.Delay(TimeSpan.FromSeconds(3), ct); } catch { }
            }
        }
    }
}