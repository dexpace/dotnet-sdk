// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// A credential object and the secret inside it, for <c>XCUT-19</c>(d): the suite renders the credential the way a log line or
/// a serializer would and requires that the secret does not appear.
/// </summary>
/// <param name="Kind">What the credential is, named in a failure, for example <c>BasicCredential</c>.</param>
/// <param name="Secret">The secret the credential holds; at least eight characters, so a hit is not an accident.</param>
/// <param name="Render">Renders the credential as a log line or a string conversion would.</param>
public sealed record CredentialSample(string Kind, string Secret, Func<string> Render);
