using Mud.Types;

namespace Mud.Test.Core.Interfaces;

[ClassPath("java.lang.StringBuilder")]
public interface IStringBuilder
{
    [JavaName("append")]
    public IStringBuilder Append(string str);
    
    [JavaName("append")]
    [JavaType("java.lang.StringBuilder")]
    public IBoundObject AppendWithGenericResp(string str);
    
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