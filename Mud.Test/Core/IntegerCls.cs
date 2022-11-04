
using Mud.Test.Core.Interfaces;
using Xunit;

namespace Mud.Test.Core;

[Collection("Serial")]
public class IntegerCls : BaseTest
{


    [Fact]
    public void BuildIntegerViaCtor()
    {
        var integer = Jvm.GetClassInfo("java.lang.Integer").Instance(10);
        var intVal = integer.Call<int>("intValue");
        Assert.Equal(10, intVal);
    }

    [Fact]
    public void BuildIntegerViaValueOf()
    {
        var integer = ClassInfo<IInteger>.Static.ValueOf(10);
        var intVal = integer.IntValue();
        Assert.Equal(10, intVal);
    }
    
}