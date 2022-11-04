using Mud.Exceptions;

namespace Mud.Types;

public enum JavaType : int
{
    Int = 1,
    Bool,
    Byte,
    Char,
    Short,
    Long,
    Float,
    Double,
    Object,
    Void
}


public record TypedArg(object? Val, CustomType Type)
{
    public TypedArg(object val) : this(val, TypeMap.MapToType(val.GetType(), (val as IBoundObject)?.ClassPath)) {}
    
    public TypedArg(object? val, string classPath) : this(val, new CustomType(classPath)) {}
}

/// <summary>
/// Allows for the use of providing types that do not have a .NET class
/// </summary>
public class CustomType
{
    /// <summary>
    /// Class path to be used
    /// </summary>
    internal string? ClassPath { get; }
    
    /// <summary>
    /// The element type of the array
    /// </summary>
    internal CustomType? ArrayOf { get; }
    
    /// <summary>
    /// Should only be used for when there is an array of primitives
    /// </summary>
    internal JavaType Type { get; } = JavaType.Object;


    public string TypeSignature
    {
        get
        {
            if (ArrayOf != null) return $"[{ArrayOf.TypeSignature}";
            if (ClassPath != null) return $"L{ClassPath};";
            return Type switch
            {
                JavaType.Int => "I",
                JavaType.Bool => "Z",
                JavaType.Byte => "B",
                JavaType.Char => "C",
                JavaType.Short => "S",
                JavaType.Long => "J",
                JavaType.Float => "F",
                JavaType.Double => "D",
                JavaType.Void => "V",
                _ => ""
            };
        }
    }

    public CustomType(CustomType arrayOf)
    {
        ArrayOf = arrayOf;
    }

    /// <summary>
    /// Primitive JavaType to target
    /// </summary>
    /// <param name="type">Primitive type</param>
    /// <exception cref="CustomTypeArgException">Throws if the type provided is Object as object types should always have a class path associated with them</exception>
    public CustomType(JavaType type)
    {
        Type = type;
        if (type is JavaType.Object)
        {
            throw new CustomTypeArgException();
        }
    }

    public CustomType(string classPath)
    {
        ClassPath = classPath.Replace('.', '/');
    }

    public override string ToString()
    {
        return TypeSignature;
    }
}