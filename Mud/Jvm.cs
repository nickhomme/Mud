using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Mud.Exceptions;
using Mud.Types;

namespace Mud;

public class Jvm : IDisposable
{
    internal JvmInstance Instance { get; }
    internal Dictionary<string, ClassInfo> ClassInfos { get; } = new();


    public Jvm(params string[] args)
    {
        var options = MudInterface.gen_options(args.Length, args);
        Instance = MudInterface.create_instance(options, args.Length);
        MudInterface.interop_free(options);
    }


    public ClassInfo GetClass(string classPath)
    {
        classPath = classPath.Replace(".", "/");
        if (!ClassInfos.TryGetValue(classPath, out var info))
        {
            var cls = MudInterface.get_class(Instance.Env, classPath);
            // Console.WriteLine($"GetClass `{classPath}`::{cls.HexAddress()}");
            info = new ClassInfo(this, cls, classPath);
            ClassInfos[classPath] = info;
        }

        return info;
    }

    internal JavaString JavaString(string str)
    {
        return MudInterface.string_new(Instance.Env, str);
    }

    internal void UsingArgs(IEnumerable<object> args, Action<JavaVal[]> action)
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
            return JavaVal.MapFrom(a);
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
            MudInterface.release_obj(Instance.Env, jString);
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
            ExceptionCls = MudInterface.get_class(Instance.Env, "java/lang/Throwable");
            FrameCls = MudInterface.get_class(Instance.Env, "java/lang/StackTraceElement");

            GetExceptionCauseMethod = MudInterface.get_method(Instance.Env, ExceptionCls, "getCause", "()Ljava/lang/Throwable;");
        
            GetExceptionStackMethod = MudInterface.get_method(Instance.Env, ExceptionCls, "getStackTrace", "()Ljava/lang/StackTraceElement;");
        
            ExceptionToStringMethod = MudInterface.get_method(Instance.Env, ExceptionCls, "toString", "()Ljava/lang/String;");
            
            FrameToStringMethod = MudInterface.get_method(Instance.Env, FrameCls, "toString", "()Ljava/lang/String;");
        }

        var mallocStr = MudInterface.get_exception_msg(Instance.Env, ex, GetExceptionCauseMethod, GetExceptionStackMethod, ExceptionToStringMethod,
            FrameToStringMethod, true);
        var str = Marshal.PtrToStringAuto(mallocStr)!;
        MudInterface.interop_free(mallocStr);
        return str;
    }
    
    internal T UsingArgs<T>(IEnumerable<object> args, Func<JavaVal[], JavaCallResp> action)
    {
        JavaCallResp resp = new();
        UsingArgs(args, (jArgs) =>
        {
            resp = action(jArgs);
        });
        
        if (resp.IsException)
        {
            var exceptionMsg = GetException(resp.Value.Object);
            MudInterface.release_obj(Instance.Env, resp.Value.Object);
            throw new JavaException(exceptionMsg);
        }

        return MapJValue<T>(resp.Value);
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
    
    private ClassInfo GetObjClass(IntPtr obj)
    {
        var classInfo = GetClass("java/lang/Class"); 
        // var getNameMethod = classInfo.GetMethodPtrBySig("getName", "()Ljava/lang/String;", false);
        var cls = MudInterface.get_class_of_obj(Instance.Env, obj);
        var classPath = classInfo.CallMethod<string>(cls, "getName");
        return GetClass(classPath);

    }

    internal object MapJValue(JavaFullType type, Type valType, JavaVal javaVal)
    {
        if (type.Type is JavaType.Object)
        {
            if (javaVal.Object == IntPtr.Zero)
            {
                return default!;
            }
            if (valType == typeof(string))
            {
                return ExtractStr(javaVal.Object);
            }
            
            var objCls = GetObjClass(javaVal.Object);

            if (valType.IsArray)
            {
                var length = MudInterface.array_length(Instance.Env, javaVal.Object);
                var arr = Array.CreateInstance(valType.GetElementType()!, length);
                var arrElemType = JavaObject.MapToType(valType.GetElementType()!);
                for (var i = 0; i < length; i++)
                {
                    arr.SetValue(MapJValue(valType.GetElementType()!, MudInterface.array_get_at(Instance.Env, javaVal.Object, i, arrElemType.Type)), i); 
                }
                return arr;
            }
            
            var jObjVal = (JavaObject) Activator.CreateInstance(valType)!;
            jObjVal.Info = objCls;
            jObjVal.Env = Instance.Env;
            jObjVal.Jvm = this;
            jObjVal.SetObj(javaVal.Object);
            return jObjVal;
        }

        object? val = type.Type switch
        {
            JavaType.Int => javaVal.Int,
            JavaType.Bool => javaVal.Bool,
            JavaType.Byte => javaVal.Byte,
            JavaType.Char => (char)javaVal.Char,
            JavaType.Short => javaVal.Short,
            JavaType.Long => javaVal.Long,
            JavaType.Float => javaVal.Float,
            JavaType.Double => javaVal.Double,
            _ => null
        };

        return val!;
    }
    internal object MapJValue(Type valType, JavaVal javaVal)
    {
        return MapJValue(JavaObject.MapToType(valType), valType, javaVal);
    }

    internal T MapJValue<T>(JavaFullType type, JavaVal javaVal)
    {
        return (T) MapJValue(type, typeof(T), javaVal);
    }
    internal T MapJValue<T>(JavaVal javaVal)
    {
        return (T) MapJValue(typeof(T), javaVal);
    } 

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
            var cls =  MudInterface.get_class(Instance.Env, classPath);
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


internal struct JvmInstance : IDisposable
{
    public IntPtr Jvm { get; init; }
    public IntPtr Env { get; init; }
    
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_jvm_destroy_instance")]
    private static extern void destroy_instance(IntPtr jvm);
    
    public void Dispose()
    {
        destroy_instance(Jvm);
    }
}


internal static class MudInterface
{
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_jvm_create_instance")]
    internal static extern JvmInstance create_instance(IntPtr options, int optionsAmnt);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_jvm_options_str_arr", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr gen_options(int amnt, string[] options);
    
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "interop_free")]
    internal static extern void interop_free(IntPtr ptr);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_exception_msg")]
    internal static extern IntPtr get_exception_msg(IntPtr env, IntPtr ex, IntPtr getCauseMethod, IntPtr getStackMethod, IntPtr exToStringMethod, IntPtr frameToStringMethod, bool isTop);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_string_new")]
    internal static extern JavaString string_new(IntPtr env, string str);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_string_release")]
    internal static extern void string_release(IntPtr env, IntPtr jString, IntPtr charStr);
 
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_method")]
    internal static extern IntPtr get_method(IntPtr env, IntPtr cls, string methodName, string signature);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_static_method", CallingConvention = CallingConvention.Cdecl)]
    internal static extern JavaCallResp call_static_method(IntPtr env, IntPtr cls, IntPtr method, JavaType type, JavaVal[] args);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_method", CallingConvention = CallingConvention.Cdecl)]
    internal static extern JavaCallResp call_method(IntPtr env, IntPtr obj, IntPtr method, JavaType type, JavaVal[] args);
    
    
    
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_build_class_object")]
    internal static extern IntPtr build_obj_by_path(IntPtr env, string classPath);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_new_object")]
    internal static extern IntPtr new_obj(IntPtr env, IntPtr jClass, string sig, [In, Out] JavaVal[] args);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_class")]
    internal static extern IntPtr get_class(IntPtr env, string classPath);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_class_of_obj")]
    internal static extern IntPtr get_class_of_obj(IntPtr env, IntPtr obj);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_static_method")]
    internal static extern IntPtr get_static_method(IntPtr env, IntPtr cls, string methodName, string signature);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_method_by_name")]
    internal static extern JavaCallResp call_method(IntPtr env, IntPtr obj, string method, string signature, JavaType type, JavaVal[] args);
    

    
    internal static JavaCallResp call_method(IntPtr env, IntPtr obj, IntPtr method, JavaType type) =>
        call_method(env, obj, method, type, Array.Empty<JavaVal>());
    
    internal static JavaCallResp call_method(IntPtr env, IntPtr cls, string method, string signature, JavaType type) =>
        call_method(env, cls, method, signature, type, Array.Empty<JavaVal>());
    

    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_call_static_method_by_name")]
    internal static extern JavaCallResp call_static_method(IntPtr env, IntPtr cls, string method, string signature, JavaType type, JavaVal[] args);

    internal static JavaCallResp call_static_method(IntPtr env, IntPtr cls, string method, string signature, JavaType type) =>
        call_static_method(env, cls, method, signature, type, Array.Empty<JavaVal>());
    
    
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_field_id")]
    internal static extern IntPtr get_field(IntPtr env, IntPtr cls, string name, string signature);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_field_value")]
    internal static extern JavaVal get_field_value(IntPtr env, IntPtr obj, IntPtr field, JavaType type);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_get_static_field_value")]
    internal static extern JavaVal get_static_field_value(IntPtr env, IntPtr cls, IntPtr field, JavaType type);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_release_object")]
    internal static extern void release_obj(IntPtr env, IntPtr obj);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_instance_of")]
    internal static extern bool instance_of(IntPtr env, IntPtr obj, IntPtr cls);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_array_length")]
    internal static extern int array_length(IntPtr env, IntPtr obj);
    
    [DllImport("libMud", CharSet = CharSet.Auto, EntryPoint = "mud_array_get_at")]
    internal static extern JavaVal array_get_at(IntPtr env, IntPtr arr, int index, JavaType type);
}


static class IntPtrExtensions
{
    public static string HexAddress(this IntPtr ptr) => $"0x{ptr:X}".ToLower();
}