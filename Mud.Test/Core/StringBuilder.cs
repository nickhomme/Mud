using Mud.Test.Core.Interfaces;
using Xunit;

namespace Mud.Test.Core;

[Collection("Serial")]
public class StringBuilder : BaseTest
{


    [Fact]
    public void StringBuilderChainedAppend()
    {
        const string classPath = "java.lang.StringBuilder";
        var strBldr = Jvm.GetClassInfo("java.lang.StringBuilder").Instance("Foo");
        strBldr = strBldr.Call("append", new(classPath), '+')
            .Call("append", new(classPath), "Bar")
            .Call("append", new(classPath), '=');
        strBldr.Call("append", new(classPath), "FooBar");
        
        Assert.Equal("Foo+Bar=FooBar", strBldr.Call<string>("toString"));
    }


    [Fact]
    public void StringBuilderChainedAppendViaInterface()
    {
        var strBldr = ClassInfo<IStringBuilder>.Instance("Foo");
        strBldr.Append('+').Append("Bar").Append('=');
        strBldr.Append("FooBar");
        
        Assert.Equal("Foo+Bar=FooBar", strBldr.ToString());
    }
    
}