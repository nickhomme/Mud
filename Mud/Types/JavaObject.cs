using System.Data;
using System.Dynamic;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Mud.Types;


[AttributeUsage(AttributeTargets.Class|AttributeTargets.Interface, AllowMultiple = true)]
public class ClassPathAttribute : Attribute
{
    public readonly string ClassPath;

    public ClassPathAttribute(string classPath)
    {
        ClassPath = classPath.Replace(".", "/");
    }
}

[AttributeUsage(AttributeTargets.Method|AttributeTargets.Property|AttributeTargets.Constructor)]
public class TypeSignatureAttribute : Attribute
{
    public readonly string Signature;

    public TypeSignatureAttribute(string signature)
    {
        Signature = signature.Replace(".", "/");
    }
}

[ClassPath("java.lang.Object")]
public class JavaObject : DynamicObject, IDisposable, IJavaObject
{
    internal IntPtr Env { get; set; }
    internal IntPtr Jobj { get; private set; }
    public string JavaObjAddress => Jobj.HexAddress();
    public string ClassPath => Info.ClassPath;
    public ClassInfo Info { get; internal set; } = null!;

    public override string ToString()
    {
        return $"{Info?.ClassPath}::{JavaObjAddress}";
    }

    public bool Equals(JavaObject? obj)
    {
        return obj?.Jobj == Jobj;
    }

    internal void SetObj(IntPtr obj)
    {
        Jobj = obj;
    }

    public bool InstanceOf(string classPath)
    {
        return MudInterface.instance_of(Env, Jobj, Jvm.GetClassInfo(classPath).Cls);
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
        obj.Info ??= Jvm.GetClassInfo(obj.GetType().GetCustomAttributes<ClassPathAttribute>().First().ClassPath);
        return InstanceOf(obj.Info);
    }


    public void BindBySig(string signature, params object[] args)
    {
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        Info ??= Jvm.GetClassInfo(GetType().GetCustomAttributes<ClassPathAttribute>().First()!.ClassPath);
#pragma warning disable CS8073
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (Jobj != null) return;
#pragma warning restore CS8073
        Jvm.UsingArgs(args, jArgs =>
        {
            Jobj = MudInterface.new_obj(Jvm.Instance.Env, Info.Cls, signature, jArgs);
        });
    }

    public void Bind(params object[] args) => BindBySig(GenMethodSignature((Type?)null, args), args);
    
    public static string GenSignature(JavaFullType val)
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

    public static string GenMethodSignature(JavaFullType? returnType, params JavaFullType[] args)
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
                CustomType = type.GetCustomAttributes<ClassPathAttribute>().First().ClassPath
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

    public JavaObject Call(string method, JavaFullType returnType, params object[] args) =>
        Call<JavaObject>(method, returnType, args); 
    public T Call<T>(string method, JavaFullType returnType, params object[] args)
    {
        return Info.CallMethodNamedReturn<T>(Jobj, method, returnType, args);
    }
    
    public T CallBySig<T>(string method, string signature, params object[] args)
    {
        return Info.CallMethodBySig<T>(Jobj, method, signature, args);
    }
    
    public void CallBySig(string method, string signature, params object[] args)
    {
        Info.CallMethodBySig(Jobj, method, signature, args);
    }

    public JavaObject GetProp(string prop, JavaFullType? type = null) => GetProp<JavaObject>(prop, type);
    public T GetProp<T>(string prop, JavaFullType? type = null)
    {
        return Info.GetField<T>(Jobj, prop, type);
    }

    public void SetProp(string prop, JavaObject value, JavaFullType? type = null)
    {
        Info.SetField(Jobj, prop, value, type);
    } 
    public void SetProp<T>(string prop, T value, JavaFullType? type = null) 
    {
        Info.SetField(Jobj, prop, value, type);
    }

    
    public void Dispose()
    {
        if (Env == IntPtr.Zero)
        {
            return;
        }

        Jvm.ReleaseObj(this);
    }
}


[ClassPath("java.lang.Object")]
public interface IJavaObject : IDisposable
{

    string ToString();

    public bool InstanceOf(string classPath);

    public bool InstanceOf(ClassInfo cls);

    public bool InstanceOf(JavaObject obj);

    public void BindBySig(string signature, params object[] args);

    public void Bind(params object[] args);
    
    public static string GenSignature(JavaFullType val)
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


    public void Call(string method, params object[] args);
    public T Call<T>(string method, params object[] args);

    public JavaObject Call(string method, JavaFullType returnType, params object[] args) =>
        Call<JavaObject>(method, returnType, args);

    public T Call<T>(string method, JavaFullType returnType, params object[] args);

    public T CallBySig<T>(string method, string signature, params object[] args);

    public void CallBySig(string method, string signature, params object[] args);

    public JavaObject GetProp(string prop, JavaFullType? type = null) => GetProp<JavaObject>(prop, type);
    public T GetProp<T>(string prop, JavaFullType? type = null);

    public void SetProp(string prop, JavaObject value, JavaFullType? type = null);
    public void SetProp<T>(string prop, T value, JavaFullType? type = null);

}