using ApiLens.Core.Lucene;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Querying;
using ApiLens.Core.Tests.Helpers;

namespace ApiLens.Core.Tests.Querying;

[TestClass]
public class SearchWithFiltersTests : IDisposable
{
    private string tempIndexPath = null!;
    private ILuceneIndexManager indexManager = null!;
    private IQueryEngine queryEngine = null!;

    [TestInitialize]
    public void Initialize()
    {
        tempIndexPath = Path.Combine(Path.GetTempPath(), $"apilens_test_{Guid.NewGuid()}");
        XmlDocumentParser parser = TestHelpers.CreateTestXmlDocumentParser();
        DocumentBuilder documentBuilder = new();
        indexManager = new LuceneIndexManager(tempIndexPath, parser, documentBuilder);

        // Add test members with various types and namespaces
        MemberInfo[] testMembers = new[]
        {
            CreateTestMember("ParseInt", MemberType.Method, "System", "System.Core"),
            CreateTestMember("ParseDouble", MemberType.Method, "System", "System.Core"),
            CreateTestMember("TryParse", MemberType.Method, "System", "System.Core"),
            CreateTestMember("JsonParser", MemberType.Type, "Newtonsoft.Json", "Newtonsoft.Json"),
            CreateTestMember("XmlParser", MemberType.Type, "System.Xml", "System.Xml"),
            CreateTestMember("ParseException", MemberType.Type, "System", "System.Core"),
            CreateTestMember("Count", MemberType.Property, "System.Collections", "System.Core"),
            CreateTestMember("Length", MemberType.Property, "System", "System.Core"),
            CreateTestMember("Serialize", MemberType.Method, "Newtonsoft.Json", "Newtonsoft.Json"),
            CreateTestMember("Deserialize", MemberType.Method, "Newtonsoft.Json", "Newtonsoft.Json"),
            CreateTestMember("IOException", MemberType.Type, "System.IO", "System.Core"),
            CreateTestMember("ArgumentException", MemberType.Type, "System", "System.Core"),
            CreateTestMember("Value", MemberType.Property, "Newtonsoft.Json.Linq", "Newtonsoft.Json"),
            CreateTestMember("Token", MemberType.Type, "Newtonsoft.Json.Linq", "Newtonsoft.Json"),
            CreateTestMember("SelectToken", MemberType.Method, "Newtonsoft.Json.Linq", "Newtonsoft.Json")
        };

        // Index the test members
        Task<IndexingResult> indexTask = indexManager.IndexBatchAsync(testMembers);
        indexTask.Wait();

        Task commitTask = indexManager.CommitAsync();
        commitTask.Wait();
        queryEngine = new QueryEngine(indexManager);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Dispose();
    }

    public void Dispose()
    {
        queryEngine?.Dispose();
        indexManager?.Dispose();

        if (!string.IsNullOrEmpty(tempIndexPath) && Directory.Exists(tempIndexPath))
        {
            try
            {
                Directory.Delete(tempIndexPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [TestMethod]
    public void SearchWithFilters_NamePatternOnly_AutoAddsWildcards()
    {
        // Act
        List<MemberInfo> results = queryEngine!.SearchWithFilters("Parse", null, null, null, 20);

        // Assert
        Assert.IsTrue(results.Count > 0, $"Expected results but got {results.Count}");
        Assert.IsTrue(results.All(r => r.Name.Contains("Parse", StringComparison.OrdinalIgnoreCase)));

        // Should find ParseInt, ParseDouble, TryParse, JsonParser, XmlParser, ParseException
        List<string> names = results.Select(r => r.Name).ToList();
        Assert.IsTrue(names.Contains("ParseInt"));
        Assert.IsTrue(names.Contains("ParseDouble"));
        Assert.IsTrue(names.Contains("TryParse"));
        Assert.IsTrue(names.Contains("JsonParser"));
        Assert.IsTrue(names.Contains("XmlParser"));
        Assert.IsTrue(names.Contains("ParseException"));
    }

    [TestMethod]
    public void SearchWithFilters_WithMemberTypeFilter()
    {
        // Act - Find only Method members with "Parse" in name
        List<MemberInfo> methods = queryEngine!.SearchWithFilters("Parse", MemberType.Method, null, null, 20);

        // Assert
        Assert.IsTrue(methods.Count > 0);
        Assert.IsTrue(methods.All(r => r.MemberType == MemberType.Method));
        Assert.IsTrue(methods.All(r => r.Name.Contains("Parse", StringComparison.OrdinalIgnoreCase)));

        // Should find ParseInt, ParseDouble, TryParse but not JsonParser (Type)
        List<string> methodNames = methods.Select(r => r.Name).ToList();
        Assert.IsTrue(methodNames.Contains("ParseInt"));
        Assert.IsTrue(methodNames.Contains("ParseDouble"));
        Assert.IsTrue(methodNames.Contains("TryParse"));
        Assert.IsFalse(methodNames.Contains("JsonParser"));
    }

    [TestMethod]
    public void SearchWithFilters_WithNamespaceFilter()
    {
        // Act - Find items in Newtonsoft.Json namespace
        List<MemberInfo> results = queryEngine!.SearchWithFilters("Serialize", null, "Newtonsoft.Json", null, 20);

        // Assert
        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.Namespace == "Newtonsoft.Json"));
        Assert.IsTrue(results.All(r => r.Name.Contains("Serialize", StringComparison.OrdinalIgnoreCase)));

        List<string> names = results.Select(r => r.Name).ToList();
        Assert.IsTrue(names.Contains("Serialize") || names.Contains("Deserialize"));
    }

    [TestMethod]
    public void SearchWithFilters_WithNamespaceWildcard()
    {
        // Act - Find items in any Newtonsoft namespace
        List<MemberInfo> results = queryEngine!.SearchWithFilters("Token", null, "Newtonsoft.*", null, 20);

        // Assert
        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.Namespace.StartsWith("Newtonsoft.")));

        List<string> names = results.Select(r => r.Name).ToList();
        Assert.IsTrue(names.Contains("Token") || names.Contains("SelectToken"));
    }

    [TestMethod]
    public void SearchWithFilters_WithAssemblyFilter()
    {
        // Act - Find items in Newtonsoft.Json assembly
        List<MemberInfo> results = queryEngine!.SearchWithFilters("Json", null, null, "Newtonsoft.Json", 20);

        // Assert
        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.Assembly == "Newtonsoft.Json"));

        List<string> names = results.Select(r => r.Name).ToList();
        Assert.IsTrue(names.Contains("JsonParser"));
    }

    [TestMethod]
    public void SearchWithFilters_CombinedFilters()
    {
        // Act - Find Type members with "Exception" in System namespace
        List<MemberInfo> results = queryEngine!.SearchWithFilters("Exception", MemberType.Type, "System", "System.Core", 20);

        // Assert
        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.MemberType == MemberType.Type));
        Assert.IsTrue(results.All(r => r.Namespace == "System"));
        Assert.IsTrue(results.All(r => r.Assembly == "System.Core"));
        Assert.IsTrue(results.All(r => r.Name.Contains("Exception")));

        List<string> names = results.Select(r => r.Name).ToList();
        Assert.IsTrue(names.Contains("ParseException") || names.Contains("ArgumentException"));
    }

    [TestMethod]
    public void SearchWithFilters_ExplicitWildcardNotDuplicated()
    {
        // Act - User provides explicit wildcards
        List<MemberInfo> results = queryEngine!.SearchWithFilters("*Exception", MemberType.Type, null, null, 20);

        // Assert
        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.Name.EndsWith("Exception")));
        Assert.IsTrue(results.All(r => r.MemberType == MemberType.Type));

        List<string> names = results.Select(r => r.Name).ToList();
        Assert.IsTrue(names.Contains("ParseException"));
        Assert.IsTrue(names.Contains("IOException"));
        Assert.IsTrue(names.Contains("ArgumentException"));
    }

    [TestMethod]
    public void SearchWithFilters_PropertiesWithFilter()
    {
        // Act - Find properties
        List<MemberInfo> results = queryEngine!.SearchWithFilters("Count", MemberType.Property, null, null, 20);

        // Assert
        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.MemberType == MemberType.Property));

        List<string> names = results.Select(r => r.Name).ToList();
        Assert.IsTrue(names.Contains("Count"));
    }

    [TestMethod]
    public void SearchWithFilters_ComplexWildcardPattern()
    {
        // Act - Find all items with "Json" anywhere and in Newtonsoft namespaces
        List<MemberInfo> results = queryEngine!.SearchWithFilters("*Json*", null, "Newtonsoft.*", null, 20);

        // Assert
        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.Name.Contains("Json", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(results.All(r => r.Namespace.StartsWith("Newtonsoft.")));
    }

    private static MemberInfo CreateTestMember(string name, MemberType type, string namespaceName, string assembly)
    {
        return new MemberInfo
        {
            Id = $"{type.ToString()[0]}:{namespaceName}.{name}",
            Name = name,
            FullName = $"{namespaceName}.{name}",
            MemberType = type,
            Namespace = namespaceName,
            Assembly = assembly,
            Summary = $"Test {type} {name}"
        };
    }
}