using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Mud.Types;

namespace Mud;

public class Jvm : IDisposable
{
    internal JvmInstance Instance { get; }
    internal Dictionary<string, ClassInfo> ClassInfos { get; } = new();


    public Jvm(params string[] args)
    {
        // var classPath = string.Join(':', args.Select(p => p.Trim()).Where(p => p.Length > 0));
        // var classPathArg = classPath.Length > 0 ? $"-Djava.class.path={classPath}" : "";
        var options = MudInterface.gen_options(args.Length, args);
        Instance = MudInterface.create_instance(options, args.Length);
        MudInterface.interop_free(options);
    }


    internal JavaString JavaString(string str)
    {
        
        return MudInterface.java_string(str);
    }

    internal void UsingArgs(IEnumerable<object> args, Action<JavaArg[]> action)
    {
        var jStrings = new List<JavaString>();
        var jArgs = args.Select(a =>
        {
            if (a is string str)
            {
                var strResp = JavaString(str);
                jStrings.Add(strResp);
                a = strResp.JavaPtr;
            }
            return JavaArg.MapFrom(a);
        }).ToArray();

        try
        {
            action(jArgs);
        }
        finally
        {
            try
            {
                foreach (var javaString in jStrings)
                {
                    MudInterface.string_release(Instance.Env, javaString.JavaPtr, javaString.CharArrPtr);
                }       
            }
            catch
            {
                // ignored
            }
        }
    }


    internal string ExtractStr(IntPtr jString, bool releaseStrObj = false)
    {
        [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_jstring_to_string")]
        static extern IntPtr jstring_to_string(IntPtr env, IntPtr jStr);
        var mallocStr = jstring_to_string(Instance.Env, jString);
        var str = Marshal.PtrToStringAuto(mallocStr)!;
        MudInterface.interop_free(mallocStr);
        if (releaseStrObj)
        {
            JavaObject.release_obj(Instance.Env, jString);
        }
        return str;
    } 

    private IntPtr ExceptionCls { get; set; }
    private IntPtr GetExceptionCauseMethod { get; set; }
    private IntPtr GetExceptionStackMethod { get; set; }
    private IntPtr ExceptionToStringMethod { get; set; }
    private IntPtr FrameCls { get; set; }
    private IntPtr FrameToStringMethod { get; set; }
    private string GetException(IntPtr ex)
    {
        if (ExceptionCls == IntPtr.Zero)
        {
            ExceptionCls = JavaObject.get_class(Instance.Env, "java/lang/Throwable");
            FrameCls = JavaObject.get_class(Instance.Env, "java/lang/StackTraceElement");

            GetExceptionCauseMethod = JavaObject.get_method(Instance.Env, ExceptionCls, "getCause", "()Ljava/lang/Throwable;");
        
            GetExceptionStackMethod = JavaObject.get_method(Instance.Env, ExceptionCls, "getStackTrace", "()Ljava/lang/StackTraceElement;");
        
            ExceptionToStringMethod = JavaObject.get_method(Instance.Env, ExceptionCls, "toString", "()Ljava/lang/String;");
            
            FrameToStringMethod = JavaObject.get_method(Instance.Env, FrameCls, "toString", "()Ljava/lang/String;");
        }

        var mallocStr = MudInterface.get_exception_msg(Instance.Env, ex, GetExceptionCauseMethod, GetExceptionStackMethod, ExceptionToStringMethod,
            FrameToStringMethod);
        var str = Marshal.PtrToStringAuto(mallocStr)!;
        MudInterface.interop_free(mallocStr);
        return str;
    }
    
    internal T UsingArgs<T>(IEnumerable<object> args, Func<JavaArg[], JavaCallResp> action)
    {
        JavaCallResp resp = new();
        UsingArgs(args, (jArgs) =>
        {
            resp = action(jArgs);
        });
        if (resp.IsException)
        {
            var exceptionMsg = GetException(resp.Value.Object);
            JavaObject.release_obj(Instance.Env, resp.Value.Object);
            throw new JavaException(exceptionMsg);
        }

        var type = JavaObject.MapToType(typeof(T));
        
        if (type.Type is JavaType.Object)
        {
            var cls = JavaObject.get_class_of_obj(Instance.Env, resp.Value.Object);
            var method = JavaObject.get_method(Instance.Env, cls, "getName", "()Ljava/lang/String;");
            var nameCallResp = JavaObject.call_method(Instance.Env, method, resp.Value.Object, JavaType.Object);
            var classPath = ExtractStr(nameCallResp.Value.Object, true);

            if (!ClassInfos.TryGetValue(classPath, out var info))
            {
                info = new ClassInfo(this, cls, classPath);
                ClassInfos[classPath] = info;
            }
            
            var jObjVal = (JavaObject) Activator.CreateInstance(typeof(T))!;
            jObjVal.Info = info;
            jObjVal.Env = Instance.Env;
            jObjVal.Jvm = this;
            jObjVal.SetObj(resp.Value.Object);
            return (T) (object) jObjVal;
        }

        object? val = type.Type switch
        {
            JavaType.Int => resp.Value.Int,
            JavaType.Bool => resp.Value.Bool == 1,
            JavaType.Byte => resp.Value.Byte,
            JavaType.Char => (char)resp.Value.Char,
            JavaType.Short => resp.Value.Short,
            JavaType.Long => resp.Value.Long,
            JavaType.Float => resp.Value.Float,
            JavaType.Double => resp.Value.Double,
            _ => null
        };

        return (T)val!;
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

    private object NewObj(Type type, string? classPath, params object[] args)
    {
        classPath ??= type.GetCustomAttribute<ClassPathAttribute>()?.ClassPath;
        if (string.IsNullOrWhiteSpace(classPath))
        {
            throw new ArgumentException(
                "Missing class path for new object. Make sure the ClassPath attribute is set and has a value");
        }

        var val = (Activator.CreateInstance(type)! as JavaObject)!;

        classPath = classPath.Replace(".", "/");
        if (!ClassInfos.TryGetValue(classPath, out var info))
        {
            var cls =  JavaObject.get_class(Instance.Env, classPath);
            info = new ClassInfo(this, cls, classPath);
            ClassInfos[classPath] = info;
        }
        
        val.Info = info;
        val.Env = Instance.Env;
        val.Jvm = this;
        
        val.Bind(args);
        return val;
    }
    private T NewObj<T>(params object[] args) where T : JavaObject
    {
        return (T) NewObj(typeof(T), null, args);
    }

    public JavaObject NewObj(string classPath, params object[] args) => NewObj<JavaObject>(classPath, args);
    
    public T NewObj<T>(string classPath, params object[] args) where T : JavaObject
    {
        return (T) NewObj(typeof(T), classPath, args);
    }

    public T NewObj<T>() where T : JavaObject =>
        NewObj<T>(typeof(T).GetCustomAttribute<ClassPathAttribute>()?.ClassPath!);
    
    public void Dispose()
    {
        Instance.Dispose();
    }
    
}


public class ClassInfo
{
    internal Jvm Jvm { get; }
    internal IntPtr Cls { get; }
    public string ClassPath { get; }
    internal Dictionary<string, IntPtr> Methods { get; } = new();
    internal Dictionary<string, IntPtr> Props { get; } = new();

    public ClassInfo(Jvm jvm, IntPtr cls, string classPath)
    {
        Jvm = jvm;
        Cls = cls;
        ClassPath = classPath;
    }

    internal IntPtr GetMethod(Type? returnType, string method, params object[] args)
    {
        if (!Methods.TryGetValue(method, out var methodPtr))
        {
            string sig;
            if (returnType == null)
            {
                sig = $"({string.Join("", args.Select(JavaObject.GenSignature))})V";
            }
            else
            {
                sig = JavaObject.GenMethodSignature(returnType, args);
            }
            methodPtr = MudInterface.get_method(Jvm.Instance.Env, Cls, method, sig);
            Methods[method] = methodPtr;
        }

        return methodPtr;
    }
    internal IntPtr GetMethod<T>(string method, params object[] args)
    {
        return GetMethod(typeof(T), method, args);
    }
    
    internal IntPtr GetVoidMethod(string method, params object[] args)
    {
        return GetMethod(null, method, args);
    }


    public T CallMethod<T>(IntPtr obj, string method, params object[] args)
    {
        return Jvm.UsingArgs<T>(args, jArgs => MudInterface.call_method(Jvm.Instance.Env, obj, GetMethod<T>(method, args), jArgs, JavaObject.MapToType(typeof(T)).Type));
    }
    
    public void CallMethod(IntPtr obj, string method, params object[] args)
    {
        Jvm.UsingArgs(args, jArgs => MudInterface.call_method(Jvm.Instance.Env, obj, GetVoidMethod(method, args), jArgs, JavaType.Void));
    }
    
    public T CallStaticMethod<T>(string method, params object[] args)
    {
        return Jvm.UsingArgs<T>(args, jArgs => MudInterface.call_static_method(Jvm.Instance.Env, Cls, GetMethod<T>(method, args), jArgs, JavaObject.MapToType(typeof(T)).Type));
    }
    
    public void CallStaticMethod(string method, params object[] args)
    {
        Jvm.UsingArgs(args, jArgs => MudInterface.call_static_method(Jvm.Instance.Env, Cls, GetVoidMethod(method, args), jArgs, JavaType.Void));
    }
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


internal static class MudInterface
{
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_jvm_create_instance")]
    internal static extern JvmInstance create_instance(IntPtr options, int optionsAmnt);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_jvm_options_str_arr", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr gen_options(int amnt, string[] options);
    
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "interop_free")]
    internal static extern void interop_free(IntPtr ptr);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_get_exception_msg")]
    internal static extern IntPtr get_exception_msg(IntPtr env, IntPtr ex, IntPtr getCauseMethod, IntPtr getStackMethod, IntPtr exToStringMethod, IntPtr frameToStringMethod);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_string_new")]
    internal static extern JavaString java_string(string str);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "_java_string_release")]
    internal static extern void string_release(IntPtr env, IntPtr jString, IntPtr charStr);
 
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_method")]
    internal static extern IntPtr get_method(IntPtr env, IntPtr cls, string methodName, string signature);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_static_method")]
    internal static extern JavaCallResp call_static_method(IntPtr env, IntPtr cls, IntPtr method, JavaArg[] args, JavaType type);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_method")]
    internal static extern JavaCallResp call_method(IntPtr env, IntPtr cls, IntPtr method, JavaArg[] args, JavaType type);
}
