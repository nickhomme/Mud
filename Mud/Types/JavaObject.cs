using System.Data;
using System.Dynamic;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Mud.Types;


[AttributeUsage(AttributeTargets.Class)]
public class ClassPathAttribute : Attribute
{
    public readonly string ClassPath;

    public ClassPathAttribute(string classPath)
    {
        ClassPath = classPath.Replace(".", "/");
    }
}

// [ClassPath("Java.Lang.Object")]
public class JavaObject : DynamicObject, IDisposable
{
    internal IntPtr Env { get; set; }
    internal IntPtr Jobj { get; private set; }
    internal Jvm Jvm { get; set; }
    internal ClassInfo Info { get; set; }

    internal void SetObj(IntPtr obj)
    {
        Jobj = obj;
    }
    public void Bind(params object[] args)
    {
        Jvm.UsingArgs(args, jArgs =>
        {
            Jobj = MudInterface.build_obj(Jvm.Instance.Jvm, Info.Cls, GenMethodSignature(null, args), jArgs);
        });
    }
    
    internal static string GenSignature(JavaFullType val)
    {
        switch (val.Type)
        {
            case JavaType.Int:
                return "I";
            case JavaType.Bool:
                return "Z";
            case JavaType.Byte:
                return "B";
            case JavaType.Char:
                return "C";
            case JavaType.Short:
                return "S";
            case JavaType.Long:
                return "L";
            case JavaType.Float:
                return "F";
            case JavaType.Double:
                return "D";
            case JavaType.Object:
                if (val.ArrayType != null)
                {
                    return $"[{GenSignature(val.ArrayType)}";
                }
                return $"L{val.CustomType!}";
            case JavaType.Void:
                return "V";
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    internal static string GenSignature(Type type) => GenSignature(MapToType(type));

    internal static string GenSignature(object val) => GenSignature(val.GetType());

    internal static string GenMethodSignature(Type? returnType, params Type[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){(returnType == null ? "V" : MapToType(returnType))}";
    }
    internal static string GenMethodSignature(Type? returnType, params object[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){(returnType == null ? "V" : MapToType(returnType))}";
    }
    
    
    internal static JavaFullType MapToType(Type type)
    {
        if (type == typeof(int))
        {
            return new(JavaType.Int);
        }
        if (type == typeof(byte))
        {
            return new(JavaType.Byte);
        }
        if (type == typeof(bool))
        {
            return new(JavaType.Bool);
        }
        if (type == typeof(float))
        {
            return new(JavaType.Float);
        }
        if (type == typeof(decimal))
        {
            return new(JavaType.Double);
        }
        if (type == typeof(double) || type == typeof(decimal))
        {
            return new(JavaType.Double);
        }
        if (type == typeof(short))
        {
            return new(JavaType.Short);
        }
        if (type == typeof(long))
        {
            return new(JavaType.Long);
        }
        
        if (type == typeof(string) || type.IsAssignableTo(typeof(JavaObject)) || type == typeof(IntPtr))
        {
            return new(JavaType.Object);
        }

        throw new ConstraintException($"Cannot determine java type for class: {type.FullName ?? type.Name} ");
    }
    
    
    

    public void Call(string method, params object[] args)
    {
        Info.CallMethod(Jobj, method, args);
    }
    public T Call<T>(string method, params object[] args)
    {
        return Info.CallMethod<T>(Jobj, method, args);
    }


    public T GetProp<T>(string prop) => Info.GetField<T>(Jobj, prop);

    
    public void Dispose()
    {
        if (Env == IntPtr.Zero)
        {
            return;
        }
        MudInterface.release_obj(Env, Jobj);
    }
}

