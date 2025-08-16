using ApiLens.Cli.Services;

namespace ApiLens.Cli.Tests.Services;

[TestClass]
public class FrameworkVersionComparerTests
{
    private FrameworkVersionComparer comparer = null!;

    [TestInitialize]
    public void Setup()
    {
        comparer = new FrameworkVersionComparer();
    }

    [TestMethod]
    public void Compare_Net9BeforeNet8()
    {
        // Act
        int result = comparer.Compare("net8.0", "net9.0");

        // Assert
        result.ShouldBeGreaterThan(0); // net9.0 should come before net8.0
    }

    [TestMethod]
    public void Compare_Net8BeforeNet7()
    {
        // Act
        int result = comparer.Compare("net7.0", "net8.0");

        // Assert
        result.ShouldBeGreaterThan(0); // net8.0 should come before net7.0
    }

    [TestMethod]
    public void Compare_NetStandardLowerPriority()
    {
        // Act
        int result1 = comparer.Compare("netstandard2.0", "net6.0");
        int result2 = comparer.Compare("netstandard2.1", "net6.0");

        // Assert
        result1.ShouldBeGreaterThan(0); // net6.0 should come before netstandard2.0
        result2.ShouldBeGreaterThan(0); // net6.0 should come before netstandard2.1
    }

    [TestMethod]
    public void Compare_NetCoreAppHandledCorrectly()
    {
        // Act
        int result = comparer.Compare("netcoreapp3.1", "net5.0");

        // Assert
        result.ShouldBeGreaterThan(0); // net5.0 should come before netcoreapp3.1
    }

    [TestMethod]
    public void Compare_SameFrameworkReturnsZero()
    {
        // Act
        int result1 = comparer.Compare("net8.0", "net8.0");
        int result2 = comparer.Compare("netstandard2.1", "netstandard2.1");

        // Assert
        result1.ShouldBe(0);
        result2.ShouldBe(0);
    }

    [TestMethod]
    public void Compare_HandlesNullValues()
    {
        // Act
        int result1 = comparer.Compare(null, "net8.0");
        int result2 = comparer.Compare("net8.0", null);
        int result3 = comparer.Compare(null, null);

        // Assert
        result1.ShouldBeGreaterThan(0); // net8.0 should come before null
        result2.ShouldBeLessThan(0); // null should come after net8.0
        result3.ShouldBe(0); // both null should be equal
    }

    [TestMethod]
    public void Compare_HandlesUnknownFrameworks()
    {
        // Act
        int result1 = comparer.Compare("unknownframework", "net8.0");
        int result2 = comparer.Compare("customframework1.0", "customframework2.0");

        // Assert
        result1.ShouldNotBe(0); // Should still provide some ordering
        result2.ShouldNotBe(0); // Should order alphabetically as fallback
    }

    [TestMethod]
    public void Compare_HandlesHigherVersionNumbers()
    {
        // Act - Test with hypothetical future versions
        int result1 = comparer.Compare("net10.0", "net9.0");
        int result2 = comparer.Compare("net15.0", "net10.0");

        // Assert
        result1.ShouldBeLessThan(0); // net10.0 should come before net9.0
        result2.ShouldBeLessThan(0); // net15.0 should come before net10.0

        // Note: net100.0 vs net99.0 would require numeric parsing which is not
        // currently implemented as it's not needed for real-world scenarios
    }

    [TestMethod]
    public void Compare_NetStandardVersionOrdering()
    {
        // Act
        int result = comparer.Compare("netstandard2.0", "netstandard2.1");

        // Assert
        result.ShouldBeGreaterThan(0); // netstandard2.1 should come before netstandard2.0
    }

    [TestMethod]
    public void Compare_MixedFrameworkTypes()
    {
        // Arrange & Act - Test priority ordering: net > netcoreapp > netstandard
        var frameworks = new List<string>
        {
            "netstandard2.0",
            "net6.0",
            "netcoreapp3.1",
            "net8.0",
            "netstandard2.1",
            "net9.0"
        };

        var sorted = frameworks.OrderBy(f => f, comparer).ToList();

        // Assert - Should be ordered by priority and version
        sorted[0].ShouldBe("net9.0");
        sorted[1].ShouldBe("net8.0");
        sorted[2].ShouldBe("net6.0");
        sorted[3].ShouldBe("netcoreapp3.1");
        sorted[4].ShouldBe("netstandard2.1");
        sorted[5].ShouldBe("netstandard2.0");
    }

    [TestMethod]
    public void Compare_CaseInsensitive()
    {
        // Act
        int result1 = comparer.Compare("Net8.0", "net8.0");
        int result2 = comparer.Compare("NET8.0", "net8.0");
        int result3 = comparer.Compare("NetStandard2.1", "netstandard2.1");

        // Assert
        result1.ShouldBe(0);
        result2.ShouldBe(0);
        result3.ShouldBe(0);
    }
}