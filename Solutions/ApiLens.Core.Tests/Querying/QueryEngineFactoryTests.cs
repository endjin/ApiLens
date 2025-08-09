using System.Collections.Concurrent;
using ApiLens.Core.Lucene;
using ApiLens.Core.Querying;

namespace ApiLens.Core.Tests.Querying;

[TestClass]
public class QueryEngineFactoryTests
{
    private QueryEngineFactory factory = null!;

    [TestInitialize]
    public void Initialize()
    {
        factory = new QueryEngineFactory();
    }

    [TestMethod]
    public void Create_WithValidIndexManager_ReturnsQueryEngine()
    {
        // Arrange
        ILuceneIndexManager? indexManager = Substitute.For<ILuceneIndexManager>();

        // Act
        IQueryEngine result = factory.Create(indexManager);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeAssignableTo<IQueryEngine>();
        result.ShouldBeOfType<QueryEngine>();
    }

    [TestMethod]
    public void Create_WithDifferentIndexManagers_ReturnsDifferentInstances()
    {
        // Arrange
        ILuceneIndexManager? indexManager1 = Substitute.For<ILuceneIndexManager>();
        ILuceneIndexManager? indexManager2 = Substitute.For<ILuceneIndexManager>();

        // Act
        IQueryEngine engine1 = factory.Create(indexManager1);
        IQueryEngine engine2 = factory.Create(indexManager2);

        // Assert
        engine1.ShouldNotBe(engine2);
        ReferenceEquals(engine1, engine2).ShouldBeFalse();
    }

    [TestMethod]
    public void Create_WithSameIndexManager_ReturnsDifferentInstances()
    {
        // Arrange
        ILuceneIndexManager? indexManager = Substitute.For<ILuceneIndexManager>();

        // Act
        IQueryEngine engine1 = factory.Create(indexManager);
        IQueryEngine engine2 = factory.Create(indexManager);

        // Assert
        engine1.ShouldNotBe(engine2);
        ReferenceEquals(engine1, engine2).ShouldBeFalse();
    }

    [TestMethod]
    public void Create_WithNullIndexManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => factory.Create(null!))
            .ParamName.ShouldBe("indexManager");
    }

    [TestMethod]
    public void Factory_ImplementsIQueryEngineFactory()
    {
        // Assert
        factory.ShouldBeAssignableTo<IQueryEngineFactory>();
    }

    [TestMethod]
    public void Factory_CanBeUsedWithDependencyInjection()
    {
        // Arrange
        IQueryEngineFactory diFactory = new QueryEngineFactory();
        ILuceneIndexManager? indexManager = Substitute.For<ILuceneIndexManager>();

        // Act
        IQueryEngine engine = diFactory.Create(indexManager);

        // Assert
        engine.ShouldNotBeNull();
        engine.ShouldBeAssignableTo<IQueryEngine>();
    }

    [TestMethod]
    public void Create_ReturnsFullyFunctionalQueryEngine()
    {
        // Arrange
        ILuceneIndexManager? indexManager = Substitute.For<ILuceneIndexManager>();

        // Act
        IQueryEngine engine = factory.Create(indexManager);

        // Assert
        engine.ShouldNotBeNull();
        // Verify it's the actual implementation, not a mock
        engine.GetType().Assembly.ShouldBe(typeof(QueryEngine).Assembly);
    }

    [TestMethod]
    public void Create_CalledMultipleTimes_AlwaysReturnsNewInstance()
    {
        // Arrange
        ILuceneIndexManager? indexManager = Substitute.For<ILuceneIndexManager>();
        List<IQueryEngine> engines = [];

        // Act
        for (int i = 0; i < 5; i++)
        {
            engines.Add(factory.Create(indexManager));
        }

        // Assert
        engines.Count.ShouldBe(5);
        engines.Distinct().Count().ShouldBe(5); // All instances should be unique
    }

    [TestMethod]
    public void Factory_IsThreadSafe()
    {
        // Arrange
        ILuceneIndexManager? indexManager = Substitute.For<ILuceneIndexManager>();
        ConcurrentBag<IQueryEngine> engines = [];
        List<Task> tasks = [];

        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                IQueryEngine engine = factory.Create(indexManager);
                engines.Add(engine);
            }));
        }

        Task.WaitAll([.. tasks]);

        // Assert
        engines.Count.ShouldBe(10);
        engines.All(e => e != null).ShouldBeTrue();
        engines.All(e => e is QueryEngine).ShouldBeTrue();
    }
}