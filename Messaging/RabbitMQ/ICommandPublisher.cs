namespace Messaging.RabbitMQ;

/// <summary>
/// Defines a publisher for commands that notifies the service
/// that should perform an action.
/// </summary>
public interface ICommandPublisher
{
    /// <summary>
    /// Publishes a command to a RabbitMQ exchange with the given routing key.
    /// If the exchange does not exist, it is declared first.
    /// </summary>
    /// <typeparam name="T">Type of the message payload.</typeparam>
    /// <param name="exchange">Name of the exchange.</param>
    /// <param name="routingKey">Routing key for the message.</param>
    /// <param name="command">Message payload (serialized to JSON).</param>
    /// <param name="kind">Exchange type (direct, fanout, topic, header).</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    Task SendAsync<T>(string exchange, string routingKey, T command, ExchangeKind kind = ExchangeKind.Direct, CancellationToken ct = default);
}
