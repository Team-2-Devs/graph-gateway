// using System.Text;
// using System.Text.Json;
// using RabbitMQ.Client;

// namespace Messaging.RabbitMQ;

// /// <summary>
// /// RabbitMQ publisher that ensures a single shared connection and
// /// sends commands and publishes events to configured exchanges.
// /// Implements <see cref="ICommandPublisher"/> and <see cref="IEventPublisher"/>
// /// so services can interact with RabbitMQ without transport details.
// /// </summary>
// /// <remarks>
// /// - Commands: point-to-point (Direct/Topic), require a routing key, at-most-one consumer.
// /// - Events: pub/sub (Fanout/Topic), routing key optional for Fanout, many consumers.
// /// </remarks>
// public sealed class RabbitMqPublisher : ICommandPublisher, IEventPublisher, IAsyncDisposable
// {
//     private readonly string _host, _user, _pass;
//     private IConnection? _connection;
//     private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

//     // Ensures that only one caller at a time can initialize the connection
//     private readonly SemaphoreSlim _initLock = new(1, 1);

//     /// <summary>
//     /// Creates a new RabbitMQ publisher with credentials and hostname.
//     /// </summary>
//     public RabbitMqPublisher(string host, string user, string pass)
//         => (_host, _user, _pass) = (host, user, pass);

//     /// <summary>
//     /// Lazily initializes the RabbitMQ connection in a thread-safe manner.
//     /// Ensures only one connection attempt happens at a time.
//     /// </summary>
//     private async Task EnsureConnectionAsync(CancellationToken ct)
//     {
//         // Already connected and open
//         if (_connection is { IsOpen: true }) return;

//         await _initLock.WaitAsync(ct).ConfigureAwait(false);
//         try
//         {
//             if (_connection is { IsOpen: true }) return;

//             var factory = new ConnectionFactory {
//                 HostName = _host,
//                 UserName = _user,
//                 Password = _pass,
//                 AutomaticRecoveryEnabled = true
//             };

//             // Establish new connection
//             _connection = await factory.CreateConnectionAsync(ct).ConfigureAwait(false);
//         }
//         finally
//         {
//             _initLock.Release();
//         }
//     }

//     public async Task SendAsync<T>(string exchange, string routingKey, T command,
//         ExchangeKind kind = ExchangeKind.Direct, CancellationToken ct = default)
//     {
//         // Ensure we have an active connection
//         await EnsureConnectionAsync(ct).ConfigureAwait(false);

//         // Open a lightweight channel for this publish operation
//         await using var channel = await _connection!.CreateChannelAsync().ConfigureAwait(false);

//         // RabbitMQ's ExchangeType class is constants, so we can use the enum name lower-cased.
//         var type = kind.ToString().ToLowerInvariant();

//         // Declare the exchange (idempotent: safe to call repeatedly)
//         await channel.ExchangeDeclareAsync(exchange, type: type, durable: true, autoDelete: false, arguments: null)
//                 .ConfigureAwait(false);

//         // Serialize command to JSON payload
//         var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command, JsonOpts));

//         // Set content type and persistence
//         var props = new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent };

//         // Publish command to exchange with routing key
//         await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, basicProperties: props, body: payload, cancellationToken: ct)
//                 .ConfigureAwait(false);
//     }

//     public async Task PublishAsync<T>(string exchange, string routingKey, T @event,
//         ExchangeKind kind = ExchangeKind.Fanout, CancellationToken ct = default)
//     {
//         // Ensure we have an active connection
//         await EnsureConnectionAsync(ct).ConfigureAwait(false);

//         // Open a lightweight channel for this publish operation
//         await using var channel = await _connection!.CreateChannelAsync().ConfigureAwait(false);

//         // RabbitMQ's ExchangeType class is constants, so we can use the enum name lower-cased.
//         var type = kind.ToString().ToLowerInvariant();

//         // Declare the exchange (idempotent: safe to call repeatedly)
//         await channel.ExchangeDeclareAsync(exchange, type: type, durable: true, autoDelete: false, arguments: null)
//                 .ConfigureAwait(false);

//         // Serialize event to JSON payload
//         var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event, JsonOpts));

//         // Set content type and persistence
//         var props = new BasicProperties { ContentType = "application/json", DeliveryMode = DeliveryModes.Persistent };

//         // Publish event to exchange with routing key
//         await channel.BasicPublishAsync<BasicProperties>(exchange, routingKey, mandatory: false, basicProperties: props, body: payload, cancellationToken: ct)
//                 .ConfigureAwait(false);
//     }

//     /// <summary>
//     /// Disposes the RabbitMQ connection and associated resources.
//     /// </summary>
//     public async ValueTask DisposeAsync()
//     {
//         if (_connection is not null)
//             await _connection.DisposeAsync().ConfigureAwait(false);

//         _initLock.Dispose();
//     }
// }