// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Conformance;

/// <summary>
/// What a transport promises about a call made after it was disposed (<c>SEAM-15</c>, a MAY). A declaration, not a hook:
/// a subject declaring <see cref="Unspecified"/> makes <c>seam-15.after-dispose</c> report <see cref="ConformanceStatus.Vacuous"/>.
/// </summary>
public enum AfterDisposeBehavior
{
    /// <summary>The transport makes no promise: what a post-dispose call does is not asserted.</summary>
    Unspecified = 0,

    /// <summary>A call after dispose throws <see cref="ObjectDisposedException"/>, directly or as the returned task's failure.</summary>
    ThrowsObjectDisposedException = 1,
}
