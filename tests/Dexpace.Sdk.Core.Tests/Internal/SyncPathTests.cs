// Copyright (c) 2026 dexpace and Omar Aljarrah.
// Licensed under the MIT License. See LICENSE in the repository root for details.

using Dexpace.Sdk.Core.Internal;
using Xunit;

namespace Dexpace.Sdk.Core.Tests.Internal;

[Trait("Category", "Unit")]
public sealed class SyncPathTests
{

    private static ValueTask<int> SyncThrowing(bool async)
    {
        return Core(async);

        static async ValueTask<int> Core(bool async)
        {
            if (!async)
            {
                throw new InvalidOperationException("original");
            }

            await Task.Yield();
            return 1;
        }
    }

    [Fact]
    public void A_completed_ValueTask_returns_its_result()
    {
        Assert.Equal(42, SyncPath.GetResult(new ValueTask<int>(42)));
    }

    [Fact]
    public void A_completed_ValueTask_of_a_faulted_async_method_rethrows_the_original_exception()
    {
        var task = SyncThrowing(async: false);
        Assert.True(task.IsCompleted);

        var thrown = Assert.Throws<InvalidOperationException>(() => SyncPath.GetResult(task));

        Assert.Equal("original", thrown.Message);
    }

    [Fact]
    public void A_non_completed_ValueTask_throws_InvalidOperationException()
    {
        var source = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thrown = Assert.Throws<InvalidOperationException>(() => SyncPath.GetResult(new ValueTask<int>(source.Task)));

        Assert.Contains("defect", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_non_generic_overload_behaves_the_same()
    {
        SyncPath.GetResult(ValueTask.CompletedTask);

        var faulted = ValueTask.FromException(new InvalidOperationException("original"));
        Assert.Equal("original", Assert.Throws<InvalidOperationException>(() => SyncPath.GetResult(faulted)).Message);

        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.Throws<InvalidOperationException>(() => SyncPath.GetResult(new ValueTask(source.Task)));
    }
}
