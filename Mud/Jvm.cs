using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Mud.Types;

namespace Mud;

public class Jvm : IDisposable
{
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_jvm_create_instance")]
    private static extern JvmInstance create_instance(string classPath);
    
    private JvmInstance Instance { get; }
    private Dictionary<string, IntPtr> ClassPaths { get; } = new();

    public Jvm(string classPath)
    {
        Instance = create_instance($"-Djava.class.path={classPath}");
    }

    private object NewObj(Type type, IntPtr jObj, string? classPath = null)
    {
        classPath ??= type.GetCustomAttribute<ClassPathAttribute>()?.ClassPath;
        if (string.IsNullOrWhiteSpace(classPath))
        {
            throw new ArgumentException(
                "Missing class path for new object. Make sure the ClassPath attribute is set and has a value");
        }

        var val = Activator.CreateInstance(type)! as JavaObject;
        val.ClassPath = classPath;

        if (!ClassPaths.TryGetValue(classPath, out var jClass))
        {
            jClass =  JavaObject.get_class(Instance.Env, classPath);
            ClassPaths[classPath] = jClass;
        }
        
        val.Jclass = jClass;
        val.Jobj = jObj;
        val.Env = Instance.Env;
        return val;
    }
    private T NewObj<T>(IntPtr jObj) where T : JavaObject
    {
        return (T) NewObj(typeof(T), jObj);
    }
    public T NewObj<T>() where T : JavaObject
    {
        var classPath = typeof(T).GetCustomAttribute<ClassPathAttribute>()?.ClassPath;
        if (string.IsNullOrWhiteSpace(classPath))
        {
            throw new ArgumentException(
                "Missing class path for new object. Make sure the ClassPath attribute is set and has a value");
        }

        var val = (T) Activator.CreateInstance(typeof(T))!;
        val.ClassPath = classPath;

        if (!ClassPaths.TryGetValue(classPath, out var jClass))
        {
            jClass =  JavaObject.get_class(Instance.Env, classPath);
            ClassPaths[classPath] = jClass;
        }
        
        val.Jclass = jClass;
        val.Jobj = JavaObject.build_obj(Instance.Env, val.Jclass);
        val.Env = Instance.Env;
        return val;
    }
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_string_release")]
    internal static extern void string_release(IntPtr env, IntPtr jString, IntPtr charStr);
    
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_call_static_method")]
    private static extern JavaTypedVal call_static_method(IntPtr env, IntPtr cls, string method, JavaFullType type, IntPtr args);
    
    
    
    private JavaTypedVal CallStatic(string classPath, string method, JavaFullType returnType, params object[] args)
    {
        if (!ClassPaths.TryGetValue(classPath, out var jClass))
        {
            Console.WriteLine(classPath);
            jClass =  JavaObject.get_class(Instance.Env, classPath);
            ClassPaths[classPath] = jClass;
        }
        
        var argsPtr = JavaObject.new_args(args.Length);
        var argList = args.Select(JavaObject.MapToVal).ToList();
        foreach (var arg in argList)
        {
            JavaObject.args_add(argsPtr, arg);
        }

        var result = call_static_method(Instance.Env, jClass, method, returnType, argsPtr);
        JavaObject.interop_free(argsPtr);
        foreach (var strArg in argList.Where(a => a.Typing.ObjectType == JavaObjType.String))
        {
            Marshal.FreeHGlobal(strArg.Val.StringVal.CharPtr);
        }
        return result;
    }

    public TReturn CallStatic<TReturn, TClass>(string method, params object[] args)
    {
        var classPath = typeof(TClass).GetCustomAttribute<ClassPathAttribute>()?.ClassPath;
        if (string.IsNullOrWhiteSpace(classPath))
        {
            throw new ArgumentException(
                "Missing class path for new object. Make sure the ClassPath attribute is set and has a value");
        }
        return CallStatic<TReturn>(classPath, method, args);
    }
    
    public T CallStaticCustomType<T>(string classPath, string method, string returnType, params object[] args)
    {
        var val = JavaObject.MapVal(Instance.Env, CallStatic(classPath, method, new(JavaObjType.Custom)
        {
            CustomType = returnType
        }, args));

        if (val is IntPtr valPtr)
        {
            return (T) NewObj(typeof(T), valPtr, returnType);
        }

        return (T) val;

    }
    
    public T CallStatic<T>(string classPath, string method, params object[] args) => CallStatic<T>(classPath, method, JavaObject.MapToType(typeof(T)), args);
    
    private T CallStatic<T>(string classPath, string method, JavaFullType returnType, params object[] args)
    {
        var val =  JavaObject.MapVal(Instance.Env, CallStatic(classPath, method, returnType, args));
        if (val is IntPtr valPtr)
        {
            return (T) NewObj(typeof(T), valPtr, returnType.CustomType);
        }

        return (T) val;
    }
    
    public void CallStatic(string classPath, string method, params object[] args)
    {
        CallStatic(classPath, method, new JavaFullType(JavaType.Void), args);
    }
    
    
    public void Dispose()
    {
        Instance.Dispose();
    }
}


internal struct JvmInstance : IDisposable
{
    public IntPtr Jvm { get; init; }
    public IntPtr Env { get; init; }
    
    
    [DllImport("Clib/cmake-build-debug/libMud.dylib", CharSet = CharSet.Auto, EntryPoint = "_java_jvm_destroy_instance")]
    private static extern void destroy_instance(IntPtr jvm);
    
    public void Dispose()
    {
        destroy_instance(Jvm);
    }
}