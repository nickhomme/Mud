using Xunit;

namespace Mud.Test.ApacheCommons;

[Collection("Serial")]
public class SkewnessTest : Math
{
    [Fact]
    public void TestNaN() {
        var skew = Jvm.GetClassInfo("org.apache.commons.math3.stat.descriptive.moment.Skewness").Instance();
        var doubleCls = Jvm.GetClassInfo("java.lang.Double");
        Assert.True(doubleCls.Call<bool>("isNaN", skew.Call<double>("getResult")));
        skew.Call("increment", 1d);
        Assert.True(doubleCls.Call<bool>("isNaN", skew.Call<double>("getResult")));
        skew.Call("increment", 1d);
        Assert.True(doubleCls.Call<bool>("isNaN", skew.Call<double>("getResult")));
        skew.Call("increment", 1d);
        Assert.False(doubleCls.Call<bool>("isNaN", skew.Call<double>("getResult")));
    }
}