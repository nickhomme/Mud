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
    private IntPtr Jobj { get; set; }
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
            Jobj = build_obj(Jvm.Instance.Jvm, Info.Cls, jArgs);
        });
    }
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_build_class_object")]
    internal static extern IntPtr build_obj_by_path(IntPtr env, string classPath);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_build_object")]
    internal static extern IntPtr build_obj(IntPtr env, IntPtr jClass, JavaArg[] args);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_class")]
    internal static extern IntPtr get_class(IntPtr env, string classPath);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_class_of_obj")]
    internal static extern IntPtr get_class_of_obj(IntPtr env, IntPtr obj);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_method")]
    internal static extern IntPtr get_method(IntPtr env, IntPtr cls, string methodName, string signature);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_static_method")]
    internal static extern IntPtr get_static_method(IntPtr env, IntPtr cls, string methodName, string signature);
    
    private Dictionary<string, IntPtr> PropFields { get; } = new();
    
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

    internal static string GenMethodSignature(Type returnType, params Type[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){MapToType(returnType)}";
    }
    internal static string GenMethodSignature(Type returnType, params object[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){MapToType(returnType)}";
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
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_method_by_name")]
    internal static extern JavaCallResp call_method(IntPtr env, IntPtr obj, string method, string signature, [In, Out] JavaArg[] args, JavaType type);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_method")]
    internal static extern JavaCallResp call_method(IntPtr env, IntPtr obj, IntPtr method, [In, Out] JavaArg[] args, JavaType type);
    
    internal static JavaCallResp call_method(IntPtr env, IntPtr obj, IntPtr method, JavaType type) =>
        call_method(env, obj, method, Array.Empty<JavaArg>(), type);
    
    internal static JavaCallResp call_method(IntPtr env, IntPtr cls, string method, string signature, JavaType type) =>
        call_method(env, cls, method, signature, Array.Empty<JavaArg>(), type);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_static_method")]
    internal static extern JavaCallResp call_static_method(IntPtr env, IntPtr cls, IntPtr method, [In, Out] JavaArg[] args, JavaType type);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_static_method_by_name")]
    internal static extern JavaCallResp call_static_method(IntPtr env, IntPtr cls, string method, string signature, [In, Out] JavaArg[] args, JavaType type);

    internal static JavaCallResp call_static_method(IntPtr env, IntPtr cls, string method, string signature, JavaType type) =>
        call_static_method(env, cls, method, signature, Array.Empty<JavaArg>(), type);
    

    private JavaTypedVal Call(string method, JavaFullType returnType, params object[] args)
    {
        Jvm.UsingArgs<>()
        
        
        var argsPtr = new_args(args.Length);
        
        var argList = args.Select(JavaObject.MapToVal).ToList();
        foreach (var arg in argList)
        {
            args_add(argsPtr, arg);
        }

        var result = call_method(Env, Jobj, method, returnType, argsPtr);
        Jvm.interop_free(argsPtr);
        
        foreach (var strArg in argList.Where(a => a.Typing.ObjectType == JavaObjType.String))
        {
            Marshal.FreeHGlobal(strArg.Val.StringVal.CharPtr);
        }

        return result;
    }
    public void Call(string method, params object[] args)
    {
        Call(method, new JavaFullType(JavaType.Void), args);
    }
    public T Call<T>(string method, params object[] args)
    {
        
        Jvm
        Jvm.UsingArgs<T>()
        
        return (T) MapVal(Env, Call(method, MapToType(typeof(T)), args));
    }
    
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_get_field_id_by_class")]
    private static extern IntPtr get_prop_field(IntPtr env, IntPtr cls, string name, JavaFullType type);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_get_object_property")]
    private static extern JavaTypedVal get_prop(IntPtr env, IntPtr obj, IntPtr field, JavaFullType type);

    private T GetProp<T>(string prop, JavaFullType type)
    {
        if (!PropFields.TryGetValue(prop, out var fieldId))
        {
            fieldId = get_prop_field(Env, Jclass, prop, type);
            PropFields[prop] = fieldId;
        }
        var typedVal = get_prop(Env, Jobj, fieldId, type);

        var val = (T)MapVal(Env, typedVal);
        return val;
    }

    public T GetProp<T>(string prop, JavaType type) =>
        GetProp<T>(prop, new JavaFullType(type));

    public T GetProp<T>(string prop, JavaObjType type) =>
        GetProp<T>(prop, new JavaFullType(type));

    public T GetProp<T>(string prop) =>
        GetProp<T>(prop, MapToType(typeof(T)));

    public T GetProp<T>(string prop, string customType) =>
        GetProp<T>(prop, new JavaFullType(JavaObjType.Custom) { CustomType = customType });
    
    //
    // public object this[string key]
    // {
    //     get
    //     {
    //         
    //     }
    //     set => Set(key, value);
    // }

    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_release_object")]
    internal static extern void release_obj(IntPtr env, IntPtr obj);
    public void Dispose()
    {
        if (Env == IntPtr.Zero)
        {
            return;
        }
        release_obj(Env, Jclass);
        release_obj(Env, Jobj);
        foreach (var propFieldsValue in PropFields.Values)
        {
            release_obj(Env, propFieldsValue);
        }
    }
}

