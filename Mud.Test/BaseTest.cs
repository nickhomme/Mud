
using Xunit;

namespace Mud.Test;

[Collection("Serial")]
public abstract class BaseTest : IAsyncLifetime
{
    protected BaseTest()
    {
        if (!Jvm.IsInitialized)
        {
            Jvm.Initialize();
        }
    }

    private static int TaskAmnt;
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        // Jvm.Destroy();
        return Task.CompletedTask;
    }
}