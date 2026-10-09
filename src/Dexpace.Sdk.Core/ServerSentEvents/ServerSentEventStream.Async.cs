// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

namespace Dexpace.Sdk.Core.ServerSentEvents;

public sealed partial class ServerSentEventStream
{
    /// <inheritdoc />
    public IAsyncEnumerator<ServerSentEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
