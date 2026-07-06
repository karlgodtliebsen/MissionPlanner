using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Library.Configuration;
using MissionPlanner.Library.EventHub;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Test.LibraryTests;

/// <summary>
/// TestOfEventHub
/// </summary>
public class TestOfEventHub
{
    private readonly IServiceProvider serviceProvider;
    private readonly CancellationToken cancellationToken = TestContext.Current.CancellationToken;

    /// <summary>
    /// 
    /// </summary>
    public TestOfEventHub()
    {
        var logger = NSubstitute.Substitute.For<ILogger<EventHub>>();
        IServiceCollection services = new ServiceCollection();
        services.AddEventHubServices();
        services.AddSingleton<ILogger<EventHub>>(logger);
        serviceProvider = services.BuildServiceProvider();
    }


    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Verify_EventSubscription_Can_Be_Created_And_Events_Can_Be_Published()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var count = 0;
        var disposable1 = eventHub.Subscribe("testing", () => count++);
        disposable1.Should().NotBeNull();

        var disposable2 = eventHub.Subscribe("testing", () => count++);
        disposable2.Should().NotBeNull();

        var disposable3 = eventHub.Subscribe("testing", () => count++);
        disposable3.Should().NotBeNull();

        eventHub.Publish("testing");
        count.Should().Be(3);
        eventHub.Publish("testing");
        count.Should().Be(6);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_EventSubscription_Can_Be_Created_And_Events_Can_Be_Published_Async()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var count = 0;
        var disposable1 = eventHub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable1.Should().NotBeNull();

        var disposable2 = eventHub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable2.Should().NotBeNull();

        var disposable3 = eventHub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable3.Should().NotBeNull();

        await eventHub.PublishAsync("testing", cancellationToken);
        count.Should().Be(3);
        await eventHub.PublishAsync("testing", cancellationToken);
        count.Should().Be(6);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_EventSubscription_Can_Be_Created_And_Events_Can_Be_Published_Async_2()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var count = 0;
        var disposable1 = eventHub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable1.Should().NotBeNull();

        var disposable2 = eventHub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable2.Should().NotBeNull();

        var disposable3 = eventHub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable3.Should().NotBeNull();

        await eventHub.PublishAsync("testing", cancellationToken);
        count.Should().Be(3);
        //don't touch in this test: disposable.Dispose();
        await eventHub.PublishAsync("testing", cancellationToken);
        count.Should().Be(6);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Verify_EventSubscription_Can_Be_CreatedAndReturnIDisposable()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var count = 0;
        var disposable = eventHub.Subscribe("testing", () => count++);
        disposable.Should().NotBeNull();
        eventHub.Publish("testing");
        count.Should().Be(1);

        disposable.Dispose();
        eventHub.Publish("testing");
        count.Should().Be(1);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Verify_EventSubscription_Using_Generic_Can_Be_Created_And_Return_IDisposable()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var message = "";
        var disposable = eventHub.Subscribe<string>("testing", (m) => message += m);
        disposable.Should().NotBeNull();
        eventHub.Publish<string>("testing", "hello world");
        message.Should().Be("hello world");

        disposable.Dispose();
        eventHub.Publish<string>("testing", "hello world");
        message.Should().Be("hello world");
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Verify_EventSubscription_Using_Payload_Can_Be_Created_And_Return_IDisposable()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var message = "";
        var p = new DomainEvent<string>("testing", "hello universe");

        var disposable = eventHub.Subscribe<DomainEvent<string>>("testing", (m) => message += m.GetData());

        disposable.Should().NotBeNull();
        eventHub.Publish<DomainEvent<string>>("testing", p);
        message.Should().Be("hello universe");

        disposable.Dispose();
        eventHub.Publish<DomainEvent<string>>("testing", p);
        message.Should().Be("hello universe");
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Verify_EventSubscription_Can_Be_CreatedAndSubscribedToMultipleTimesAndReturnIDisposable()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var count = 0;

        int Increase(int m)
        {
            return count += m;
        }

        var disposables = new Disposables();
        for (var i = 0; i < 100; i++)
        {
            var disposable = eventHub.Subscribe<int>("testing", (m) => Increase(m));
            disposable.Should().NotBeNull();
            disposables.Add(disposable);
        }

        eventHub.Publish<int>("testing", 1);
        count.Should().Be(100);
        disposables.Dispose();
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Verify_EventSubscription_Can_Be_CreatedAnd2TimesSubscribedToMultipleTimesAndReturnIDisposable()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
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
            var disposable = eventHub.Subscribe<int>("testing", (m) => Increase1(m));
            disposable.Should().NotBeNull();
            disposables.Add(disposable);
        }

        for (var i = 0; i < 100; i++)
        {
            var disposable = eventHub.Subscribe<int>("testing", (m) => Increase2(m));
            disposable.Should().NotBeNull();
            disposables.Add(disposable);
        }

        eventHub.Publish<int>("testing", 1);
        count.Should().Be(200);

        disposables.Dispose();
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Verify_EventSubscription_MultipleTimes_Can_Remove_The_Correct_Subscription()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
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

        var disposable = eventHub.Subscribe<int>("testing", (m) => Increase1(m));
        disposable.Should().NotBeNull();
        disposables.Add(disposable);


        disposable = eventHub.Subscribe<int>("testing", (m) => Increase2(m));
        disposable.Should().NotBeNull();
        disposables.Add(disposable);

        eventHub.Publish<int>("testing", 1);
        count.Should().Be(3);
        count = 0;
        disposables.First().Dispose();
        eventHub.Publish<int>("testing", 1);
        count.Should().Be(1);
        disposables.Dispose();
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public void Verify_EventSubscription_MultipleTimes_Can_Remove_The_Correct_Last_Subscription()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
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

        var disposable = eventHub.Subscribe<int>("testing", (m) => Increase1(m));
        disposable.Should().NotBeNull();
        disposables.Add(disposable);


        disposable = eventHub.Subscribe<int>("testing", (m) => Increase2(m));
        disposable.Should().NotBeNull();
        disposables.Add(disposable);

        eventHub.Publish<int>("testing", 1);
        count.Should().Be(3);
        count = 0;
        disposables.Last().Dispose();
        eventHub.Publish<int>("testing", 1);
        count.Should().Be(2);
        disposables.Dispose();
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_Created_And_Publish_Is_Working()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var count = 0;
        var disposable = eventHub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable.Should().NotBeNull();
        await eventHub.PublishAsync("testing", cancellationToken);
        count.Should().Be(1);
        //dont touch in this test: disposable.Dispose();
        await eventHub.PublishAsync("testing", cancellationToken);
        count.Should().Be(2);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_Created_And_Event_Published()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var count = 0;
        var disposable = eventHub.SubscribeAsync("testing", (ct) =>
        {
            count++;
            return Task.CompletedTask;
        });
        disposable.Should().NotBeNull();
        await eventHub.PublishAsync("testing", cancellationToken);
        count.Should().Be(1);


        await eventHub.PublishAsync("testing", cancellationToken);
        count.Should().Be(2);
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Using_Generics_Can_Be_CreatedAndReturnIDisposable()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var message = "";
        var disposable = eventHub.SubscribeAsync<string>("testing", (m, ct) =>
        {
            message += m;
            return Task.CompletedTask;
        });
        disposable.Should().NotBeNull();
        await eventHub.PublishAsync<string>("testing", "hello world", cancellationToken);
        message.Should().Be("hello world");

        disposable.Dispose();
        await eventHub.PublishAsync<string>("testing", "hello world", cancellationToken);
        message.Should().Be("hello world");
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Using_Payload_Can_Be_CreatedAndReturnIDisposable()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();

        var message = "";
        var p = new DomainEvent<string>("testing", "hello universe");

        var disposable = eventHub.SubscribeAsync<DomainEvent<string>>("testing", (m, ct) =>
        {
            message += m.GetData();
            return Task.CompletedTask;
        });

        disposable.Should().NotBeNull();
        await eventHub.PublishAsync<DomainEvent<string>>("testing", p, cancellationToken);
        message.Should().Be("hello universe");

        disposable.Dispose();
        await eventHub.PublishAsync<DomainEvent<string>>("testing", p, cancellationToken);
        message.Should().Be("hello universe");
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_Created_And_HandledPublish_Of_EventData_With_Moniker()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var sb = new StringBuilder();
        var p = new EventData { Message = "hello universe" };

        var disposable = eventHub.SubscribeAsync<EventData>("testing", (m, ct) =>
        {
            sb.Append(m.Message);
            return Task.CompletedTask;
        });

        disposable.Should().NotBeNull();
        await eventHub.PublishAsync<EventData>("testing", p, cancellationToken);
        var message = sb.ToString();
        message.Should().Be("hello universe");

        await eventHub.PublishAsync<EventData>("testing", p, cancellationToken);
        message = sb.ToString();
        message.Should().Be("hello universe" + "hello universe");
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_Created_And_HandledPublish_Of_EventData()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var sb = new StringBuilder();
        var p = new EventData { Message = "hello universe" };

        var disposable = eventHub.SubscribeAsync<EventData>((m, ct) =>
        {
            sb.Append(m.Message);
            return Task.CompletedTask;
        });

        disposable.Should().NotBeNull();
        await eventHub.PublishAsync<EventData>(p, cancellationToken);
        var message = sb.ToString();
        message.Should().Be("hello universe");

        await eventHub.PublishAsync<EventData>(p, cancellationToken);
        message = sb.ToString();
        message.Should().Be("hello universe" + "hello universe");
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_Created_And_HandledPublish_Of_IList_Of_EventData()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var sb = new StringBuilder();
        var p = new EventData { Message = "hello universe" };

        var disposable = eventHub.SubscribeAsync<IList<EventData>>((m, ct) =>
        {
            var data = m.Single();
            sb.Append(data.Message);
            return Task.CompletedTask;
        });

        var collection = new List<EventData> { p };
        disposable.Should().NotBeNull();
        await eventHub.PublishAsync<IList<EventData>>(collection, cancellationToken);
        var message = sb.ToString();
        message.Should().Be("hello universe");

        await eventHub.PublishAsync<IList<EventData>>(collection, cancellationToken);
        message = sb.ToString();
        message.Should().Be("hello universe" + "hello universe");
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_Created_And_HandledPublish_Of_IList_Of_EventData_With_Named_Event()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var sb = new StringBuilder();
        var p = new EventData { Message = "hello universe" };

        var disposable = eventHub.SubscribeAsync<IList<EventData>>("Set-Event-Data", (m, ct) =>
        {
            var data = m.Single();
            sb.Append(data.Message);
            return Task.CompletedTask;
        });

        var collection = new List<EventData> { p };
        disposable.Should().NotBeNull();
        await eventHub.PublishAsync<IList<EventData>>("Set-Event-Data", collection, cancellationToken);
        var message = sb.ToString();
        message.Should().Be("hello universe");

        await eventHub.PublishAsync<IList<EventData>>("Set-Event-Data", collection, cancellationToken);
        message = sb.ToString();
        message.Should().Be("hello universe" + "hello universe");
    }


    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_Created_And_HandledPublish_Of_List_Of_EventData_With_Named_Event()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var sb = new StringBuilder();
        var p = new EventData { Message = "hello universe" };

        var disposable = eventHub.SubscribeAsync<List<EventData>>("Set-Event-Data", (m, ct) =>
        {
            var data = m.Single();
            sb.Append(data.Message);
            return Task.CompletedTask;
        });

        var collection = new List<EventData> { p };
        disposable.Should().NotBeNull();
        await eventHub.PublishAsync("Set-Event-Data", collection, cancellationToken);
        var message = sb.ToString();
        message.Should().Be("hello universe");

        await eventHub.PublishAsync("Set-Event-Data", collection, cancellationToken);
        message = sb.ToString();
        message.Should().Be("hello universe" + "hello universe");
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_Created_And_HandledPublish_Of_List_Of_EventData()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var sb = new StringBuilder();
        var p = new EventData { Message = "hello universe" };

        var disposable = eventHub.SubscribeAsync<List<EventData>>((m, ct) =>
        {
            var data = m.Single();
            sb.Append(data.Message);
            return Task.CompletedTask;
        });

        var collection = new List<EventData> { p };
        disposable.Should().NotBeNull();
        await eventHub.PublishAsync(collection, cancellationToken);
        var message = sb.ToString();
        message.Should().Be("hello universe");

        await eventHub.PublishAsync(collection, cancellationToken);
        message = sb.ToString();
        message.Should().Be("hello universe" + "hello universe");
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Data_Publish_And_Subscription_Is_Connected()
    {
        var messageReceived = new TaskCompletionSource<bool>();

        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
        var eventData = new EventData { Message = "hello universe" };

        var disposable = eventHub.SubscribeAsync<EventData>((m, ct) =>
        {
            m.Message.Should().Be("hello universe");
            messageReceived.SetResult(true);
            return Task.CompletedTask;
        });


        await eventHub.PublishAsync(eventData, cancellationToken);

        await Task.WhenAny(messageReceived.Task, Task.Delay(TimeSpan.FromSeconds(1), cancellationToken));
        messageReceived.Task.IsCompleted.Should().BeTrue("EventData was received in the expected time frame.");
        disposable.Dispose();
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_CreatedAndSubscribedToMultipleTimesAndReturnIDisposable()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
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

        await eventHub.PublishAsync<int>("testing", 1, cancellationToken);
        count.Should().Be(100);
        disposables.Dispose();
    }

    /// <summary>
    /// 
    /// </summary>
    [Fact]
    public async Task Verify_Async_EventSubscription_Can_Be_Created_And_Subscribed_To_MultipleTimes_And_All_Subscription_Are_Invoked()
    {
        var eventHub = serviceProvider.GetRequiredService<IEventHub>();
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
