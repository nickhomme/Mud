using System.Runtime.InteropServices;

namespace Mud.Types;

internal struct JavaTypedVal
{
    public JavaFullType Typing { get; init; }
    public JavaVal Val { get; init; }
}


internal struct JavaFullType
{
    public JavaType Type { get; init; } = 0;
    public JavaObjType ObjectType { get; init; } = 0;
    public string? CustomType { get; init; } = null;

    public JavaFullType(JavaType type)
    {
        Type = type;
    }

    public JavaFullType(JavaObjType objType)
    {
        Type = JavaType.Object;
        ObjectType = objType;
    }
    
    public JavaFullType(JavaType type, JavaObjType objType)
    {
        Type = type;
        ObjectType = objType;
    }
    
}


internal struct JavaVal
{
    public byte BoolVal { get; init; } = default;
    public byte ByteVal { get; init; } = default;
    public char CharVal { get; init; } = default;
    public int IntVal { get; init; } = default;
    public short ShortVal { get; init; } = default;
    public long LongVal { get; init; } = default;
    public float FloatVal { get; init; } = default;
    public double DoubleVal { get; init; } = default;
    public JString StringVal { get; init; } = default;
    public IntPtr ObjVal { get; init; } = default;
    public IntPtr ObjArrayVal { get; init; } = default;

    public JavaVal(){}
    public JavaVal(byte boolVal) : this()
    {
        BoolVal = boolVal;
        ByteVal = boolVal;
    }

    public JavaVal(char charVal) : this()
    {
        CharVal = charVal;
    }

    public JavaVal(int intVal) : this()
    {
        IntVal = intVal;
    }

    public JavaVal(short shortVal) : this()
    {
        ShortVal = shortVal;
    }

    public JavaVal(long longVal) : this()
    {
        LongVal = longVal;
    }

    public JavaVal(float floatVal) : this()
    {
        FloatVal = floatVal;
    }

    public JavaVal(double doubleVal) : this()
    {
        DoubleVal = doubleVal;
    }
    
    public JavaVal(decimal doubleVal) : this()
    {
        DoubleVal = Convert.ToDouble(doubleVal);
    }

    public JavaVal(string stringVal) : this()
    {
        StringVal = new(stringVal) {};
    }

    public JavaVal(IntPtr objVal) : this()
    {
        ObjVal = objVal;
        ObjArrayVal = objVal;
    }
}

public struct JString
{
    public IntPtr JavaString { get; init; }
    public IntPtr CharPtr { get; init; }

    public string? Val
    {
        get => Marshal.PtrToStringAuto(CharPtr);
        init => CharPtr = Marshal.StringToHGlobalAnsi(value);
    }

    public JString(string val) : this()
    {
        Val = val;
    }
}