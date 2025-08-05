using System.Xml.Linq;
using ApiLens.Core.Models;

namespace ApiLens.Core.Parsing;

public class CrossReferenceExtractor
{
    public static ImmutableArray<CrossReference> ExtractReferences(XElement memberElement, string memberId)
    {
        ArgumentNullException.ThrowIfNull(memberElement);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberId);

        ImmutableArray<CrossReference>.Builder builder = ImmutableArray.CreateBuilder<CrossReference>();

        // Extract <see cref="..."/> elements
        foreach (XElement see in memberElement.Descendants("see"))
        {
            string? cref = see.Attribute("cref")?.Value;
            if (!string.IsNullOrWhiteSpace(cref))
            {
                builder.Add(new CrossReference
                {
                    SourceId = memberId,
                    TargetId = cref,
                    Type = ReferenceType.See,
                    Context = see.Parent?.Name.LocalName ?? "unknown"
                });
            }
        }

        // Extract <seealso cref="..."/> elements
        foreach (XElement seealso in memberElement.Descendants("seealso"))
        {
            string? cref = seealso.Attribute("cref")?.Value;
            if (!string.IsNullOrWhiteSpace(cref))
            {
                builder.Add(new CrossReference
                {
                    SourceId = memberId, TargetId = cref, Type = ReferenceType.SeeAlso, Context = "seealso"
                });
            }
        }

        // Extract <exception cref="..."/> elements
        foreach (XElement exception in memberElement.Descendants("exception"))
        {
            string? cref = exception.Attribute("cref")?.Value;
            if (!string.IsNullOrWhiteSpace(cref))
            {
                builder.Add(new CrossReference
                {
                    SourceId = memberId, TargetId = cref, Type = ReferenceType.Exception, Context = "exception"
                });
            }
        }

        return builder.ToImmutable();
    }
}