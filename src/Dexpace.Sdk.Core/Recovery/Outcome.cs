// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Dexpace.Sdk.Core.Http.Response;

namespace Dexpace.Sdk.Core.Recovery;

/// <summary>
/// The closed result of a call attempt: exactly one of <see cref="Success"/> (a response) or <see cref="Failure"/>
/// (an exception) (RECOV-1).
/// </summary>
/// <remarks>
/// An abstract class with a private constructor and two nested sealed classes, not a record: a non-sealed record gets a
/// synthesised <c>protected</c> copy constructor that any record outside the assembly can call, which would open the
/// hierarchy (design §5.2, P4b-3). Equality is by reference. <see cref="ToString"/> names the status or the exception
/// type and never a message.
/// </remarks>
public abstract class Outcome
{
    private Outcome()
    {
    }

    /// <summary>Whether this outcome carries a response.</summary>
    public bool IsSuccess => this is Success;

    /// <summary>Whether this outcome carries an exception.</summary>
    public bool IsFailure => this is Failure;

    /// <summary>Gets the response when this is a <see cref="Success"/>.</summary>
    /// <param name="response">The response, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when this is a success.</returns>
    public bool TryGetResponse([NotNullWhen(true)] out Response? response)
    {
        if (this is Success success)
        {
            response = success.Response;
            return true;
        }

        response = null;
        return false;
    }

    /// <summary>Gets the exception when this is a <see cref="Failure"/>.</summary>
    /// <param name="error">The exception, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when this is a failure.</returns>
    public bool TryGetError([NotNullWhen(true)] out Exception? error)
    {
        if (this is Failure failure)
        {
            error = failure.Error;
            return true;
        }

        error = null;
        return false;
    }

    /// <summary>Folds this outcome: invokes exactly one of the two branches, once.</summary>
    /// <typeparam name="T">The fold's result type.</typeparam>
    /// <param name="onSuccess">Invoked with the response for a success.</param>
    /// <param name="onFailure">Invoked with the exception for a failure.</param>
    /// <returns>The invoked branch's result.</returns>
    /// <exception cref="ArgumentNullException">A delegate is <see langword="null"/>.</exception>
    public T Match<T>(Func<Response, T> onSuccess, Func<Exception, T> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return this switch
        {
            Success success => onSuccess(success.Response),
            Failure failure => onFailure(failure.Error),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Renders the variant and the status or exception type, never an exception message.</summary>
    /// <returns>For example <c>Success(NOT_FOUND(404))</c> or <c>Failure(System.IO.IOException)</c>.</returns>
    public override string ToString() => this switch
    {
        Success success => $"Success({success.Response.Status})",
        Failure failure => $"Failure({failure.Error.GetType().FullName})",
        _ => throw new UnreachableException(),
    };

    /// <summary>The outcome that holds a response.</summary>
    public sealed class Success : Outcome
    {
        /// <summary>Creates a success.</summary>
        /// <param name="response">The response; the outcome does not dispose it.</param>
        /// <exception cref="ArgumentNullException"><paramref name="response"/> is <see langword="null"/>.</exception>
        public Success(Response response)
        {
            ArgumentNullException.ThrowIfNull(response);
            Response = response;
        }

        /// <summary>The response.</summary>
        public Response Response { get; }
    }

    /// <summary>The outcome that holds an exception.</summary>
    public sealed class Failure : Outcome
    {
        /// <summary>Creates a failure.</summary>
        /// <param name="error">The exception.</param>
        /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
        public Failure(Exception error)
        {
            ArgumentNullException.ThrowIfNull(error);
            Error = error;
        }

        /// <summary>The exception.</summary>
        public Exception Error { get; }
    }
}
