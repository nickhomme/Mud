using Mud.Types;

namespace Mud.Test.ApacheCommons.Interfaces;

[ClassPath("org.apache.commons.math3.stat.descriptive.DescriptiveStatistics")]
public interface IDescriptiveStatistics
{
    [JavaName("getMean")]
    public double GetMean();
    
    [JavaName("getPercentile")]
    public double GetPercentile(double val);
    
    [JavaName("getStandardDeviation")]
    public double GetStandardDeviation();
    
    [JavaName("addValue")]
    public void AddValue(double val);
}