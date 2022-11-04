using Mud.Types;
using Xunit;

namespace Mud.Test.ApacheCommons;

[Collection("Serial")]
public class ComplexTest : Math
{
 
    [Fact]
    public void TestAbs() {
        var z = Jvm.GetClassInfo("org.apache.commons.math3.complex.Complex").Instance(3.0, 4.0);
        Assert.Equal(5.0, z.Call<double>("abs"), 1.0e-5);
    }

    [Fact]
    public void TestAbsNaN()
    {
        var nanField = Jvm.GetClassInfo("org.apache.commons.math3.complex.Complex")
            .GetField("NaN", new CustomType("org.apache.commons.math3.complex.Complex"));
        var dblCls = Jvm.GetClassInfo("java.lang.Double");
        
        Assert.True(dblCls.Call<bool>("isNaN", nanField.Call<double>("abs")));
        var z = Jvm.GetClassInfo("org.apache.commons.math3.complex.Complex").Instance( 
            dblCls.GetField<double>("POSITIVE_INFINITY"), dblCls.GetField<double>("NaN"));
        Assert.True(dblCls.Call<bool>("isNaN", z.Call<double>("abs")));
    }
    
}