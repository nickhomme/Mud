using Mud.Types;

namespace Mud.Test.Core.Interfaces;

[ClassPath("java.lang.Math")]
public interface IMath
{
    [JavaStatic("PI")]
    public double Pi { get; }

    [JavaStatic]
    public double sin(double val);
    
    [JavaStatic("cos")]
    public double Cos(double val);
    
    public double tan(double val);
}