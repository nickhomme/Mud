using Xunit;

namespace Mud.Test.ApacheCommons;

[Collection("Serial")]
public class VectorialMeanTest : Math
{
    private double[][] _points = {
        new[] { 1.2, 2.3,  4.5},
        new[] {-0.7, 2.3,  5.0},
        new[] { 3.1, 0.0, -3.1},
        new[] { 6.0, 1.2,  4.2},
        new[] {-0.7, 2.3,  5.0}
    };

    [Fact]
    public void TestSimplistic()
    {
        var stat = Jvm.GetClassInfo("org.apache.commons.math3.stat.descriptive.moment.VectorialMean").Instance(2);
        stat.Call("increment", new double[] {-1.0,  1.0});
        stat.Call("increment", new double[] {1.0, -1.0});
        var mean = stat.Call<double[]>("getResult");
        Assert.Equal(0.0, mean[0], 1.0e-12);
        Assert.Equal(0.0, mean[1], 1.0e-12);
    }
  

}