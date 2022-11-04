using System.IO.Compression;
using Mud.Types;

namespace Mud.Playground;

[ClassPath("java.lang.StringBuilder")]
public interface IStringBuilder
{
    [JavaName("append")]
    [JavaType("java.lang.StringBuilder")]
    public IBoundObject Append(string str);
    
    [JavaName("append")]
    public IStringBuilder Append(char[] chars);
    
    [JavaName("append")]
    public IStringBuilder Append(bool bol);
    
    [JavaName("append")]
    public IStringBuilder Append(char val);
    
    [JavaType("java.lang.StringBuilder")]
    public IBoundObject Append(int val);
    
    [JavaName("append")]
    public IStringBuilder AppendCharSequence([JavaType("java.lang.CharSequence")] IBoundObject val);
    
    [JavaName("append")]
    public IStringBuilder AppendStringBuffer([JavaType("java.lang.StringBuffer")] IBoundObject val);

    [JavaName("toString")]
    public string ToString();

    [JavaName("trimToSize")]
    public void TrimToSize();
}

[ClassPath("java.lang.Thread")]
public interface IThread
{
    [JavaStatic("sleep")]
    void Sleep(long amnt);

}

[ClassPath("java.lang.Math")]
public interface IMath
{
    [JavaStatic("PI")]
    public double Pi { get; }

    [JavaStatic]
    public double sin(double val);
    
    [JavaStatic("cos")]
    public double Cos(double val);
    
    [JavaStatic("tan")]
    public double Tan(double val);
}

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