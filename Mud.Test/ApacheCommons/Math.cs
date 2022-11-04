using Mud.Test.ApacheCommons.Interfaces;
using Mud.Types;
using Xunit;

namespace Mud.Test.ApacheCommons;

[Collection("Serial")]
public class Math : JarBaseTest
{

    public Math() : base("commons-math3.jar", "https://repo1.maven.org/maven2/org/apache/commons/commons-math3/3.6.1/commons-math3-3.6.1.jar")
    {
    }
    
    
    [Fact]
    public void DescriptiveStats()
    {
        var descriptiveStats = Jvm.GetClassInfo("org.apache.commons.math3.stat.descriptive.DescriptiveStatistics").Instance();
        var values = new double[] { 65, 51, 16, 11, 6519, 191, 0, 98, 19854, 1, 32 };
        foreach (var value in values)
        {
            descriptiveStats.Call("addValue", value);
        }


        var mean = descriptiveStats.Call<double>("getMean");
        var median = descriptiveStats.Call<double>("getPercentile", 50d);
        var standardDeviation = descriptiveStats.Call<double>("getStandardDeviation");
        Assert.Equal(2439.8181818181815, mean);
        Assert.Equal(51, median);
        Assert.Equal(6093.054649651221, standardDeviation);
    }
    
    [Fact]
    public void DescriptiveStatsViaInterface()
    {
        var descriptiveStats = ClassInfo<IDescriptiveStatistics>.Instance();
        
        var values = new double[] { 65, 51, 16, 11, 6519, 191, 0, 98, 19854, 1, 32 };
        foreach (var value in values)
        {
            descriptiveStats.AddValue(value);
        }


        var mean = descriptiveStats.GetMean();
        var median = descriptiveStats.GetPercentile(50);
        var standardDeviation = descriptiveStats.GetStandardDeviation();
        Assert.Equal(2439.8181818181815, mean);
        Assert.Equal(51, median);
        Assert.Equal(6093.054649651221, standardDeviation);
    }

}