using System;
using System.Collections.Generic;
using EasyNetQ.ChannelDispatcher;
using EasyNetQ.Consumer;
using EasyNetQ.DI;
using EasyNetQ.Events;
using EasyNetQ.Interception;
using EasyNetQ.Logging;
using EasyNetQ.Persistent;
using EasyNetQ.Producer;
using FluentAssertions;
using NSubstitute;
using RabbitMQ.Client;
using Xunit;

namespace EasyNetQ.Tests;

public class AdvancedBusEventHandlersTests : IDisposable
{
    public AdvancedBusEventHandlersTests()
    {
        var advancedBusEventHandlers = new AdvancedBusEventHandlers(
            (_, e) =>
            {
                connectedCalled = true;
                connectedEventArgs = e;
            },
            (_, e) =>
            {
                disconnectedCalled = true;
                disconnectedEventArgs = e;
            },
            (_, e) =>
            {
                blockedCalled = true;
                blockedEventArgs = e;
            },
            (_, _) => unBlockedCalled = true,
            (_, e) =>
            {
                messageReturnedCalled = true;
                messageReturnedEventArgs = e;
            }
        );

        eventBus = new EventBus(Substitute.For<ILogger<EventBus>>());

        advancedBus = new RabbitAdvancedBus(
            Substitute.For<ILogger<RabbitAdvancedBus>>(),
            Substitute.For<IProducerConnection>(),
            Substitute.For<IConsumerConnection>(),
            Substitute.For<IConsumerFactory>(),
            Substitute.For<IPersistentChannelDispatcher>(),
            Substitute.For<IPublishConfirmationListener>(),
            eventBus,
            Substitute.For<IHandlerCollectionFactory>(),
            Substitute.For<IServiceResolver>(),
            Substitute.For<ConnectionConfiguration>(),
            Substitute.For<IEnumerable<IProduceConsumeInterceptor>>(),
            Substitute.For<IMessageSerializationStrategy>(),
            Substitute.For<IConventions>(),
            Substitute.For<IPullingConsumerFactory>(),
            advancedBusEventHandlers,
            Substitute.For<IConsumeScopeProvider>()
        );
    }

    public void Dispose()
    {
        advancedBus.Dispose();
    }

    private readonly IEventBus eventBus;
    private bool connectedCalled;
    private bool disconnectedCalled;
    private bool blockedCalled;
    private BlockedEventArgs blockedEventArgs;
    private bool unBlockedCalled;
    private bool messageReturnedCalled;
    private MessageReturnedEventArgs messageReturnedEventArgs;
    private readonly IAdvancedBus advancedBus;
    private ConnectedEventArgs connectedEventArgs;
    private DisconnectedEventArgs disconnectedEventArgs;

    [Fact]
    public void AdvancedBusEventHandlers_Blocked_handler_is_called()
    {
        var @event = new ConnectionBlockedEvent(PersistentConnectionType.Producer, "a random reason");

        eventBus.Publish(@event);
        blockedCalled.Should().BeTrue();
        blockedEventArgs.Should().NotBeNull();
        blockedEventArgs.Reason.Should().Be(@event.Reason);
    }

    [Fact]
    public void AdvancedBusEventHandlers_Connected_handler_is_called_when_connection_recovered()
    {
        eventBus.Publish(new ConnectionRecoveredEvent(PersistentConnectionType.Producer, new AmqpTcpEndpoint()));
        connectedCalled.Should().BeTrue();
        connectedEventArgs.Hostname.Should().Be("localhost");
        connectedEventArgs.Port.Should().Be(5672);
    }

    [Fact]
    public void AdvancedBusEventHandlers_Connected_handler_is_called_when_connection_created()
    {
        eventBus.Publish(new ConnectionCreatedEvent(PersistentConnectionType.Producer, new AmqpTcpEndpoint()));
        connectedCalled.Should().BeTrue();
        connectedEventArgs.Hostname.Should().Be("localhost");
        connectedEventArgs.Port.Should().Be(5672);
    }

    [Fact]
    public void AdvancedBusEventHandlers_Disconnected_handler_is_called()
    {
        var @event = new ConnectionDisconnectedEvent(
            PersistentConnectionType.Producer, new AmqpTcpEndpoint(), "a random reason"
        );
        eventBus.Publish(@event);
        disconnectedCalled.Should().BeTrue();
        disconnectedEventArgs.Should().NotBeNull();
        disconnectedEventArgs.Hostname.Should().Be("localhost");
        disconnectedEventArgs.Port.Should().Be(5672);
        disconnectedEventArgs.Reason.Should().Be("a random reason");
    }

    [Fact]
    public void AdvancedBusEventHandlers_MessageReturned_handler_is_called()
    {
        var @event = new ReturnedMessageEvent(
            null,
            Array.Empty<byte>(),
            new MessageProperties(),
            new MessageReturnedInfo("my.exchange", "routing.key", "reason")
        );

        eventBus.Publish(@event);
        messageReturnedCalled.Should().BeTrue();
        messageReturnedEventArgs.Should().NotBeNull();
        messageReturnedEventArgs.MessageBody.ToArray().Should().Equal(@event.Body.ToArray());
        messageReturnedEventArgs.MessageProperties.Should().Be(@event.Properties);
        messageReturnedEventArgs.MessageReturnedInfo.Should().Be(@event.Info);
    }

    [Fact]
    public void AdvancedBusEventHandlers_Unblocked_handler_is_called()
    {
        eventBus.Publish(new ConnectionUnblockedEvent(PersistentConnectionType.Producer));
        unBlockedCalled.Should().BeTrue();
    }
}
