using ApiLens.Core.Formatting;

namespace ApiLens.Core.Tests.Formatting;

[TestClass]
public class GenericTypeFormatterTests
{
    [TestMethod]
    public void FormatTypeName_WithSingleBacktick_ReturnsAngleBrackets()
    {
        // Arrange
        string input = "List`1";

        // Act
        string result = GenericTypeFormatter.FormatTypeName(input);

        // Assert
        result.ShouldBe("List<T>");
    }

    [TestMethod]
    public void FormatTypeName_WithDoubleBacktick_ReturnsAngleBrackets()
    {
        // Arrange
        string input = "ToIReadOnlyListUnsafe``1";

        // Act
        string result = GenericTypeFormatter.FormatTypeName(input);

        // Assert
        result.ShouldBe("ToIReadOnlyListUnsafe<T>");
    }

    [TestMethod]
    public void FormatTypeName_WithMultipleParameters_ReturnsCorrectFormat()
    {
        // Arrange
        string input1 = "Dictionary`2";
        string input2 = "Func`3";

        // Act
        string result1 = GenericTypeFormatter.FormatTypeName(input1);
        string result2 = GenericTypeFormatter.FormatTypeName(input2);

        // Assert
        result1.ShouldBe("Dictionary<TKey,TValue>");
        result2.ShouldBe("Func<T1,T2,T3>");
    }

    [TestMethod]
    public void FormatTypeName_WithoutBacktick_ReturnsOriginal()
    {
        // Arrange
        string input = "String";

        // Act
        string result = GenericTypeFormatter.FormatTypeName(input);

        // Assert
        result.ShouldBe("String");
    }

    [TestMethod]
    public void FormatTypeName_WithEmpty_ReturnsEmpty()
    {
        // Arrange & Act
        string result1 = GenericTypeFormatter.FormatTypeName("");
        string result2 = GenericTypeFormatter.FormatTypeName(null!);
        string result3 = GenericTypeFormatter.FormatTypeName("   ");

        // Assert
        result1.ShouldBe("");
        result2.ShouldBeNull();
        result3.ShouldBe("   ");
    }

    [TestMethod]
    public void FormatFullName_WithNamespace_FormatsCorrectly()
    {
        // Arrange
        string input = "System.Collections.Generic.List`1";

        // Act
        string result = GenericTypeFormatter.FormatFullName(input);

        // Assert
        result.ShouldBe("System.Collections.Generic.List<T>");
    }

    [TestMethod]
    public void FormatFullName_WithMethodSignature_FormatsMethodName()
    {
        // Arrange
        string input = "System.Linq.CollectionExtensions.ToIReadOnlyListUnsafe``1(System.Collections.Generic.IEnumerable{``0})";

        // Act
        string result = GenericTypeFormatter.FormatFullName(input);

        // Assert
        result.ShouldBe("System.Linq.CollectionExtensions.ToIReadOnlyListUnsafe<T>(System.Collections.Generic.IEnumerable{``0})");
    }

    [TestMethod]
    public void FormatTypeName_WithGenericMethodMultipleParams_FormatsCorrectly()
    {
        // Arrange
        string input = "Convert``2";

        // Act
        string result = GenericTypeFormatter.FormatTypeName(input);

        // Assert
        result.ShouldBe("Convert<TKey,TValue>");
    }
}