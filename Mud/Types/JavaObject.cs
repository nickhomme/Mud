using System.Data;
using System.Dynamic;
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
    internal string ClassPath { get; set; }
    internal IntPtr Env { get; set; }
    internal IntPtr Jobj { get; set; }
    internal IntPtr Jclass { get; set; }


    public JavaObject() {}
    
    internal JavaObject(IntPtr env, string classPath, IntPtr jClass, IntPtr jObj)
    {
        Env = env;
        ClassPath = classPath;
        Jobj = jObj;
        Jclass = jClass;
    }
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_build_class_object")]
    internal static extern IntPtr build_obj_by_path(IntPtr env, string classPath);
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_build_object")]
    internal static extern IntPtr build_obj(IntPtr env, IntPtr jClass);
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_get_class")]
    internal static extern IntPtr get_class(IntPtr env, string classPath);
    
    private Dictionary<string, IntPtr> PropFields { get; } = new();
    
    internal JavaObject(IntPtr env, string classPath, IntPtr jClass)
    {
        Env = env;
        ClassPath = classPath;
        Jclass = jClass;
        Jobj = build_obj(env, jClass);
    }
    
    internal JavaObject(IntPtr env, string classPath)
    {
        Env = env;
        ClassPath = classPath;
        Jclass = get_class(env, classPath);
        Jobj = build_obj(env, Jclass);
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
        if (type == typeof(double))
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
        if (type == typeof(string))
        {
            return new(JavaObjType.String);
        }
        
        if (type.IsAssignableTo(typeof(JavaObject)))
        {
            return new(JavaObjType.Custom)
            {
                CustomType = type.GetCustomAttribute<ClassPathAttribute>()!.ClassPath
            };
        }
        if (type == typeof(IntPtr))
        {
            return new(JavaObjType.Class);
        }

        throw new ConstraintException($"Cannot determine java type for class: {type.FullName ?? type.Name} ");
    }
    
    internal static JavaTypedVal MapToVal(object? val)
    {
        var javaVal = new JavaVal();
        JavaType type = 0;
        JavaObjType objType = 0;
        string? customType = null;
        switch (val)
        {
            case decimal v:
                javaVal = new JavaVal(v);
                type = JavaType.Double;
                break;
            case double v:
                javaVal = new JavaVal(v);
                type = JavaType.Double;
                break;
            case string v:
                javaVal = new JavaVal(v);
                type = JavaType.Object;
                objType = JavaObjType.String;
                break;
            case int v:
                javaVal = new JavaVal(v);
                type = JavaType.Int;
                break;
            case byte v:
                javaVal = new JavaVal(v);
                type = JavaType.Byte;
                break;
            case bool v:
                javaVal = new JavaVal(v ? byte.One : byte.Zero);
                type = JavaType.Bool;
                break;
            case char v:
                javaVal = new JavaVal(v);
                type = JavaType.Char;
                break;
            case long v:
                javaVal = new JavaVal(v);
                type = JavaType.Long;
                break;
            case short v:
                javaVal = new JavaVal(v);
                type = JavaType.Short;
                break;
            case float v:
                javaVal = new JavaVal(v);
                type = JavaType.Float;
                break;
            case JavaObject v:
                javaVal = new JavaVal(v.Jobj);
                type = JavaType.Object;
                objType = JavaObjType.Custom;
                customType = v.ClassPath;
                break;
            case IntPtr v:
                javaVal = new JavaVal(v);
                type = JavaType.Object;
                objType = JavaObjType.Class;
                break;
            case JavaTypedVal v:
                return v;
            case Enum v:
                javaVal = new JavaVal(Convert.ToInt32(v));
                type = JavaType.Int;
                break;
            default:
                throw new ConstraintException($"Cannot determine java type for val: {val?.GetType()?.Name ?? "null"} ");
        }


        return new JavaTypedVal
        {
            Typing = new JavaFullType(type, objType)
            {
                CustomType = customType
            },
            Val = javaVal
        };
    }
    
    internal static object MapVal(IntPtr env, JavaTypedVal typedVal)
    {
        object val;
        switch (typedVal.Typing.Type)
        {
            case JavaType.Int:
                val = typedVal.Val.IntVal;
                break;
            case JavaType.Bool:
                val = typedVal.Val.BoolVal == 1;
                break;
            case JavaType.Byte:
                val = typedVal.Val.ByteVal;
                break;
            case JavaType.Char:
                val = typedVal.Val.CharVal;
                break;
            case JavaType.Short:
                val = typedVal.Val.ShortVal;
                break;
            case JavaType.Long:
                val = typedVal.Val.LongVal;
                break;
            case JavaType.Float:
                val = typedVal.Val.FloatVal;
                break;
            case JavaType.Double:
                val = typedVal.Val.DoubleVal;
                break;
            case JavaType.Object:
                switch (typedVal.Typing.ObjectType)
                {
                    case JavaObjType.String:
                        val = typedVal.Val.StringVal.Val!;
                        Jvm.string_release(env, typedVal.Val.StringVal.JavaString, typedVal.Val.StringVal.CharPtr);
                        break;
                    case JavaObjType.Throwable:
                        throw new Exception(typedVal.Val.StringVal.Val);
                    case JavaObjType.Class:
                    case JavaObjType.Custom:
                        val = typedVal.Val.ObjVal;
                        break;
                    case JavaObjType.Array:
                    default:
                        throw new NotImplementedException($"ObjectType: {typedVal.Typing.ObjectType} not mapped");
                }
                break;
            case JavaType.Void:
                throw new InvalidCastException("Property has void value");
            default:
                throw new NotImplementedException($"JavaType: {typedVal.Typing.Type} not mapped");
        }

        return val;
    }
    
    
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_call_method")]
    private static extern JavaTypedVal call_method(IntPtr env, IntPtr obj, string method, JavaFullType type, IntPtr args);
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "interop_free")]
    internal static extern void interop_free(IntPtr ptr);
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_args_new_ptr")]
    internal static extern IntPtr new_args(int argAmnt);
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_args_add")]
    internal static extern void args_add(IntPtr args, JavaTypedVal arg);

    private JavaTypedVal Call(string method, JavaFullType returnType, params object[] args)
    {
        var argsPtr = new_args(args.Length);
        
        var argList = args.Select(JavaObject.MapToVal).ToList();
        foreach (var arg in argList)
        {
            args_add(argsPtr, arg);
        }

        var result = call_method(Env, Jobj, method, returnType, argsPtr);
        interop_free(argsPtr);
        
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
        return (T) MapVal(Env, Call(method, MapToType(typeof(T)), args));
    }
    
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_get_field_id_by_class")]
    private static extern IntPtr get_prop_field(IntPtr env, IntPtr cls, string name, JavaFullType type);
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_get_object_property")]
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

    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_release_object")]
    private static extern void release_obj(IntPtr env, IntPtr obj);
    public void Dispose()
    {
        if (Env == null)
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

