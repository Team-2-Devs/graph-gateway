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
    private readonly ITopicEventSender _publisher;
    private readonly string _host, _user, _pass;

    // RabbitMQ exchange names
    private const string _startedExchange = Exchanges.AnalysisStarted, _completedExchange = Exchanges.AnalysisCompleted;

    // GraphQL-specific queues bound to the exchanges
    private const string _graphStartedQueue = "graph.subs.started", _graphCompletedQueue = "graph.subs.completed";

    // HotChocolate subscription topics (must match [Topic(...)] attributes in resolvers)
    private const string _startedTopic = "analysis/started", _completedTopic = "analysis/completed";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public RabbitToSubscriptions(ITopicEventSender publisher, string host, string user, string pass)
    {
        _publisher = publisher;
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
                await ch.ExchangeDeclareAsync(_startedExchange, ExchangeType.Fanout, durable: true);
                await ch.ExchangeDeclareAsync(_completedExchange, ExchangeType.Fanout, durable: true);

                await ch.QueueDeclareAsync(_graphStartedQueue, durable: true, exclusive: false, autoDelete: false);
                await ch.QueueDeclareAsync(_graphCompletedQueue, durable: true, exclusive: false, autoDelete: false);

                await ch.QueueBindAsync(_graphStartedQueue, _startedExchange, "");
                await ch.QueueBindAsync(_graphCompletedQueue, _completedExchange, "");

                // Consumer for started events
                var started = new AsyncEventingBasicConsumer(ch);
                started.ReceivedAsync += async (_, ea) =>
                {
                    // Deserialize RabbitMQ message to contract
                    var evt = JsonSerializer.Deserialize<AnalysisStarted>(ea.Body.Span, JsonOpts);
                    if (evt is not null)
                    {
                        // Forward into GraphQL subscription pipeline
                        await _publisher.SendAsync(_startedTopic, evt, ct);
                    }
                };

                // Consumer for completed events
                var completed = new AsyncEventingBasicConsumer(ch);
                completed.ReceivedAsync += async (_, ea) =>
                {
                    var evt = JsonSerializer.Deserialize<AnalysisCompleted>(ea.Body.Span, JsonOpts);
                    if (evt is not null) await _publisher.SendAsync(_completedTopic, evt, ct);
                };

                // Start consuming both queues (autoAck enabled)
                await ch.BasicConsumeAsync(_graphStartedQueue, autoAck: true, started, ct);
                await ch.BasicConsumeAsync(_graphCompletedQueue, autoAck: true, completed, ct);

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