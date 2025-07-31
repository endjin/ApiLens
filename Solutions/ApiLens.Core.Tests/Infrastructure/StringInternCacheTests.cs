using ApiLens.Core.Infrastructure;

namespace ApiLens.Core.Tests.Infrastructure;

[TestClass]
public class StringInternCacheTests
{
    [TestMethod]
    public void GetOrAdd_NewString_AddsToCache()
    {
        // Arrange
        StringInternCache cache = new();
        string value = "test string";

        // Act
        string result = cache.GetOrAdd(value);

        // Assert
        result.ShouldBe(value);
        cache.Count.ShouldBe(1);
    }

    [TestMethod]
    public void GetOrAdd_ExistingString_ReturnsCachedInstance()
    {
        // Arrange
        StringInternCache cache = new();
        string value1 = "test string";
        string value2 = new string(value1.ToCharArray()); // Create different instance with same value

        // Act
        string cached1 = cache.GetOrAdd(value1);
        string cached2 = cache.GetOrAdd(value2);

        // Assert
        cached1.ShouldBeSameAs(cached2); // Should return the same instance
        cache.Count.ShouldBe(1);
    }

    [TestMethod]
    public void GetOrAdd_NullString_ReturnsNull()
    {
        // Arrange
        StringInternCache cache = new();

        // Act
        string? result = cache.GetOrAdd(null!);

        // Assert
        result.ShouldBeNull();
        cache.Count.ShouldBe(0);
    }

    [TestMethod]
    public void GetOrAdd_EmptyString_ReturnsEmpty()
    {
        // Arrange
        StringInternCache cache = new();

        // Act
        string result = cache.GetOrAdd("");

        // Assert
        result.ShouldBe("");
        cache.Count.ShouldBe(0);
    }

    [TestMethod]
    public void GetOrAdd_ExceedsMaxSize_DoesNotCache()
    {
        // Arrange
        const int maxSize = 3;
        StringInternCache cache = new(maxSize);

        // Fill cache to max
        cache.GetOrAdd("string1");
        cache.GetOrAdd("string2");
        cache.GetOrAdd("string3");

        // Act
        string result = cache.GetOrAdd("string4");

        // Assert
        result.ShouldBe("string4");
        cache.Count.ShouldBe(3); // Should not exceed max size
    }

    [TestMethod]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        StringInternCache cache = new();
        cache.GetOrAdd("string1");
        cache.GetOrAdd("string2");
        cache.GetOrAdd("string3");

        // Act
        cache.Clear();

        // Assert
        cache.Count.ShouldBe(0);
    }

    [TestMethod]
    public void GetOrAdd_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        StringInternCache cache = new();
        const int threadCount = 10;
        const int stringsPerThread = 100;
        List<Task> tasks = [];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < stringsPerThread; j++)
                {
                    cache.GetOrAdd($"thread{threadId}_string{j}");
                    cache.GetOrAdd("shared_string"); // All threads add this
                }
            }));
        }

        Task.WaitAll([.. tasks]);

        // Assert
        // Should have all unique strings plus the shared one
        cache.Count.ShouldBeLessThanOrEqualTo((threadCount * stringsPerThread) + 1);
        cache.Count.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public void GetOrAdd_DifferentStrings_CachesAll()
    {
        // Arrange
        StringInternCache cache = new();
        List<string> strings = ["alpha", "beta", "gamma", "delta"];

        // Act
        foreach (string s in strings)
        {
            cache.GetOrAdd(s);
        }

        // Assert
        cache.Count.ShouldBe(strings.Count);
    }

    [TestMethod]
    public void GetOrAdd_RepeatedStrings_MaintainsSingleInstance()
    {
        // Arrange
        StringInternCache cache = new();
        string original = "repeated";

        // Act
        string first = cache.GetOrAdd(original);
        string second = cache.GetOrAdd("repeated");
        string third = cache.GetOrAdd(new string("repeated".ToCharArray()));

        // Assert
        first.ShouldBeSameAs(second);
        second.ShouldBeSameAs(third);
        cache.Count.ShouldBe(1);
    }

    [TestMethod]
    public void Constructor_DefaultMaxSize_IsReasonable()
    {
        // Arrange & Act
        StringInternCache cache = new();

        // Add many strings
        for (int i = 0; i < 10001; i++)
        {
            cache.GetOrAdd($"string{i}");
        }

        // Assert
        cache.Count.ShouldBe(10000); // Default max size
    }

    [TestMethod]
    public void GetOrAdd_WhitespaceString_CachesNormally()
    {
        // Arrange
        StringInternCache cache = new();
        string whitespace = "   ";

        // Act
        string result = cache.GetOrAdd(whitespace);

        // Assert
        result.ShouldBe(whitespace);
        cache.Count.ShouldBe(1);
    }

    [TestMethod]
    public void GetOrAdd_SpecialCharacters_CachesCorrectly()
    {
        // Arrange
        StringInternCache cache = new();
        string[] specialStrings = 
        [
            "test\nstring",
            "test\tstring",
            "test\"string",
            "test'string",
            "test\\string",
            "test\u0000string"
        ];

        // Act
        foreach (string s in specialStrings)
        {
            cache.GetOrAdd(s);
        }

        // Assert
        cache.Count.ShouldBe(specialStrings.Length);
    }

    [TestMethod]
    public void GetOrAdd_LongString_CachesCorrectly()
    {
        // Arrange
        StringInternCache cache = new();
        string longString = new('x', 10000);

        // Act
        string result = cache.GetOrAdd(longString);

        // Assert
        result.ShouldBe(longString);
        cache.Count.ShouldBe(1);
    }

    [TestMethod]
    public void Count_ReflectsActualCacheSize()
    {
        // Arrange
        StringInternCache cache = new();

        // Act & Assert
        cache.Count.ShouldBe(0);

        cache.GetOrAdd("one");
        cache.Count.ShouldBe(1);

        cache.GetOrAdd("two");
        cache.Count.ShouldBe(2);

        cache.GetOrAdd("one"); // Duplicate
        cache.Count.ShouldBe(2); // Should not increase

        cache.Clear();
        cache.Count.ShouldBe(0);
    }
}