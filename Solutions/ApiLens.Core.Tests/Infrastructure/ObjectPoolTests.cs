using System.Text;
using ApiLens.Core.Infrastructure;

namespace ApiLens.Core.Tests.Infrastructure;

[TestClass]
public class ObjectPoolTests
{
    [TestMethod]
    public void Rent_EmptyPool_CreatesNewObject()
    {
        // Arrange
        int createCount = 0;
        ObjectPool<StringBuilder> pool = new(() =>
        {
            createCount++;
            return new StringBuilder();
        });

        // Act
        StringBuilder sb = pool.Rent();

        // Assert
        sb.ShouldNotBeNull();
        createCount.ShouldBe(1);
        pool.TotalCreated.ShouldBe(1);
    }

    [TestMethod]
    public void Return_ValidObject_AddsToPool()
    {
        // Arrange
        ObjectPool<StringBuilder> pool = new(() => new StringBuilder());
        StringBuilder sb = pool.Rent();

        // Act
        pool.Return(sb);

        // Assert
        pool.Count.ShouldBe(1);
    }

    [TestMethod]
    public void Rent_AfterReturn_ReusesObject()
    {
        // Arrange
        ObjectPool<StringBuilder> pool = new(() => new StringBuilder());
        StringBuilder original = pool.Rent();
        original.Append("test");
        pool.Return(original);

        // Act
        StringBuilder reused = pool.Rent();

        // Assert
        reused.ShouldBeSameAs(original);
        pool.Count.ShouldBe(0); // Object was taken from pool
    }

    [TestMethod]
    public void Return_WithResetAction_ResetsObject()
    {
        // Arrange
        bool resetCalled = false;
        ObjectPool<StringBuilder> pool = new(
            () => new StringBuilder(),
            sb =>
            {
                resetCalled = true;
                sb.Clear();
            });

        StringBuilder sb = pool.Rent();
        sb.Append("test");

        // Act
        pool.Return(sb);

        // Assert
        resetCalled.ShouldBeTrue();
        sb.Length.ShouldBe(0); // Reset action cleared the StringBuilder
    }

    [TestMethod]
    public void Return_ExceedsMaxSize_DoesNotAddToPool()
    {
        // Arrange
        ObjectPool<StringBuilder> pool = new(() => new StringBuilder(), maxSize: 2);

        // Create objects up to max size
        StringBuilder sb1 = pool.Rent();
        StringBuilder sb2 = pool.Rent();

        // Act - Return objects
        pool.Return(sb1);
        pool.Return(sb2);

        // Now create another object when we're at max
        StringBuilder sb3 = pool.Rent(); // This takes one from pool
        StringBuilder sb4 = pool.Rent(); // This takes another from pool
        StringBuilder sb5 = pool.Rent(); // This creates a new one (total = 3)

        pool.Return(sb3);
        pool.Return(sb4);
        pool.Return(sb5); // This one won't be added to pool

        // Assert
        pool.Count.ShouldBe(2); // Pool is still at max size
        pool.TotalCreated.ShouldBe(2); // Total created decremented after discarding sb5
    }

    [TestMethod]
    public void Rent_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        int createCount = 0;
        ObjectPool<TestObject> pool = new(() =>
        {
            Interlocked.Increment(ref createCount);
            return new TestObject();
        });

        const int threadCount = 10;
        const int operationsPerThread = 100;
        List<Task> tasks = [];

        // Act
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < operationsPerThread; j++)
                {
                    TestObject obj = pool.Rent();
                    Thread.Yield(); // Encourage context switching
                    pool.Return(obj);
                }
            }));
        }

        Task.WaitAll([.. tasks]);

        // Assert
        // All objects should be returned to pool (or discarded if over max size)
        pool.Count.ShouldBeGreaterThanOrEqualTo(0);
        pool.Count.ShouldBeLessThanOrEqualTo(1024); // Default max size
        createCount.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public void Constructor_NullObjectGenerator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ObjectPool<StringBuilder>(null!));
    }

    [TestMethod]
    public void Return_NullObject_ThrowsArgumentNullException()
    {
        // Arrange
        ObjectPool<StringBuilder> pool = new(() => new StringBuilder());

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => pool.Return(null!));
    }

    [TestMethod]
    public void Count_ReflectsPoolSize()
    {
        // Arrange
        ObjectPool<StringBuilder> pool = new(() => new StringBuilder());

        // Act & Assert
        pool.Count.ShouldBe(0);

        StringBuilder sb1 = pool.Rent();
        pool.Count.ShouldBe(0);

        pool.Return(sb1);
        pool.Count.ShouldBe(1);

        StringBuilder sb2 = pool.Rent();
        pool.Count.ShouldBe(0);
    }

    [TestMethod]
    public void TotalCreated_TracksAllCreatedObjects()
    {
        // Arrange
        ObjectPool<StringBuilder> pool = new(() => new StringBuilder(), maxSize: 2);

        // Act
        StringBuilder sb1 = pool.Rent();
        pool.TotalCreated.ShouldBe(1);

        StringBuilder sb2 = pool.Rent();
        pool.TotalCreated.ShouldBe(2);

        StringBuilder sb3 = pool.Rent();
        pool.TotalCreated.ShouldBe(3);

        // Return objects
        pool.Return(sb1);
        pool.Return(sb2);
        pool.Return(sb3); // This one won't be added to pool (exceeds max)

        pool.TotalCreated.ShouldBe(2); // Decremented when object discarded
    }

    [TestMethod]
    public void MultipleRentReturn_MaintainsCorrectState()
    {
        // Arrange
        ObjectPool<StringBuilder> pool = new(() => new StringBuilder());
        List<StringBuilder> objects = [];

        // Act - Rent multiple objects
        for (int i = 0; i < 5; i++)
        {
            objects.Add(pool.Rent());
        }

        // Return all objects
        foreach (StringBuilder obj in objects)
        {
            pool.Return(obj);
        }

        // Rent again
        List<StringBuilder> reusedObjects = [];
        for (int i = 0; i < 5; i++)
        {
            reusedObjects.Add(pool.Rent());
        }

        // Assert
        // All objects should be reused
        foreach (StringBuilder reused in reusedObjects)
        {
            objects.ShouldContain(reused);
        }
        pool.Count.ShouldBe(0); // All taken from pool
    }

    [TestMethod]
    public void ObjectPool_WithCustomMaxSize_RespectsLimit()
    {
        // Arrange
        const int maxSize = 3;
        ObjectPool<StringBuilder> pool = new(() => new StringBuilder(), maxSize: maxSize);

        // Act - Create more objects than max size
        List<StringBuilder> objects = [];
        for (int i = 0; i < 5; i++)
        {
            objects.Add(pool.Rent());
        }

        pool.TotalCreated.ShouldBe(5); // All objects were created

        // Return first 3 objects
        for (int i = 0; i < 3; i++)
        {
            pool.Return(objects[i]);
        }

        pool.Count.ShouldBe(3); // Pool filled to max

        // Return remaining objects
        for (int i = 3; i < 5; i++)
        {
            pool.Return(objects[i]);
        }

        // Assert
        pool.Count.ShouldBe(3); // Pool still at max size
        pool.TotalCreated.ShouldBe(3); // Decremented for discarded objects
    }

    private class TestObject
    {
        public int Value { get; set; }
    }
}