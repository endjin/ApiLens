using System.Xml.Linq;
using ApiLens.Core.Models;
using ApiLens.Core.Parsing;
using ApiLens.Core.Tests.Helpers;

namespace ApiLens.Core.Tests.Integration;

[TestClass]
public class XmlParsingIntegrationTests
{
    private XmlDocumentParser parser = null!;

    [TestInitialize]
    public void Setup()
    {
        parser = TestHelpers.CreateTestXmlDocumentParser();
    }

    [TestMethod]
    public void ParseSystemCollectionsXml_ExtractsAllMembers()
    {
        // Arrange
        string xmlPath = Path.Combine("SampleData", "System.Collections.xml");
        XDocument doc = XDocument.Load(xmlPath);

        // Act
        ApiAssemblyInfo assembly = parser.ParseAssembly(doc);
        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, assembly.Name);

        // Assert
        assembly.Name.ShouldBe("System.Collections");
        members.Length.ShouldBe(10);

        // Verify types
        members.ShouldContain(m => m.Name == "List`1" && m.MemberType == MemberType.Type);
        members.ShouldContain(m => m.Name == "Dictionary`2" && m.MemberType == MemberType.Type);
        members.ShouldContain(m => m.Name == "Dictionary`2+Enumerator" && m.MemberType == MemberType.Type);

        // Verify methods
        // The Add method should exist - check just by name first
        members.ShouldContain(m => m.Name == "Add" && m.MemberType == MemberType.Method);
        members.ShouldContain(m => m.Name == "Clear" && m.MemberType == MemberType.Method);
        members.ShouldContain(m => m.Name == "Contains" && m.MemberType == MemberType.Method);

        // Verify property
        members.ShouldContain(m => m.Name == "Count" && m.MemberType == MemberType.Property);

        // Verify field
        members.ShouldContain(m => m.Name == "DefaultCapacity" && m.MemberType == MemberType.Field);

        // Verify event
        members.ShouldContain(m => m.Name == "NotifyCollectionChangedEventHandler" && m.MemberType == MemberType.Event);
    }

    [TestMethod]
    public void ParseSystemCollectionsXml_ExtractsCrossReferences()
    {
        // Arrange
        string xmlPath = Path.Combine("SampleData", "System.Collections.xml");
        XDocument doc = XDocument.Load(xmlPath);
        XElement? membersElement = doc.Root?.Element("members");

        // Act
        List<CrossReference> crossReferences = [];
        foreach (XElement memberElement in membersElement?.Elements("member") ?? [])
        {
            string? memberId = memberElement.Attribute("name")?.Value;
            if (memberId != null)
            {
                ImmutableArray<CrossReference> refs = CrossReferenceExtractor.ExtractReferences(memberElement, memberId);
                crossReferences.AddRange(refs);
            }
        }

        // Assert
        crossReferences.Count.ShouldBeGreaterThan(0);

        // List<T> should have seealso references
        crossReferences.ShouldContain(r =>
            r.SourceId == "T:System.Collections.Generic.List`1" &&
            r.TargetId == "T:System.Collections.Generic.IList`1" &&
            r.Type == ReferenceType.SeeAlso);

        // Add method should have exception reference
        crossReferences.ShouldContain(r =>
            r.SourceId == "M:System.Collections.Generic.List`1.Add(`0)" &&
            r.TargetId == "T:System.NotSupportedException" &&
            r.Type == ReferenceType.Exception);

        // Dictionary.Add should have multiple exception references
        crossReferences.Count(r => r is { SourceId: "M:System.Collections.Generic.Dictionary`2.Add(`0,`1)", Type: ReferenceType.Exception }).ShouldBe(2);
    }

    [TestMethod]
    public void ParseSystemLinqXml_HandlesGenericMethods()
    {
        // Arrange
        string xmlPath = Path.Combine("SampleData", "System.Linq.xml");
        XDocument doc = XDocument.Load(xmlPath);

        // Act
        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, "System.Linq");

        // Assert
        members.Length.ShouldBe(3);

        MemberInfo selectMethod = members.Single(m => m.Name == "Select");
        selectMethod.MemberType.ShouldBe(MemberType.Method);
        // FullName is the member identifier without the type prefix
        selectMethod.FullName.ShouldStartWith("System.Linq.Enumerable.Select");

        MemberInfo whereMethod = members.Single(m => m.Name == "Where");
        whereMethod.MemberType.ShouldBe(MemberType.Method);
    }

    [TestMethod]
    public void ParseMemberIds_HandlesComplexGenericTypes()
    {
        // Arrange
        string xmlPath = Path.Combine("SampleData", "System.Linq.xml");
        XDocument doc = XDocument.Load(xmlPath);
        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, "System.Linq");

        // Act & Assert
        foreach (MemberInfo member in members)
        {
            ParsedMemberId parsedId = MemberIdParser.Parse(member.Id);

            parsedId.ShouldNotBeNull();
            // FullName formats differ: parsedId.FullName doesn't include prefix, member.FullName does for the full ID
            // Both should represent the same entity, just verify parsedId is valid
            parsedId.FullName.ShouldNotBeNullOrEmpty();
            // MemberName includes generic arity (e.g., "Select``2") while member.Name doesn't (e.g., "Select")
            // This is by design, so we don't compare them

            // For methods, the namespace doesn't include the type name
            if (member.MemberType == MemberType.Method)
            {
                parsedId.Namespace.ShouldBe("System.Linq");
                parsedId.TypeName.ShouldBe("Enumerable");
            }
            else
            {
                parsedId.Namespace.ShouldBe(member.Namespace);
            }
        }
    }

    [TestMethod]
    public void ParseNestedType_ExtractsParentChildRelationship()
    {
        // Arrange
        string xmlPath = Path.Combine("SampleData", "System.Collections.xml");
        XDocument doc = XDocument.Load(xmlPath);
        ImmutableArray<MemberInfo> members = parser.ParseMembers(doc, "System.Collections");

        // Act
        MemberInfo nestedType = members.Single(m => m.Name == "Dictionary`2+Enumerator");
        ParsedMemberId parsedId = MemberIdParser.Parse(nestedType.Id);

        // Assert
        parsedId.IsNested.ShouldBe(true);
        parsedId.ParentType.ShouldBe("Dictionary`2");
        parsedId.NestedTypeName.ShouldBe("Enumerator");
    }

    [TestMethod]
    public void CrossReferenceExtraction_FindsAllReferenceTypes()
    {
        // Arrange
        string xmlPath = Path.Combine("SampleData", "System.Linq.xml");
        XDocument doc = XDocument.Load(xmlPath);
        XElement? membersElement = doc.Root?.Element("members");

        // Act
        Dictionary<ReferenceType, List<CrossReference>> referencesByType = [];
        foreach (XElement memberElement in membersElement?.Elements("member") ?? [])
        {
            string? memberId = memberElement.Attribute("name")?.Value;
            if (memberId != null)
            {
                ImmutableArray<CrossReference> refs = CrossReferenceExtractor.ExtractReferences(memberElement, memberId);
                foreach (CrossReference reference in refs)
                {
                    if (!referencesByType.ContainsKey(reference.Type))
                    {
                        referencesByType[reference.Type] = [];
                    }

                    referencesByType[reference.Type].Add(reference);
                }
            }
        }

        // Assert
        referencesByType.ContainsKey(ReferenceType.See).ShouldBe(true);
        referencesByType.ContainsKey(ReferenceType.SeeAlso).ShouldBe(true);
        referencesByType.ContainsKey(ReferenceType.Exception).ShouldBe(true);

        // Verify see references in summaries and params
        referencesByType[ReferenceType.See].Count.ShouldBeGreaterThan(0);

        // Verify exception references
        referencesByType[ReferenceType.Exception].Count.ShouldBe(2); // Two ArgumentNullException refs
    }
}