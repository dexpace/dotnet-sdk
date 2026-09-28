// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Tools.Knowledge;

/// <summary>
/// A usage error: a typo in a flag value, a table whose format changed. Exit 2, because a silent
/// empty result is worse than a refusal.
/// </summary>
internal sealed class UsageException : Exception
{
    public UsageException()
    {
    }

    public UsageException(string message)
        : base(message)
    {
    }

    public UsageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The corpus or the requirement index is simply not there. Exit 1: nothing is wrong with the
/// command, this checkout has not been harvested yet.
/// </summary>
internal sealed class NotHarvestedException : Exception
{
    public NotHarvestedException()
    {
    }

    public NotHarvestedException(string message)
        : base(message)
    {
    }

    public NotHarvestedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
