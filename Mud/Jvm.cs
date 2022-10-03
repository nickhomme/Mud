using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Mud.Types;

namespace Mud;

public class Jvm : IDisposable
{
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_jvm_create_instance")]
    private static extern JvmInstance create_instance(IntPtr options, int optionsAmnt);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_add_class_path")]
    private static extern void add_class_path(IntPtr env, string path);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_jvm_options_str_arr", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr gen_options(int amnt, string[] options);
    
    private JvmInstance Instance { get; }
    private Dictionary<string, IntPtr> ClassPaths { get; } = new();

    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "interop_free")]
    internal static extern void interop_free(IntPtr ptr);
    
    public static string? LibPath { get; set; }
    
    public Jvm(params string[] args)
    {
        // var classPath = string.Join(':', args.Select(p => p.Trim()).Where(p => p.Length > 0));
        // var classPathArg = classPath.Length > 0 ? $"-Djava.class.path={classPath}" : "";
        var options = gen_options(args.Length, args);
        Instance = create_instance(options, args.Length);
        interop_free(options);
    }

    // public void AddClassPath(string path)
    // {
    //     if (string.IsNullOrWhiteSpace(path)) return;
    //     
    //     path = path.Trim();
    //     if (!path.StartsWith("file://"))
    //     {
    //         path = $"file://{Path.GetFullPath(path)}";
    //     }
    //     
    //     add_class_path(Instance.Env, path);
    // }

    private object NewObj(Type type, IntPtr jObj, string? classPath, params object[] args)
    {
        classPath ??= type.GetCustomAttribute<ClassPathAttribute>()?.ClassPath;
        if (string.IsNullOrWhiteSpace(classPath))
        {
            throw new ArgumentException(
                "Missing class path for new object. Make sure the ClassPath attribute is set and has a value");
        }

        classPath = classPath.Replace(".", "/");

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
    private T NewObj<T>(IntPtr jObj, params object[] args) where T : JavaObject
    {
        return (T) NewObj(typeof(T), jObj, null, args);
    }

    public JavaObject NewObj(string classPath, params object[] args) => NewObj<JavaObject>(classPath, args);

    public T NewObj<T>(string classPath, params object[] args) where T : JavaObject
    {
        if (string.IsNullOrWhiteSpace(classPath))
        {
            throw new ArgumentException(
                "Missing class path for new object. Make sure the ClassPath attribute is set and has a value");
        }

        var val = (T) Activator.CreateInstance(typeof(T))!;
        classPath = classPath.Replace(".", "/");
        // Console.WriteLine($"CLS: {classPath}");
        val.ClassPath = classPath; 

        if (!ClassPaths.TryGetValue(classPath, out var jClass))
        {
            jClass =  JavaObject.get_class(Instance.Env, classPath);
            ClassPaths[classPath] = jClass;
        }
        
        var argsPtr = JavaObject.new_args(args.Length);
        
        var argList = args.Select(JavaObject.MapToVal).ToList();
        foreach (var arg in argList)
        {
            JavaObject.args_add(argsPtr, arg);
        }
        
        val.Jclass = jClass;
        val.Jobj = JavaObject.build_obj(Instance.Env, val.Jclass, argsPtr);
        interop_free(argsPtr);
        val.Env = Instance.Env;
        return val;
    }

    public T NewObj<T>() where T : JavaObject =>
        NewObj<T>(typeof(T).GetCustomAttribute<ClassPathAttribute>()?.ClassPath!);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_string_release")]
    internal static extern void string_release(IntPtr env, IntPtr jString, IntPtr charStr);
    
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_call_static_method")]
    private static extern JavaTypedVal call_static_method(IntPtr env, IntPtr cls, string method, JavaFullType type, IntPtr args);
    
    private JavaTypedVal CallStatic(string classPath, string method, JavaFullType returnType, params object[] args)
    {
        classPath = classPath.Replace(".", "/");
        if (!ClassPaths.TryGetValue(classPath, out var jClass))
        {
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
        Jvm.interop_free(argsPtr);
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

    public JavaObject CallStaticReturnType(string classPath, string method, string returnType, params object[] args) =>
        CallStaticReturnType<JavaObject>(classPath, method, returnType, args);
    public T CallStaticReturnType<T>(string classPath, string method, string returnType, params object[] args) where T : JavaObject
    {
        var val = JavaObject.MapVal(Instance.Env, CallStatic(classPath, method, new(JavaObjType.Custom)
        {
            CustomType = returnType.Replace(".", "/")
        }, args));

        if (val is IntPtr valPtr)
        {
            return (T) NewObj(typeof(T), valPtr, returnType);
        }

        return (T) val;

    }
    
    public object CallStatic(Type type, string classPath, string method, params object[] args) => CallStatic(type, classPath, method, JavaObject.MapToType(type), args);
    
    public T CallStatic<T>(string classPath, string method, params object[] args) => CallStatic<T>(classPath, method, JavaObject.MapToType(typeof(T)), args);
    
    private object CallStatic(Type type, string classPath, string method, JavaFullType returnType, params object[] args)
    {
        var val =  JavaObject.MapVal(Instance.Env, CallStatic(classPath, method, returnType, args));
        if (val is IntPtr valPtr)
        {
            return NewObj(type, valPtr, returnType.CustomType);
        }

        return val;
    }
    
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
    
    // private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    // {
    //     // if not loading libmud then fall back
    //     if (libraryName != "libMud")
    //     {
    //         return IntPtr.Zero;
    //     }
    //
    //     var os = Environment.OSVersion;
    //     if (os.Platform == PlatformID.Unix)
    //     {
    //         libraryName 
    //     }
    //     
    //     var basePath = Path.GetFullPath(LibPath ?? Directory.GetCurrentDirectory());
    //     var libPath = Path.Join(basePath, libraryName);
    //     // On systems with AVX2 support, load a different library.
    //     if (System.Runtime.Intrinsics.X86.Avx2.IsSupported)
    //     {
    //         return NativeLibrary.Load( $"{Path.GetFullPath()}""nativedep_avx2", assembly, searchPath);
    //     }
    // }
}


internal struct JvmInstance : IDisposable
{
    public IntPtr Jvm { get; init; }
    public IntPtr Env { get; init; }
    
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_jvm_destroy_instance")]
    private static extern void destroy_instance(IntPtr jvm);
    
    public void Dispose()
    {
        destroy_instance(Jvm);
    }
}