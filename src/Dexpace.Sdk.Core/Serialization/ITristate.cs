// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.Serialization;

/// <summary>
/// The non-generic face of every <see cref="Tristate{T}"/>: the state, and a double-dispatch door to the closed type
/// (P7a-6). This is the codec-adapter hook.
/// </summary>
/// <remarks>
/// A codec adapter that must serialize a <see cref="Tristate{T}"/> it only knows by <see cref="Type"/> boxes (or
/// creates) an instance, casts to <see cref="ITristate"/>, and calls <see cref="Accept{TResult}"/> with a visitor whose
/// <see cref="ITristateVisitor{TResult}.Visit{T}"/> receives the closed type as a generic argument. No reflection over
/// generic types is needed, so the path is trim- and NativeAOT-safe. A future Newtonsoft adapter uses the same door.
/// </remarks>
public interface ITristate
{
    /// <summary>Gets the state of the value.</summary>
    TristateState State { get; }

    /// <summary>Hands the closed <see cref="Tristate{T}"/> to <paramref name="visitor"/> and returns its result.</summary>
    /// <typeparam name="TResult">What the visit produces.</typeparam>
    /// <param name="visitor">The visitor.</param>
    /// <returns>The visitor's result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="visitor"/> is <see langword="null"/>.</exception>
    TResult Accept<TResult>(ITristateVisitor<TResult> visitor);
}
