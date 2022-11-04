using Mud.Types;

namespace Mud.Test.Core.Interfaces;

[ClassPath("java.lang.Integer")]
public interface IInteger
{
    [JavaName("compareTo")]
    public int CompareTo([JavaType("java.lang.Integer")] object val);
    [JavaName("compareTo")]
    public int CompareToWithBadType([JavaType("java.lang.String")] object val);

    [JavaStatic("valueOf")]
    public IInteger ValueOf(int val);

    [JavaName("intValue")]
    public int IntValue();
}