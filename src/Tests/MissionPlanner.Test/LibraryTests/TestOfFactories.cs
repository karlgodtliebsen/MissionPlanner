using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.Factory.Domain;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.Test.LibraryTests;

/// <summary>
/// Test class for verifying the behavior of factory-created services.
/// </summary>
public class TestOfFactories
{
    /// <summary>
    /// Verify that ExtendedService can be created using default DI container with transient lifetime, and that each instance gets new dependencies
    /// </summary>
    [Fact]
    public void Verify_That_ExtendedService_Can_Be_Created_Using_Default_Using_Transient()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(new ApplicationData { Size = 42.0 }));

        services.AddTransient<IExtendedService, ExtendedService>();
        services.AddTransient<IService1, Service1>();
        services.AddTransient<IService2, Service2>();

        var serviceProvider = services.BuildServiceProvider();

        var extendedService1 = serviceProvider.GetRequiredService<IExtendedService>();
        extendedService1.Should().NotBeNull();

        var extendedService2 = serviceProvider.GetRequiredService<IExtendedService>();
        extendedService2.Should().NotBeNull();

        extendedService1.TrackingId.Should().NotBe(extendedService2.TrackingId);
        extendedService1.Service1.TrackingId.Should().NotBe(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().NotBe(extendedService2.Service2.TrackingId);
    }

    /// <summary>
    /// Verify that ExtendedService can be created using default DI container with singleton lifetime, and that each instance gets the same dependencies 
    /// </summary>
    [Fact]
    public void Verify_That_ExtendedService_Can_Be_Created_Using_Default_Using_Singleton()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(new ApplicationData { Size = 42.0 }));

        services.AddSingleton<IExtendedService, ExtendedService>();
        services.AddSingleton<IService1, Service1>();
        services.AddSingleton<IService2, Service2>();

        var serviceProvider = services.BuildServiceProvider();

        var extendedService1 = serviceProvider.GetRequiredService<IExtendedService>();
        extendedService1.Should().NotBeNull();

        var extendedService2 = serviceProvider.GetRequiredService<IExtendedService>();
        extendedService2.Should().NotBeNull();

        extendedService1.TrackingId.Should().Be(extendedService2.TrackingId);
        extendedService1.Service1.TrackingId.Should().Be(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().Be(extendedService2.Service2.TrackingId);
    }

    /// <summary>
    /// Verify that ExtendedService can be created using default DI container with transient and singleton lifetimes, and that each instance gets the appropriate dependencies.
    /// </summary>
    [Fact]
    public void Verify_That_ExtendedService_Can_Be_Created_Using_Default_Using_Transient_And_Singleton()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(new ApplicationData { Size = 42.0 }));

        services.AddTransient<IExtendedService, ExtendedService>();
        services.AddSingleton<IService1, Service1>();
        services.AddSingleton<IService2, Service2>();

        var serviceProvider = services.BuildServiceProvider();

        var extendedService1 = serviceProvider.GetRequiredService<IExtendedService>();
        extendedService1.Should().NotBeNull();

        var extendedService2 = serviceProvider.GetRequiredService<IExtendedService>();
        extendedService2.Should().NotBeNull();

        extendedService1.TrackingId.Should().NotBe(extendedService2.TrackingId);
        extendedService1.Service1.TrackingId.Should().Be(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().Be(extendedService2.Service2.TrackingId);
    }

    /// <summary>
    /// Verify that ExtendedService can be created using ServiceFactory with transient lifetime, and that each instance gets new dependencies.
    /// </summary>
    [Fact]
    public void Verify_That_ExtendedService_Can_Be_Created_Using_ServiceFactory_Using_Transient()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddTransient<IServiceFactory, ServiceFactory>();
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(new ApplicationData { Size = 42.0 }));

        services.AddTransient<IExtendedService, ExtendedService>();
        services.AddTransient<IService1, Service1>();
        services.AddTransient<IService2, Service2>();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IServiceFactory>();
        var extendedService1 = factory.Create<IExtendedService>();
        extendedService1.Should().NotBeNull();

        var extendedService2 = factory.Create<IExtendedService>();
        extendedService2.Should().NotBeNull();

        extendedService1.TrackingId.Should().NotBe(extendedService2.TrackingId);
        extendedService1.Service1.TrackingId.Should().NotBe(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().NotBe(extendedService2.Service2.TrackingId);
    }

    /// <summary>
    /// Verify that ExtendedService can be created using ServiceFactory with singleton lifetime, and that each instance gets the same dependencies.
    /// </summary>
    [Fact]
    public void Verify_That_ExtendedService_Can_Be_Created_Using_ServiceFactory_Using_Singleton()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddTransient<IServiceFactory, ServiceFactory>();
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(new ApplicationData { Size = 42.0 }));

        services.AddSingleton<IExtendedService, ExtendedService>();
        services.AddSingleton<IService1, Service1>();
        services.AddSingleton<IService2, Service2>();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IServiceFactory>();
        var extendedService1 = factory.Create<IExtendedService>();
        extendedService1.Should().NotBeNull();

        var extendedService2 = factory.Create<IExtendedService>();
        extendedService2.Should().NotBeNull();

        extendedService1.TrackingId.Should().Be(extendedService2.TrackingId);
        extendedService1.Service1.TrackingId.Should().Be(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().Be(extendedService2.Service2.TrackingId);
    }

    /// <summary>
    /// Verify that ExtendedService can be created using ServiceFactory with transient and singleton lifetimes, and that each instance gets the appropriate dependencies.
    /// </summary>
    [Fact]
    public void Verify_That_ExtendedService_Can_Be_Created_Using_ServiceFactory_Using_Transient_And_Singleton()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddTransient<IServiceFactory, ServiceFactory>();
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(new ApplicationData { Size = 42.0 }));

        services.AddTransient<IExtendedService, ExtendedService>();
        services.AddSingleton<IService1, Service1>();
        services.AddSingleton<IService2, Service2>();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IServiceFactory>();
        var extendedService1 = factory.Create<IExtendedService>();
        extendedService1.Should().NotBeNull();

        var extendedService2 = factory.Create<IExtendedService>();
        extendedService2.Should().NotBeNull();

        extendedService1.TrackingId.Should().NotBe(extendedService2.TrackingId);
        extendedService1.Service1.TrackingId.Should().Be(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().Be(extendedService2.Service2.TrackingId);
    }

    /// <summary>
    /// Verify that ExtendedService can be created using DomainFactory with transient lifetime, and that each instance gets new dependencies.
    /// </summary>
    [Fact]
    public void Verify_That_ExtendedService_Can_Be_Created_Using_DomainFactory_Using_Transient()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IDomainFactory, DomainFactory>();
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(new ApplicationData { Size = 42.0 }));

        services.AddTransient<IExtendedService, ExtendedService>();
        services.AddSingleton<IService1, Service1>();
        services.AddSingleton<IService2, Service2>();


        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDomainFactory>();
        factory.Add<IExtendedService, ExtendedService>();

        var extendedService1 = factory.Create<IExtendedService>();
        extendedService1.Should().NotBeNull();

        var extendedService2 = factory.Create<IExtendedService>();
        extendedService2.Should().NotBeNull();
        extendedService1.TrackingId.Should().NotBe(extendedService2.TrackingId);
    }

    /// <summary>
    /// Verify that ExtendedService can be created using DomainFactory with singleton lifetime, and that each instance gets the same dependencies.
    /// </summary>
    [Fact]
    public void Verify_That_ExtendedService_Can_Be_Created_Using_DomainFactory_Using_Singleton()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IDomainFactory, DomainFactory>();
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(new ApplicationData { Size = 42.0 }));
        //Note this singleton
        services.AddSingleton<IExtendedService, ExtendedService>();
        services.AddSingleton<IService1, Service1>();
        services.AddSingleton<IService2, Service2>();

        //this magic removes the boundaries of IServiceCollection registration

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDomainFactory>();
        factory.Add<IExtendedService, ExtendedService>();

        var extendedService1 = factory.Create<IExtendedService>();
        extendedService1.Should().NotBeNull();

        var extendedService2 = factory.Create<IExtendedService>();
        extendedService2.Should().NotBeNull();
        //and this factory creates new instances regardless of IServiceCollection registration
        extendedService1.TrackingId.Should().NotBe(extendedService2.TrackingId);
        extendedService1.Service1.TrackingId.Should().Be(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().Be(extendedService2.Service2.TrackingId);
    }


    /// <summary>
    /// Verify that ExtendedService and its dependencies can be created using DomainFactory with singleton lifetime, and that each instance gets the same dependencies.
    /// </summary>
    [Fact]
    public void Verify_That_ExtendedService_And_Dependencies_Can_Be_Created_Using_DomainFactory_Using_Singleton()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IDomainFactory, DomainFactory>();
        var options = new ApplicationData { Size = 42.0 };
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(options));
        //Note this singleton
        services.AddSingleton<IExtendedService, ExtendedService>();
        services.AddSingleton<IService1, Service1>();
        services.AddSingleton<IService2, Service2>();


        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDomainFactory>();
        //this magic removes the boundaries of IServiceCollection registration
        factory.Add<IExtendedService, ExtendedService>();
        factory.Add<IService1, Service1>();


        var extendedService2 = factory.Create<IExtendedService>();
        extendedService2.Should().NotBeNull();


        var extendedService1 = factory.Create<IExtendedService, IService1>();
        extendedService1.Should().NotBeNull();

        //and this factory creates new instances regardless of IServiceCollection registration
        extendedService1.TrackingId.Should().NotBe(extendedService2.TrackingId);
        extendedService1.Service1.TrackingId.Should().NotBe(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().Be(extendedService2.Service2.TrackingId);
        extendedService1.Options.Size.Should().Be(options.Size);

        factory.Add<IService2, Service2>();
        extendedService1 = factory.Create<IExtendedService, IService1, IService2>();
        extendedService1.Should().NotBeNull();
        extendedService1.Service1.TrackingId.Should().NotBe(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().NotBe(extendedService2.Service2.TrackingId);
        extendedService1.Options.Size.Should().Be(options.Size);

        factory.Add<IDateTimeProvider, DateTimeProvider>();
        extendedService1 = factory.Create<IExtendedService, IService1, IService2, IDateTimeProvider>();
        extendedService1.Should().NotBeNull();
        extendedService1.Service1.TrackingId.Should().NotBe(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().NotBe(extendedService2.Service2.TrackingId);
        extendedService1.Options.Size.Should().Be(options.Size);

        extendedService1 = factory.Create<IExtendedService, IService1, IService2, IDateTimeProvider>();
        extendedService1.Should().NotBeNull();
        extendedService1.Service1.TrackingId.Should().NotBe(extendedService2.Service1.TrackingId);
        extendedService1.Service2.TrackingId.Should().NotBe(extendedService2.Service2.TrackingId);
        extendedService1.Options.Size.Should().Be(options.Size);
    }

    /// <summary>
    /// Verify that SuperExtendedService and its dependencies can be created using DomainFactory with singleton lifetime, and that each instance gets the same dependencies.
    /// </summary>
    [Fact]
    public void Verify_That_SuperExtendedService_And_Dependencies_Can_Be_Created_Using_DomainFactory_Using_Singleton()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddTransient<IDomainFactory, DomainFactory>();
        var options = new ApplicationData { Size = 42.0 };
        services.AddSingleton<IOptions<ApplicationData>>(Options.Create(options));
        //Note this singleton
        services.AddSingleton<IExtendedService, ExtendedService>();
        services.AddSingleton<ISuperExtendedService, SuperExtendedService>();
        services.AddSingleton<IService1, Service1>();
        services.AddSingleton<IService2, Service2>();
        services.AddSingleton<IService3, Service3>();
        services.AddSingleton<IService4, Service4>();
        services.AddSingleton<IService5, Service5>();
        services.AddSingleton<IService6, Service6>();
        services.AddSingleton<IService7, Service7>();


        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDomainFactory>();

        //the magic below removes the boundaries of IServiceCollection registration.
        //Super for scenarios where you want to deviate from singleton pattern, ie co-hosted services that should not interfere with each other

        factory.Add<ISuperExtendedService, SuperExtendedService>();
        factory.Add<IService1, Service1>();
        factory.Add<IService2, Service2>();
        factory.Add<IService3, Service3>();
        factory.Add<IService4, Service4>();
        factory.Add<IService5, Service5>();
        factory.Add<IService6, Service6>();
        factory.Add<IService7, Service7>();


        var extendedService = serviceProvider.GetRequiredService<ISuperExtendedService>();

        var superExtendedService = factory.Create<ISuperExtendedService, IService1>();
        superExtendedService.Should().NotBeNull();
        superExtendedService.Service1.TrackingId.Should().NotBe(extendedService.Service1.TrackingId);
        superExtendedService.Service2.TrackingId.Should().Be(extendedService.Service2.TrackingId);
        superExtendedService.Options.Size.Should().Be(options.Size);

        superExtendedService = factory.Create<ISuperExtendedService, IService1, IService2>();
        superExtendedService.Should().NotBeNull();
        superExtendedService.Service1.TrackingId.Should().NotBe(extendedService.Service1.TrackingId);
        superExtendedService.Service2.TrackingId.Should().NotBe(extendedService.Service2.TrackingId);
        superExtendedService.Service3.TrackingId.Should().Be(extendedService.Service3.TrackingId);
        superExtendedService.Options.Size.Should().Be(options.Size);


        superExtendedService = factory.Create<ISuperExtendedService, IService1, IService2, IService3>();
        superExtendedService.Should().NotBeNull();
        superExtendedService.Service1.TrackingId.Should().NotBe(extendedService.Service1.TrackingId);
        superExtendedService.Service2.TrackingId.Should().NotBe(extendedService.Service2.TrackingId);
        superExtendedService.Service3.TrackingId.Should().NotBe(extendedService.Service3.TrackingId);
        superExtendedService.Service4.TrackingId.Should().Be(extendedService.Service4.TrackingId);
        superExtendedService.Options.Size.Should().Be(options.Size);


        superExtendedService = factory.Create<ISuperExtendedService, IService1, IService2, IService3, IService4>();
        superExtendedService.Should().NotBeNull();
        superExtendedService.Service1.TrackingId.Should().NotBe(extendedService.Service1.TrackingId);
        superExtendedService.Service2.TrackingId.Should().NotBe(extendedService.Service2.TrackingId);
        superExtendedService.Service3.TrackingId.Should().NotBe(extendedService.Service3.TrackingId);
        superExtendedService.Service4.TrackingId.Should().NotBe(extendedService.Service4.TrackingId);
        superExtendedService.Service5.TrackingId.Should().Be(extendedService.Service5.TrackingId);
        superExtendedService.Options.Size.Should().Be(options.Size);


        superExtendedService = factory.Create<ISuperExtendedService, IService1, IService2, IService3, IService4, IService5>();
        superExtendedService.Should().NotBeNull();
        superExtendedService.Service1.TrackingId.Should().NotBe(extendedService.Service1.TrackingId);
        superExtendedService.Service2.TrackingId.Should().NotBe(extendedService.Service2.TrackingId);
        superExtendedService.Service3.TrackingId.Should().NotBe(extendedService.Service3.TrackingId);
        superExtendedService.Service4.TrackingId.Should().NotBe(extendedService.Service4.TrackingId);
        superExtendedService.Service5.TrackingId.Should().NotBe(extendedService.Service5.TrackingId);
        superExtendedService.Service6.TrackingId.Should().Be(extendedService.Service6.TrackingId);
        superExtendedService.Options.Size.Should().Be(options.Size);

        superExtendedService = factory.Create<ISuperExtendedService, IService1, IService2, IService3, IService4, IService5, IService6>();
        superExtendedService.Should().NotBeNull();
        superExtendedService.Service1.TrackingId.Should().NotBe(extendedService.Service1.TrackingId);
        superExtendedService.Service2.TrackingId.Should().NotBe(extendedService.Service2.TrackingId);
        superExtendedService.Service3.TrackingId.Should().NotBe(extendedService.Service3.TrackingId);
        superExtendedService.Service4.TrackingId.Should().NotBe(extendedService.Service4.TrackingId);
        superExtendedService.Service5.TrackingId.Should().NotBe(extendedService.Service5.TrackingId);
        superExtendedService.Service6.TrackingId.Should().NotBe(extendedService.Service6.TrackingId);
        superExtendedService.Service7.TrackingId.Should().Be(extendedService.Service7.TrackingId);
        superExtendedService.Options.Size.Should().Be(options.Size);

        superExtendedService = factory.Create<ISuperExtendedService, IService1, IService2, IService3, IService4, IService5, IService6, IService7>();
        superExtendedService.Should().NotBeNull();
        superExtendedService.Service1.TrackingId.Should().NotBe(extendedService.Service1.TrackingId);
        superExtendedService.Service2.TrackingId.Should().NotBe(extendedService.Service2.TrackingId);
        superExtendedService.Service3.TrackingId.Should().NotBe(extendedService.Service3.TrackingId);
        superExtendedService.Service4.TrackingId.Should().NotBe(extendedService.Service4.TrackingId);
        superExtendedService.Service5.TrackingId.Should().NotBe(extendedService.Service5.TrackingId);
        superExtendedService.Service6.TrackingId.Should().NotBe(extendedService.Service6.TrackingId);
        superExtendedService.Service7.TrackingId.Should().NotBe(extendedService.Service7.TrackingId);
        superExtendedService.Options.Size.Should().Be(options.Size);
    }

    /// <summary>
    /// 
    /// </summary>
    public class ApplicationData
    {
        public double Size { get; set; }

        public override string ToString()
        {
            return Size.ToString("F1");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public interface IExtendedService
    {
        Guid TrackingId { get; }
        IService1 Service1 { get; }
        IService2 Service2 { get; }
        ApplicationData Options { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class ExtendedService : IExtendedService
    {
        /// <summary>
        /// 
        /// </summary>
        public ApplicationData Options { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="service1"></param>
        /// <param name="service2"></param>
        /// <param name="dateTimeProvider"></param>
        /// <param name="options"></param>
        public ExtendedService(IService1 service1, IService2 service2, IDateTimeProvider dateTimeProvider, IOptions<ApplicationData> options)

        {
            Service1 = service1;
            Service2 = service2;
            Options = options.Value;
            TrackingId = Guid.NewGuid();
        }

        public IService1 Service1 { get; }
        public IService2 Service2 { get; }

        public Guid TrackingId { get; set; }

        public override string ToString()
        {
            return TrackingId.ToString("D");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public interface IService1
    {
        Guid TrackingId { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class Service1 : IService1
    {
        /// <summary>
        /// 
        /// </summary>
        public Service1()
        {
            TrackingId = Guid.NewGuid();
        }

        public Guid TrackingId { get; set; }

        public override string ToString()
        {
            return TrackingId.ToString("D");
        }
    }

    public interface IService2
    {
        Guid TrackingId { get; }
    }

    public class Service2 : IService2
    {
        public Service2()
        {
            TrackingId = Guid.NewGuid();
        }

        public Guid TrackingId { get; set; }

        public override string ToString()
        {
            return TrackingId.ToString("D");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public interface ISuperExtendedService
    {
        Guid TrackingId { get; }
        IService1 Service1 { get; }
        IService2 Service2 { get; }
        IService3 Service3 { get; }
        IService4 Service4 { get; }
        IService5 Service5 { get; }
        IService6 Service6 { get; }
        IService7 Service7 { get; }
        ApplicationData Options { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="service1"></param>
    /// <param name="service2"></param>
    /// <param name="service3"></param>
    /// <param name="service4"></param>
    /// <param name="service5"></param>
    /// <param name="service6"></param>
    /// <param name="service7"></param>
    /// <param name="options"></param>
    public class SuperExtendedService(
        IService1 service1,
        IService2 service2,
        IService3 service3,
        IService4 service4,
        IService5 service5,
        IService6 service6,
        IService7 service7,
        IOptions<ApplicationData> options) : ISuperExtendedService
    {
        public ApplicationData Options => options.Value;

        public IService1 Service1 => service1;
        public IService2 Service2 => service2;
        public IService3 Service3 => service3;
        public IService4 Service4 => service4;
        public IService5 Service5 => service5;
        public IService6 Service6 => service6;
        public IService7 Service7 => service7;

        public Guid TrackingId { get; } = Guid.NewGuid();

        public override string ToString()
        {
            return TrackingId.ToString("D");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public interface IService3
    {
        Guid TrackingId { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class Service3 : IService3
    {
        public Service3()
        {
            TrackingId = Guid.NewGuid();
        }

        public Guid TrackingId { get; set; }

        public override string ToString()
        {
            return TrackingId.ToString("D");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public interface IService4
    {
        Guid TrackingId { get; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class Service4 : IService4
    {
        public Service4()
        {
            TrackingId = Guid.NewGuid();
        }

        public Guid TrackingId { get; set; }

        public override string ToString()
        {
            return TrackingId.ToString("D");
        }
    }

    public interface IService5
    {
        Guid TrackingId { get; }
    }

    public class Service5 : IService5
    {
        public Service5()
        {
            TrackingId = Guid.NewGuid();
        }

        public Guid TrackingId { get; set; }

        public override string ToString()
        {
            return TrackingId.ToString("D");
        }
    }

    public interface IService6
    {
        Guid TrackingId { get; }
    }

    public class Service6 : IService6
    {
        public Service6()
        {
            TrackingId = Guid.NewGuid();
        }

        public Guid TrackingId { get; set; }

        public override string ToString()
        {
            return TrackingId.ToString("D");
        }
    }


    public interface IService7
    {
        Guid TrackingId { get; }
    }

    public class Service7 : IService7
    {
        public Service7()
        {
            TrackingId = Guid.NewGuid();
        }

        public Guid TrackingId { get; set; }

        public override string ToString()
        {
            return TrackingId.ToString("D");
        }
    }
}
