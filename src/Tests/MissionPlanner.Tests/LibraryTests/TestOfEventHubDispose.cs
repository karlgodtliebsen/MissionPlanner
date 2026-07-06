using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Library.EventHub;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Tests.LibraryTests;

/// <summary>
/// 
/// </summary>
public class TestOfEventHubDispose
{
    private readonly ILogger<EventHub> logger = NSubstitute.Substitute.For<ILogger<EventHub>>();
    private readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

    [Fact]
    public void VerifyEventSubscriptionCanBeCreatedAndPublishesIsWorking()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;
        var disposable1 = hub.Subscribe("testing", () => count++);
        disposable1.Should().NotBeNull();

        var disposable2 = hub.Subscribe("testing", () => count++);
        disposable2.Should().NotBeNull();

        var disposable3 = hub.Subscribe("testing", () => count++);
        disposable3.Should().NotBeNull();

        hub.Publish("testing");
        count.Should().Be(3);
        //don't touch in this test: disposable.Dispose();
        hub.Publish("testing");
        count.Should().Be(6);
    }

    [Fact]
    public async Task VerifyEventSubscriptionCanBeCreatedAndPublishesIsWorkingAsync()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;
        var disposable1 = hub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable1.Should().NotBeNull();

        var disposable2 = hub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable2.Should().NotBeNull();

        var disposable3 = hub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable3.Should().NotBeNull();

        await hub.PublishAsync("testing", cancellationToken);
        count.Should().Be(3);
        //don't touch in this test: disposable.Dispose();
        await hub.PublishAsync("testing", cancellationToken);
        count.Should().Be(6);
    }

    [Fact]
    public void VerifyEventSubscriptionCanBeCreatedAndReturnIDisposable()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;
        var disposable = hub.Subscribe("testing", () => count++);
        disposable.Should().NotBeNull();
        hub.Publish("testing");
        count.Should().Be(1);

        disposable.Dispose();
        hub.Publish("testing");
        count.Should().Be(1);
    }

    [Fact]
    public void VerifyEventSubscriptionUsingGenericCanBeCreatedAndReturnIDisposable()
    {
        IEventHub hub = new EventHub(logger);
        var message = "";
        var disposable = hub.Subscribe<string>("testing", (m) => message += m);
        disposable.Should().NotBeNull();
        hub.Publish<string>("testing", "hello world");
        message.Should().Be("hello world");

        disposable.Dispose();
        hub.Publish<string>("testing", "hello world");
        message.Should().Be("hello world");
    }

    [Fact]
    public void VerifyEventSubscriptionUsingPayloadCanBeCreatedAndReturnIDisposable()
    {
        IEventHub hub = new EventHub(logger);
        var message = "";
        var p = new DomainEvent<string>("testing", "hello universe");

        var disposable = hub.Subscribe<DomainEvent<string>>("testing", (m) => message += m.GetData());

        disposable.Should().NotBeNull();
        hub.Publish<DomainEvent<string>>("testing", p);
        message.Should().Be("hello universe");

        disposable.Dispose();
        hub.Publish<DomainEvent<string>>("testing", p);
        message.Should().Be("hello universe");
    }

    [Fact]
    public void VerifyEventSubscriptionCanBeCreatedAndSubscribedToMultipleTimesAndReturnIDisposable()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;

        int Increase(int m)
        {
            return count += m;
        }

        var disposables = new Disposables();
        for (var i = 0; i < 100; i++)
        {
            var disposable = hub.Subscribe<int>("testing", (m) => Increase(m));
            disposable.Should().NotBeNull();
            disposables.Add(disposable);
        }

        hub.Publish<int>("testing", 1);
        count.Should().Be(100);
        disposables.Dispose();
    }

    [Fact]
    public void VerifyEventSubscriptionCanBeCreatedAnd2TimesSubscribedToMultipleTimesAndReturnIDisposable()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;

        int Increase1(int m)
        {
            return count += m;
        }

        int Increase2(int m)
        {
            return count += m;
        }

        var disposables = new Disposables();
        for (var i = 0; i < 100; i++)
        {
            var disposable = hub.Subscribe<int>("testing", (m) => Increase1(m));
            disposable.Should().NotBeNull();
            disposables.Add(disposable);
        }

        for (var i = 0; i < 100; i++)
        {
            var disposable = hub.Subscribe<int>("testing", (m) => Increase2(m));
            disposable.Should().NotBeNull();
            disposables.Add(disposable);
        }

        hub.Publish<int>("testing", 1);
        count.Should().Be(200);

        disposables.Dispose();
    }

    [Fact]
    public void VerifySubscriptionMultipleTimesCanRemoveTheCorrectSubscription()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;

        int Increase1(int m)
        {
            return count += m + 1;
        }

        int Increase2(int m)
        {
            return count += m;
        }

        var disposables = new Disposables();

        var disposable = hub.Subscribe<int>("testing", (m) => Increase1(m));
        disposable.Should().NotBeNull();
        disposables.Add(disposable);


        disposable = hub.Subscribe<int>("testing", (m) => Increase2(m));
        disposable.Should().NotBeNull();
        disposables.Add(disposable);

        hub.Publish<int>("testing", 1);
        count.Should().Be(3);
        count = 0;
        disposables.First().Dispose();
        hub.Publish<int>("testing", 1);
        count.Should().Be(1);
        disposables.Dispose();
    }

    [Fact]
    public void VerifySubscriptionMultipleTimesCanRemoveTheCorrectLastSubscription()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;

        int Increase1(int m)
        {
            return count += m + 1;
        }

        int Increase2(int m)
        {
            return count += m;
        }

        var disposables = new Disposables();

        var disposable = hub.Subscribe<int>("testing", (m) => Increase1(m));
        disposable.Should().NotBeNull();
        disposables.Add(disposable);


        disposable = hub.Subscribe<int>("testing", (m) => Increase2(m));
        disposable.Should().NotBeNull();
        disposables.Add(disposable);

        hub.Publish<int>("testing", 1);
        count.Should().Be(3);
        count = 0;
        disposables.Last().Dispose();
        hub.Publish<int>("testing", 1);
        count.Should().Be(2);
        disposables.Dispose();
    }

    [Fact]
    public async Task VerifyEventAsyncSubscriptionCanBeCreatedAndPublishesIsWorking()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;
        var disposable = hub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable.Should().NotBeNull();
        await hub.PublishAsync("testing", cancellationToken);
        count.Should().Be(1);
        //dont touch in this test: disposable.Dispose();
        await hub.PublishAsync("testing", cancellationToken);
        count.Should().Be(2);
    }

    [Fact]
    public async Task VerifyEventAsyncSubscriptionCanBeCreatedAndReturnIDisposable()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;
        var disposable = hub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable.Should().NotBeNull();
        await hub.PublishAsync("testing", cancellationToken);
        count.Should().Be(1);

        disposable.Dispose();
        await hub.PublishAsync("testing", cancellationToken);
        count.Should().Be(1);
    }

    [Fact]
    public async Task VerifyEventAsyncSubscriptionUsingGenericCanBeCreatedAndReturnIDisposable()
    {
        IEventHub hub = new EventHub(logger);
        var message = "";
        var disposable = hub.SubscribeAsync<string>("testing", (m, ct) =>
        {
            message += m;
            return Task.CompletedTask;
        });
        disposable.Should().NotBeNull();
        await hub.PublishAsync<string>("testing", "hello world", cancellationToken);
        message.Should().Be("hello world");

        disposable.Dispose();
        await hub.PublishAsync<string>("testing", "hello world", cancellationToken);
        message.Should().Be("hello world");
    }

    [Fact]
    public async Task VerifyEventAsyncSubscriptionUsingPayloadCanBeCreatedAndReturnIDisposable()
    {
        IEventHub hub = new EventHub(logger);
        var message = "";
        var p = new DomainEvent<string>("testing", "hello universe");

        var disposable = hub.SubscribeAsync<DomainEvent<string>>("testing", (m, ct) =>
        {
            message += m.GetData();
            return Task.CompletedTask;
        });

        disposable.Should().NotBeNull();
        await hub.PublishAsync<DomainEvent<string>>("testing", p, cancellationToken);
        message.Should().Be("hello universe");

        disposable.Dispose();
        await hub.PublishAsync<DomainEvent<string>>("testing", p, cancellationToken);
        message.Should().Be("hello universe");
    }

    [Fact]
    public async Task VerifyEventAsyncSubscriptionCanBeCreatedAndSubscribedToMultipleTimesAndReturnIDisposable()
    {
        IEventHub hub = new EventHub(logger);
        var count = 0;

        int Increase(int m)
        {
            return count += m;
        }

        var disposables = new Disposables();
        for (var i = 0; i < 100; i++)
        {
            var disposable = hub.SubscribeAsync<int>("testing", (m, ct) =>
            {
                Increase(m);
                return Task.CompletedTask;
            });
            disposable.Should().NotBeNull();
            disposables.Add(disposable);
        }

        await hub.PublishAsync<int>("testing", 1, cancellationToken);
        count.Should().Be(100);
        disposables.Dispose();
    }

    [Fact]
    public async Task VerifyEventSubscriptionCanBeCreatedAndSubscribedToMultipleTimesAndAllSubscriptionIsInvoked()
    {
        IEventHub eventHub = new EventHub(logger);
        var count = 0;

        int Increase(int m)
        {
            return count += m;
        }

        var disposables = new Disposables();
        for (var i = 0; i < 100; i++)
        {
            var disposable = eventHub.SubscribeAsync<int>("testing", (m, ct) =>
            {
                Increase(m);
                return Task.CompletedTask;
            });
            disposable.Should().NotBeNull();
            disposables.Add(disposable);
        }

        for (var i = 0; i < 100; i++)
        {
            var disposable = eventHub.SubscribeAll((m) => { count++; });
            disposable.Should().NotBeNull();
            disposables.Add(disposable);
        }

        await eventHub.PublishAsync<int>("testing", 1, cancellationToken);
        count.Should().Be(200);
        disposables.Dispose();
    }
}
