using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace ApiLens.Cli.Tests;

[TestClass]
public sealed class TypeRegistrarTests
{
    private ServiceCollection services = null!;
    private TypeRegistrar registrar = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        services = [];
        registrar = new TypeRegistrar(services);
    }

    [TestMethod]
    public void Register_ShouldAddServiceToCollection()
    {
        // Act
        registrar.Register(typeof(ITestService), typeof(TestService));

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        ITestService? resolvedService = serviceProvider.GetService<ITestService>();
        resolvedService.ShouldNotBeNull();
        resolvedService.ShouldBeOfType<TestService>();
    }

    [TestMethod]
    public void RegisterInstance_ShouldAddInstanceToCollection()
    {
        // Arrange
        TestService instance = new();

        // Act
        registrar.RegisterInstance(typeof(ITestService), instance);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        ITestService? resolvedService = serviceProvider.GetService<ITestService>();
        resolvedService.ShouldNotBeNull();
        resolvedService.ShouldBeSameAs(instance);
    }

    [TestMethod]
    public void RegisterLazy_ShouldAddLazyFactoryToCollection()
    {
        // Arrange
        TestService expectedInstance = new();
        bool factoryCalled = false;

        // Act
        registrar.RegisterLazy(typeof(ITestService), Factory);

        // Assert - Factory not called until service is resolved
        factoryCalled.ShouldBeFalse();

        ServiceProvider serviceProvider = services.BuildServiceProvider();
        ITestService? resolvedService = serviceProvider.GetService<ITestService>();

        factoryCalled.ShouldBeTrue();
        resolvedService.ShouldNotBeNull();
        resolvedService.ShouldBeSameAs(expectedInstance);
        return;

        object Factory()
        {
            factoryCalled = true;
            return expectedInstance;
        }
    }

    [TestMethod]
    public void Build_ShouldReturnTypeResolver()
    {
        // Arrange
        registrar.Register(typeof(ITestService), typeof(TestService));

        // Act
        ITypeResolver resolver = registrar.Build();

        // Assert
        resolver.ShouldNotBeNull();
        resolver.ShouldBeOfType<TypeResolver>();
    }

    [TestMethod]
    public void Build_ShouldReturnResolverThatCanResolveRegisteredServices()
    {
        // Arrange
        registrar.Register(typeof(ITestService), typeof(TestService));

        // Act
        ITypeResolver resolver = registrar.Build();
        object? resolvedService = resolver.Resolve(typeof(ITestService));

        // Assert
        resolvedService.ShouldNotBeNull();
        resolvedService.ShouldBeOfType<TestService>();
    }

    [TestMethod]
    public void Register_WithMultipleServices_ShouldRegisterAll()
    {
        // Act
        registrar.Register(typeof(ITestService), typeof(TestService));
        registrar.Register(typeof(IOtherService), typeof(OtherService));

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        ITestService? testService = serviceProvider.GetService<ITestService>();
        testService.ShouldNotBeNull();
        testService.ShouldBeOfType<TestService>();

        IOtherService? otherService = serviceProvider.GetService<IOtherService>();
        otherService.ShouldNotBeNull();
        otherService.ShouldBeOfType<OtherService>();
    }

    // Test interfaces and implementations
    private interface ITestService;

    private class TestService : ITestService;

    private interface IOtherService;

    private class OtherService : IOtherService;
}