using Microsoft.Extensions.DependencyInjection;

namespace ApiLens.Cli.Tests;

[TestClass]
public sealed class TypeResolverTests
{
    [TestMethod]
    public void Resolve_WithValidType_ShouldReturnService()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<ITestService, TestService>();
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        TypeResolver resolver = new(serviceProvider);

        // Act
        object? result = resolver.Resolve(typeof(ITestService));

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<TestService>();
    }

    [TestMethod]
    public void Resolve_WithUnregisteredType_ShouldReturnNull()
    {
        // Arrange
        ServiceCollection services = new();
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        TypeResolver resolver = new(serviceProvider);

        // Act
        object? result = resolver.Resolve(typeof(ITestService));

        // Assert
        result.ShouldBeNull();
    }

    [TestMethod]
    public void Resolve_WithNullType_ShouldReturnNull()
    {
        // Arrange
        IServiceProvider? serviceProvider = Substitute.For<IServiceProvider>();
        TypeResolver resolver = new(serviceProvider);

        // Act
        object? result = resolver.Resolve(null);

        // Assert
        result.ShouldBeNull();
        serviceProvider.DidNotReceive().GetService(Arg.Any<Type>());
    }

    [TestMethod]
    public void Resolve_ShouldCallServiceProviderGetService()
    {
        // Arrange
        TestService expectedService = new();
        IServiceProvider? serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ITestService)).Returns(expectedService);
        TypeResolver resolver = new(serviceProvider);

        // Act
        object? result = resolver.Resolve(typeof(ITestService));

        // Assert
        result.ShouldBeSameAs(expectedService);
        serviceProvider.Received(1).GetService(typeof(ITestService));
    }

    [TestMethod]
    public void Constructor_ShouldAcceptServiceProvider()
    {
        // Arrange
        IServiceProvider? serviceProvider = Substitute.For<IServiceProvider>();

        // Act
        TypeResolver resolver = new(serviceProvider);

        // Assert
        resolver.ShouldNotBeNull();
    }

    [TestMethod]
    public void Resolve_MultipleTypes_ShouldResolveCorrectly()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<ITestService, TestService>();
        services.AddSingleton<IOtherService, OtherService>();
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        TypeResolver resolver = new(serviceProvider);

        // Act & Assert
        object? testService = resolver.Resolve(typeof(ITestService));
        testService.ShouldNotBeNull();
        testService.ShouldBeOfType<TestService>();

        object? otherService = resolver.Resolve(typeof(IOtherService));
        otherService.ShouldNotBeNull();
        otherService.ShouldBeOfType<OtherService>();
    }

    // Test interfaces and implementations
    private interface ITestService;

    private class TestService : ITestService;

    private interface IOtherService;

    private class OtherService : IOtherService;
}