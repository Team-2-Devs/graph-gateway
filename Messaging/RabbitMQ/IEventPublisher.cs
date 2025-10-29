namespace Messaging.RabbitMQ;

/// <summary>
/// Defines a publisher for events that notifies other services
/// about something that has already happened.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event to a RabbitMQ exchange with the given routing key.
    /// If the exchange does not exist, it is declared first.
    /// </summary>
    /// <typeparam name="T">Type of the message payload.</typeparam>
    /// <param name="exchange">Name of the exchange.</param>
    /// <param name="routingKey">Routing key for the message.</param>
    /// <param name="@event">Message payload (serialized to JSON).</param>
    /// <param name="kind">Exchange type (direct, fanout, topic, header).</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A task that represents the asynchronous publish operation.</returns>
    Task PublishAsync<T>(string exchange, string routingKey, T @event, ExchangeKind kind = ExchangeKind.Fanout, CancellationToken ct = default);
}
