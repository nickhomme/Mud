using System.Data;
using System.Runtime.InteropServices;

namespace Mud.Types;

/// <summary>
/// The mud clib will provide this struct for any called methods, includes the info on whether the value is an exception or void
/// </summary>
public struct JavaCallResp
{
    [MarshalAs(UnmanagedType.U1)]
    public bool IsVoid;
    [MarshalAs(UnmanagedType.U1)]
    public bool IsException;
    public JavaVal Value;
}


/// <summary>
/// Mathes the layout and size of the JNI's jvalue union
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct JavaVal
{
    [FieldOffset(0)]
    [MarshalAs(UnmanagedType.U1)]
    public bool Bool;
    [FieldOffset(0)]
    public byte Byte;
    [FieldOffset(0)]
    public ushort Char;
    [FieldOffset(0)]
    public short Short;
    [FieldOffset(0)]
    public int Int;
    [FieldOffset(0)]
    public long Long;
    [FieldOffset(0)]
    public float Float;
    [FieldOffset(0)]
    public double Double;
    [FieldOffset(0)]
    public IntPtr Object;


    public static JavaVal MapFrom(object? val)
    {
        var arg = new JavaVal();
        switch (val)
        {
            case decimal v:
                arg.Double = Convert.ToDouble(v);
                break;
            case double v:
                arg.Double = v;
                break;
            case int v:
                arg.Int = v;
                break;
            case byte v:
                arg.Byte = v;
                break;
            case bool v:
                arg.Bool = v;
                break;
            case char v:
                arg.Char = v;
                break;
            case long v:
                arg.Long = v;
                break;
            case short v:
                arg.Short = v;
                break;
            case float v:
                arg.Float = v;
                break;
            case IBoundObject v:
                arg.Object = v.Jobj;
                break;
            case IntPtr v:
                arg.Object = v;
                break;
            case Enum v:
                arg.Int = Convert.ToInt32(v);
                break;
            case null:
                arg.Object = IntPtr.Zero;
                break;
            default:
                throw new ConstraintException($"Cannot determine java type for val: {val.GetType().Name} ");
        }

        return arg;
    }

    public override string ToString()
    {
        return $".z: {Bool}; .b: {Byte}; .c: '{Convert.ToChar(Char)}'; .s: {Short}; .i: {Int}; .j: {Long}; .f: {Float}; .d: {Double}; .l: {Object.HexAddress()}";
    }
}
