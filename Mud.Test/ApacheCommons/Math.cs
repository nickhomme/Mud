using NUnit.Framework;

namespace Mud.Test.ApacheCommons;

public class Math : JarBaseTest
{

    public Math() : base("commons-math3.jar", "https://repo1.maven.org/maven2/org/apache/commons/commons-math3/3.6.1/commons-math3-3.6.1.jar")
    {
    }
    
    
    [TestCase]
    public void DescriptiveStats()
    {
        
        var descriptiveStats = Jvm.NewObj("org.apache.commons.math3.stat.descriptive.DescriptiveStatistics");
        var values = new double[] { 65, 51, 16, 11, 6519, 191, 0, 98, 19854, 1, 32 };
        foreach (var value in values)
        {
            descriptiveStats.Call("addValue", value);
        }


        var mean = descriptiveStats.Call<double>("getMean");
        var median = descriptiveStats.Call<double>("getPercentile", 50d);
        var standardDeviation = descriptiveStats.Call<double>("getStandardDeviation");
        Assert.AreEqual(mean, 2439.8181818181815);
        Assert.AreEqual(median, 51);
        Assert.AreEqual(standardDeviation, 6093.054649651221);
    }

}