// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Reflection;
using Dexpace.Sdk.Core.Errors;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Exceptions;

/// <summary>
/// SEAM-23 (design §3.4, §7.3; plan ruling 1): the abstract <see cref="SerdeException"/> roots the serde failures, and
/// the two subtypes are unsealed so codegen and adapters can add more specific ones. Evidence for breaking item 1.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SerdeExceptionHierarchyTests
{
    private sealed class WidgetDecodeException(string message) : DeserializationException(message);

    [Fact]
    public void SerdeException_is_abstract_and_derives_from_SdkException()
    {
        Assert.True(typeof(SerdeException).IsAbstract);
        Assert.True(typeof(SerdeException).IsSubclassOf(typeof(SdkException)));
    }

    [Fact]
    public void SerdeException_constructors_are_all_protected()
    {
        Assert.Empty(typeof(SerdeException).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        var nonPublic = typeof(SerdeException).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Equal(3, nonPublic.Length);
        Assert.All(nonPublic, ctor => Assert.True(ctor.IsFamily));
    }

    [Fact]
    public void SerializationException_and_DeserializationException_derive_from_SerdeException()
    {
        Assert.True(typeof(SerializationException).IsSubclassOf(typeof(SerdeException)));
        Assert.True(typeof(DeserializationException).IsSubclassOf(typeof(SerdeException)));
    }

    [Fact]
    public void Both_subtypes_are_not_sealed()
    {
        // SEAM-23: open for codegen and adapters to add more specific subtypes.
        Assert.False(typeof(SerializationException).IsSealed);
        Assert.False(typeof(DeserializationException).IsSealed);
    }

    [Fact]
    public void A_test_local_sealed_subtype_of_DeserializationException_is_caught_as_SerdeException()
    {
        var thrown = new WidgetDecodeException("boom");

        var caughtAsSerde = false;
        try
        {
            throw thrown;
        }
        catch (SerdeException)
        {
            caughtAsSerde = true;
        }

        var caughtAsSdk = false;
        try
        {
            throw thrown;
        }
        catch (SdkException)
        {
            caughtAsSdk = true;
        }

        Assert.True(caughtAsSerde);
        Assert.True(caughtAsSdk);
    }

    [Fact]
    public void Encode_and_decode_failures_are_distinguishable_under_one_handler()
    {
        static bool IsEncodeFailure(SdkException toThrow)
        {
            try
            {
                throw toThrow;
            }
            catch (SerdeException e) when (e is SerializationException)
            {
                return true;
            }
            catch (SerdeException)
            {
                return false;
            }
        }

        Assert.True(IsEncodeFailure(new SerializationException("encode")));
        Assert.False(IsEncodeFailure(new DeserializationException("decode")));
    }

    [Fact]
    public void The_chained_cause_survives()
    {
        var cause = new InvalidOperationException("cause");

        Assert.Same(cause, new SerializationException("m", cause).InnerException);
        Assert.Same(cause, new DeserializationException("m", cause).InnerException);
    }

    [Fact]
    public void Every_public_constructor_of_the_subtypes_is_unchanged()
    {
        foreach (var type in new[] { typeof(SerializationException), typeof(DeserializationException) })
        {
            var shapes = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Select(ctor => string.Join(",", ctor.GetParameters().Select(p => p.ParameterType.Name)))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(["", "String", "String,Exception"], shapes);
        }
    }
}
