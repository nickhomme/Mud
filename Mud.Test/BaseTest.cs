using NUnit.Framework;

namespace Mud.Test;


public abstract class BaseTest : IDisposable
{
    protected List<string> Args { get; set; } = new();
    protected Jvm Jvm { get; private set; } = null!;
    protected BaseTest()
    {
        
    }

    [OneTimeSetUp]
    public virtual Task Setup()
    {
        // Jvm = new Jvm("-Xlog:class+load=info:classloaded.txt");
        Jvm = new Jvm(Args.ToArray());
        return Task.CompletedTask;
    }

    [OneTimeTearDown]
    public virtual void Dispose()
    {
        Jvm.Dispose();
    }
}