// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// Reads the assembly-level attributes the packaging suite judges straight from a PE image's metadata with
/// <c>System.Reflection.Metadata</c>: the image is never loaded, so no packed code runs and nothing needs reflection (trim-safe, P10-10, V6).
/// Every attribute read has only string constructor arguments, so each is decoded from its blob by hand.
/// </summary>
internal static class AssemblyMetadataReader
{
    // The only attributes decoded: each has a constructor of string parameters only, which is what the blob reading below assumes.
    private static readonly HashSet<string> s_read = new(StringComparer.Ordinal)
    {
        "System.Reflection.AssemblyMetadataAttribute",
        "System.Reflection.AssemblyInformationalVersionAttribute",
        "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
        "System.Runtime.Versioning.TargetFrameworkAttribute",
    };

    /// <summary>Reads the attributes of the assembly in <paramref name="image"/>.</summary>
    /// <param name="image">The bytes of a managed assembly.</param>
    /// <exception cref="ConformanceException">The bytes are not a managed assembly.</exception>
    internal static AssemblyFacts Read(byte[] image)
    {
        try
        {
            using var pe = new PEReader(new MemoryStream(image, writable: false));
            if (!pe.HasMetadata)
            {
                throw new ConformanceException("the packed library is not a managed assembly");
            }

            var reader = pe.GetMetadataReader();
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
            var visibleTo = new List<string>();
            string? informational = null;
            string? framework = null;

            foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
            {
                var attribute = reader.GetCustomAttribute(handle);
                var type = TypeName(reader, attribute);
                if (!s_read.Contains(type))
                {
                    continue;
                }

                var arguments = Strings(reader, attribute);
                switch (type)
                {
                    case "System.Reflection.AssemblyMetadataAttribute" when arguments.Count == 2:
                        metadata[arguments[0]] = arguments[1];
                        break;
                    case "System.Reflection.AssemblyInformationalVersionAttribute" when arguments.Count >= 1:
                        informational = arguments[0];
                        break;
                    case "System.Runtime.CompilerServices.InternalsVisibleToAttribute" when arguments.Count >= 1:
                        visibleTo.Add(arguments[0]);
                        break;
                    case "System.Runtime.Versioning.TargetFrameworkAttribute" when arguments.Count >= 1:
                        framework = arguments[0];
                        break;
                    default:
                        break;
                }
            }

            return new AssemblyFacts(metadata, informational, visibleTo, framework);
        }
        catch (BadImageFormatException ex)
        {
            throw new ConformanceException("the packed library is not a readable managed assembly", ex);
        }
    }

    private static string TypeName(MetadataReader reader, CustomAttribute attribute)
    {
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
        {
            return string.Empty;
        }

        var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        if (member.Parent.Kind != HandleKind.TypeReference)
        {
            return string.Empty;
        }

        var type = reader.GetTypeReference((TypeReferenceHandle)member.Parent);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }

    // The fixed arguments of an attribute whose constructor takes only strings: the 0x0001 prolog, then one serialized string per
    // constructor parameter (the count comes from the constructor's own signature, so named arguments after them are never read as strings).
    private static List<string> Strings(MetadataReader reader, CustomAttribute attribute)
    {
        var strings = new List<string>();
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
        {
            return strings;
        }

        var signature = reader.GetBlobReader(reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Signature);
        _ = signature.ReadSignatureHeader();
        var parameters = signature.ReadCompressedInteger();

        var blob = reader.GetBlobReader(attribute.Value);
        if (blob.Length < 2 || blob.ReadUInt16() != 1)
        {
            return strings;
        }

        for (var i = 0; i < parameters && blob.RemainingBytes > 0; i++)
        {
            var next = blob.ReadSerializedString();
            if (next is null)
            {
                break;
            }

            strings.Add(next);
        }

        return strings;
    }
}
