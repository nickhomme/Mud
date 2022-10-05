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

[ClassPath("java.lang.Object")]
public class JavaObject : DynamicObject, IDisposable
{
    internal IntPtr Env { get; set; }
    internal IntPtr Jobj { get; private set; }
    public string JavaObjAddress => Jobj.HexAddress();
    public string ClassPath => Info.ClassPath;
    internal Jvm Jvm { get; set; }
    internal ClassInfo Info { get; set; }

    public override string ToString()
    {
        return $"{Info?.ClassPath}::{JavaObjAddress}";
    }

    internal void SetObj(IntPtr obj)
    {
        Jobj = obj;
    }

    public bool InstanceOf(string classPath)
    {
        return MudInterface.instance_of(Env, Jobj, Jvm.GetClass(classPath).Cls);
    }
    
    public bool InstanceOf(ClassInfo cls)
    {
        return MudInterface.instance_of(Env, Jobj, cls.Cls);
    }
    
    internal bool InstanceOf(IntPtr cls)
    {
        return MudInterface.instance_of(Env, Jobj, cls);
    }
    
    public bool InstanceOf(JavaObject obj)
    {
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        obj.Info ??= Jvm.GetClass(obj.GetType().GetCustomAttribute<ClassPathAttribute>()!.ClassPath);
        return InstanceOf(obj.Info);
    }
    
    
    internal void Bind(params object[] args)
    {
        Jvm.UsingArgs(args, jArgs =>
        {
            Jobj = MudInterface.new_obj(Jvm.Instance.Env, Info.Cls, GenMethodSignature((Type?) null, args), jArgs);
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
                return $"L{val.CustomType!};";
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
        return $"({string.Join("", args.Select(GenSignature))}){(returnType == null ? "V" : GenSignature(returnType))}";
    }

    internal static string GenMethodSignature(JavaFullType? returnType, params object[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){(returnType == null ? "V" : GenSignature(returnType))}";
    }
    internal static string GenMethodSignature(Type? returnType, params object[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){(returnType == null ? "V" : GenSignature(returnType))}";
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


        if (type.IsArray)
        {
            return new JavaFullType(JavaType.Object)
            {
                ArrayType = MapToType(type.GetElementType()!)
            };
        }
        
        if (type == typeof(string))
        {
            return new JavaFullType(JavaType.Object)
            {
                CustomType = "java/lang/String"
            };
        }
        if (type.IsAssignableTo(typeof(JavaObject)))
        {
            return new(JavaType.Object)
            {
                CustomType = type.GetCustomAttribute<ClassPathAttribute>()?.ClassPath
            };
        }
        if (type == typeof(IntPtr))
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

    public JavaObject CallNamedReturn(string method, JavaFullType returnType, params object[] args) =>
        CallNamedReturn<JavaObject>(method, returnType, args); 
    public T CallNamedReturn<T>(string method, JavaFullType returnType, params object[] args)
    {
        return Info.CallMethodNamedReturn<T>(Jobj, method, returnType, args);
    }


    public JavaObject GetProp(string prop, JavaFullType? type = null) => GetProp<JavaObject>(prop, type);
    public T GetProp<T>(string prop, JavaFullType? type = null)
    {
        return Info.GetField<T>(Jobj, prop, type);
    }

    
    public void Dispose()
    {
        if (Env == IntPtr.Zero)
        {
            return;
        }
        MudInterface.release_obj(Env, Jobj);
    }
}

