using NUnit.Framework;

namespace Mud.Test.Core;

public class StringBuilder : BaseTest
{


    [TestCase]
    public void StringBuilderCtor()
    {
        var strBldr = Jvm.NewObj("java.lang.StringBuilder", "Foo");
        Assert.AreEqual("Foo", strBldr.Call<string>("toString"));
    }
}