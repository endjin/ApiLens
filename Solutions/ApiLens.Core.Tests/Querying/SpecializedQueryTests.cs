using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Querying;
using Lucene.Net.Documents;
using Lucene.Net.Store;

namespace ApiLens.Core.Tests.Querying;

[TestClass]
public class SpecializedQueryTests : IDisposable
{
    private readonly RAMDirectory directory = new();
    private readonly ILuceneIndexManager indexManager;
    private readonly IDocumentBuilder documentBuilder = new DocumentBuilder();
    private readonly IQueryEngine queryEngine;

    public SpecializedQueryTests()
    {
        indexManager = new LuceneIndexManager(directory);
        queryEngine = new QueryEngine(indexManager);
    }

    [TestMethod]
    public void SearchByCodeExample_WithMatchingPattern_ShouldReturnResults()
    {
        // Arrange
        MemberInfo member = CreateTestMemberWithCodeExample();
        Document doc = documentBuilder.BuildDocument(member);
        indexManager.AddDocument(doc);
        indexManager.Commit();

        // Act - First search by name to verify basic indexing works
        List<MemberInfo> nameResults = queryEngine.SearchByName("ProcessOrder", 10);
        nameResults.Count.ShouldBe(1); // Verify the document is indexed

        // Now try content search
        List<MemberInfo> contentResults = queryEngine.SearchByContent("ProcessOrder", 10);
        contentResults.Count.ShouldBe(1); // Verify content field works

        // Verify code examples are indexed
        List<MemberInfo> withExamples = queryEngine.GetMethodsWithExamples(10);
        withExamples.Count.ShouldBe(1); // Should find documents with code examples

        // Finally try code example search - use exact word that appears in the code
        List<MemberInfo> results = queryEngine.SearchByCodeExample("total", 10);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("ProcessOrder");
        results[0].CodeExamples.Length.ShouldBe(1);
        results[0].CodeExamples[0].Code.ShouldContain("CalculateTotal");
    }

    [TestMethod]
    public void GetByExceptionType_WithMatchingException_ShouldReturnResults()
    {
        // Arrange
        MemberInfo member = CreateTestMemberWithExceptions();
        Document doc = documentBuilder.BuildDocument(member);
        indexManager.AddDocument(doc);
        indexManager.Commit();

        // Act
        List<MemberInfo> results = queryEngine.GetByExceptionType("System.ArgumentNullException", 10);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("ValidateInput");
        results[0].Exceptions.Length.ShouldBe(2);
        results[0].Exceptions.Any(e => e.Type == "System.ArgumentNullException").ShouldBeTrue();
    }

    [TestMethod]
    public void GetByParameterCount_WithinRange_ShouldReturnResults()
    {
        // Arrange
        MemberInfo member1 = CreateTestMemberWithParameters(2);
        MemberInfo member2 = CreateTestMemberWithParameters(3);
        MemberInfo member3 = CreateTestMemberWithParameters(5);

        indexManager.AddDocument(documentBuilder.BuildDocument(member1));
        indexManager.AddDocument(documentBuilder.BuildDocument(member2));
        indexManager.AddDocument(documentBuilder.BuildDocument(member3));
        indexManager.Commit();

        // Act
        List<MemberInfo> results = queryEngine.GetByParameterCount(2, 3, 10);

        // Assert
        results.Count.ShouldBe(2);
        results.All(r => r.Complexity?.ParameterCount >= 2 && r.Complexity?.ParameterCount <= 3).ShouldBeTrue();
    }

    [TestMethod]
    public void GetMethodsWithExamples_ShouldReturnOnlyMethodsWithExamples()
    {
        // Arrange
        MemberInfo withExample = CreateTestMemberWithCodeExample();
        MemberInfo withoutExample = CreateTestMemberWithoutCodeExample();

        indexManager.AddDocument(documentBuilder.BuildDocument(withExample));
        indexManager.AddDocument(documentBuilder.BuildDocument(withoutExample));
        indexManager.Commit();

        // Act
        List<MemberInfo> results = queryEngine.GetMethodsWithExamples(10);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Name.ShouldBe("ProcessOrder");
        results[0].CodeExamples.Length.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public void GetComplexMethods_WithMinComplexity_ShouldReturnResults()
    {
        // Arrange
        MemberInfo simple = CreateTestMemberWithComplexity(1);
        MemberInfo moderate = CreateTestMemberWithComplexity(5);
        MemberInfo complex = CreateTestMemberWithComplexity(10);

        indexManager.AddDocument(documentBuilder.BuildDocument(simple));
        indexManager.AddDocument(documentBuilder.BuildDocument(moderate));
        indexManager.AddDocument(documentBuilder.BuildDocument(complex));
        indexManager.Commit();

        // Act
        List<MemberInfo> results = queryEngine.GetComplexMethods(5, 10);

        // Assert
        results.Count.ShouldBe(2);
        results.All(r => r.Complexity?.CyclomaticComplexity >= 5).ShouldBeTrue();
    }

    [TestMethod]
    public void GetByParameterCount_InvalidRange_ShouldThrowException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => queryEngine.GetByParameterCount(5, 2, 10))
            .Message.ShouldContain("Min parameter count cannot be greater than max");
    }

    private static MemberInfo CreateTestMemberWithCodeExample()
    {
        return new MemberInfo
        {
            Id = "M:Test.ProcessOrder",
            MemberType = MemberType.Method,
            Name = "ProcessOrder",
            FullName = "Test.ProcessOrder",
            Assembly = "Test.dll",
            Namespace = "Test",
            Summary = "Processes an order",
            CodeExamples =
            [
                new CodeExample
                {
                    Description = "Basic usage",
                    Code = "var total = CalculateTotal(order);\nProcessOrder(order, total);"
                }
            ],
            Complexity = new ComplexityMetrics
            {
                ParameterCount = 2,
                CyclomaticComplexity = 3,
                DocumentationLineCount = 5
            }
        };
    }

    private static MemberInfo CreateTestMemberWithoutCodeExample()
    {
        return new MemberInfo
        {
            Id = "M:Test.SimpleMethod",
            MemberType = MemberType.Method,
            Name = "SimpleMethod",
            FullName = "Test.SimpleMethod",
            Assembly = "Test.dll",
            Namespace = "Test",
            Summary = "A simple method"
        };
    }

    private static MemberInfo CreateTestMemberWithExceptions()
    {
        return new MemberInfo
        {
            Id = "M:Test.ValidateInput",
            MemberType = MemberType.Method,
            Name = "ValidateInput",
            FullName = "Test.ValidateInput",
            Assembly = "Test.dll",
            Namespace = "Test",
            Summary = "Validates input",
            Exceptions =
            [
                new ExceptionInfo { Type = "System.ArgumentNullException", Condition = "When input is null" },
                new ExceptionInfo { Type = "System.ArgumentException", Condition = "When input is invalid" }
            ]
        };
    }

    private static MemberInfo CreateTestMemberWithParameters(int paramCount)
    {
        List<ParameterInfo> parameters = [];
        for (int i = 0; i < paramCount; i++)
        {
            parameters.Add(new ParameterInfo
            {
                Name = $"param{i}",
                Type = "string",
                Position = i,
                IsOptional = false,
                IsParams = false,
                IsOut = false,
                IsRef = false
            });
        }

        return new MemberInfo
        {
            Id = $"M:Test.Method{paramCount}Params",
            MemberType = MemberType.Method,
            Name = $"Method{paramCount}Params",
            FullName = $"Test.Method{paramCount}Params",
            Assembly = "Test.dll",
            Namespace = "Test",
            Summary = $"Method with {paramCount} parameters",
            Parameters = [.. parameters],
            Complexity = new ComplexityMetrics
            {
                ParameterCount = paramCount,
                CyclomaticComplexity = 1,
                DocumentationLineCount = 3
            }
        };
    }

    private static MemberInfo CreateTestMemberWithComplexity(int complexity)
    {
        return new MemberInfo
        {
            Id = $"M:Test.Method{complexity}Complexity",
            MemberType = MemberType.Method,
            Name = $"Method{complexity}Complexity",
            FullName = $"Test.Method{complexity}Complexity",
            Assembly = "Test.dll",
            Namespace = "Test",
            Summary = $"Method with complexity {complexity}",
            Complexity = new ComplexityMetrics
            {
                ParameterCount = 1,
                CyclomaticComplexity = complexity,
                DocumentationLineCount = 10
            }
        };
    }

    public void Dispose()
    {
        queryEngine.Dispose();
        indexManager.Dispose();
        directory.Dispose();
    }
}