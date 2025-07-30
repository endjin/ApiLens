using ApiLens.Core.Lucene;

namespace ApiLens.Core.Tests.Lucene;

[TestClass]
public class LuceneIndexManagerFactoryTests
{
    private LuceneIndexManagerFactory factory = null!;

    [TestInitialize]
    public void Initialize()
    {
        factory = new LuceneIndexManagerFactory();
    }

    [TestMethod]
    public void Create_WithValidPath_ReturnsLuceneIndexManager()
    {
        // Arrange
        string indexPath = "test-index";

        // Act
        ILuceneIndexManager result = factory.Create(indexPath);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeAssignableTo<ILuceneIndexManager>();
        result.ShouldBeOfType<LuceneIndexManager>();
    }

    [TestMethod]
    public void Create_WithDifferentPaths_ReturnsDifferentInstances()
    {
        // Arrange
        string path1 = "index1";
        string path2 = "index2";

        // Act
        ILuceneIndexManager manager1 = factory.Create(path1);
        ILuceneIndexManager manager2 = factory.Create(path2);

        // Assert
        manager1.ShouldNotBe(manager2);
        ReferenceEquals(manager1, manager2).ShouldBeFalse();
    }

    [TestMethod]
    public void Create_WithSamePath_ReturnsDifferentInstances()
    {
        // Arrange
        string path1 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string path2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Act
            ILuceneIndexManager manager1 = factory.Create(path1);
            ILuceneIndexManager manager2 = factory.Create(path2);

            // Assert
            manager1.ShouldNotBe(manager2);
            ReferenceEquals(manager1, manager2).ShouldBeFalse();

            // Cleanup
            manager1.Dispose();
            manager2.Dispose();
        }
        finally
        {
            if (Directory.Exists(path1)) Directory.Delete(path1, true);
            if (Directory.Exists(path2)) Directory.Delete(path2, true);
        }
    }

    [TestMethod]
    public void Create_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        string indexPath = string.Empty;

        // Act & Assert
        Should.Throw<ArgumentException>(() => factory.Create(indexPath));
    }

    [TestMethod]
    public void Create_WithRelativePath_ReturnsLuceneIndexManager()
    {
        // Arrange
        string indexPath = Path.Combine(Path.GetTempPath(), "relative", Guid.NewGuid().ToString());

        try
        {
            // Act
            ILuceneIndexManager result = factory.Create(indexPath);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeAssignableTo<ILuceneIndexManager>();

            // Cleanup
            result.Dispose();
        }
        finally
        {
            if (Directory.Exists(indexPath)) Directory.Delete(indexPath, true);
        }
    }

    [TestMethod]
    public void Create_WithTempPath_ReturnsLuceneIndexManager()
    {
        // Arrange
        string indexPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Act
            ILuceneIndexManager result = factory.Create(indexPath);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeAssignableTo<ILuceneIndexManager>();

            // Cleanup
            result.Dispose();
        }
        finally
        {
            if (Directory.Exists(indexPath)) Directory.Delete(indexPath, true);
        }
    }

    [TestMethod]
    public void Create_WithSpecialCharactersInPath_ReturnsLuceneIndexManager()
    {
        // Arrange
        string indexPath = Path.Combine(Path.GetTempPath(), "path with spaces", "and-dashes", Guid.NewGuid().ToString());

        try
        {
            // Act
            ILuceneIndexManager result = factory.Create(indexPath);

            // Assert
            result.ShouldNotBeNull();
            result.ShouldBeAssignableTo<ILuceneIndexManager>();

            // Cleanup
            result.Dispose();
        }
        finally
        {
            if (Directory.Exists(indexPath)) Directory.Delete(indexPath, true);
        }
    }

    [TestMethod]
    public void Create_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => factory.Create(null!))
            .ParamName.ShouldBe("path");
    }

    [TestMethod]
    public void Factory_ImplementsILuceneIndexManagerFactory()
    {
        // Assert
        factory.ShouldBeAssignableTo<ILuceneIndexManagerFactory>();
    }

    [TestMethod]
    public void Factory_CanBeUsedWithDependencyInjection()
    {
        // Arrange
        ILuceneIndexManagerFactory diFactory = new LuceneIndexManagerFactory();

        // Act
        ILuceneIndexManager manager = diFactory.Create("di-test-index");

        // Assert
        manager.ShouldNotBeNull();
        manager.ShouldBeAssignableTo<ILuceneIndexManager>();
    }
}