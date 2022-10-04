using System.Data;
using System.Runtime.InteropServices;

namespace Mud.Types;

internal struct JavaCallResp
{
    public bool IsVoid;
    public bool IsException;
    public JavaArg Value;
}


[StructLayout(LayoutKind.Explicit)]
internal struct JavaArg
{
    [FieldOffset(0)]
    public byte Bool;
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


    public static JavaArg MapFrom(object val)
    {
        var arg = new JavaArg();
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
#if !NET7_0_OR_GREATER
                arg.Bool = v ? byte.One : byte.Zero);
#else
                arg.Bool = (byte)(v ? 1 : 0);
#endif
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
            case JavaObject v:
                arg.Object = v.Jobj;
                break;
            case IntPtr v:
                arg.Object = v;
                break;
            case Enum v:
                arg.Int = Convert.ToInt32(v);
                break;
            default:
                throw new ConstraintException($"Cannot determine java type for val: {val?.GetType()?.Name ?? "null"} ");
        }

        return arg;
    }
}



internal class JavaFullType
{
    public JavaType Type { get; init; } = 0;
    // public JavaObjType ObjectType { get; init; } = 0;
    public string? CustomType { get; init; } = null;
    public JavaFullType? ArrayType { get; init; } = null;

    public JavaFullType(JavaType type)
    {
        Type = type;
    }

    // public JavaFullType(JavaObjType objType)
    // {
    //     Type = JavaType.Object;
    //     ObjectType = objType;
    // }
    //
    // public JavaFullType(JavaType type, JavaObjType objType)
    // {
    //     Type = type;
    //     ObjectType = objType;
    // }
}

// internal struct JArray
// {
//     public int Size { get; init; }
//     public IntPtr JavaArray { get; init; }
//
//     public T GetAt<T>(int index)
//     {
//         
//     }
//
//     public JArray(string val) : this()
//     {
//         Val = val;
//     }
// }