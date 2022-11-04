using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Mud.Exceptions;
using Mud.Types;

namespace Mud;

public static class Jvm
{
    internal static JvmInstance Instance { get; private set; }
    internal static Dictionary<string, ClassInfo> ClassInfos { get; } = new();
    internal static List<IntPtr> ObjPointers { get; } = new();
    
    public static bool IsInitialized => Instance.Env != IntPtr.Zero;

    /// <summary>
    /// Load the JVM with the provided arguments and will append rt.jar to the class path if it is not provided
    /// </summary>
    /// <param name="javaHome">Java installation directory</param>
    /// <param name="args">Desired JVM arguments</param>
    /// <exception cref="JavaLocateException">Will throw if the provided JAVA_HOME directory does not exist</exception>
    public static void Initialize(DirectoryInfo javaHome, params string[] args)
    {
        args = args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
        if (!javaHome.Exists)
        {
            throw new JavaLocateException($"Path ({javaHome}) provided by JAVA_HOME environment does not exist");
        }
        javaHome = javaHome.GetDirectories("jre").FirstOrDefault() ?? javaHome;
        var libDir = new DirectoryInfo(Path.Join(javaHome.FullName, "lib"));
        var binDir = new DirectoryInfo(Path.Join(javaHome.FullName, "bin"));

        var rtJar = new FileInfo(Path.Join(libDir.FullName, "rt.jar"));
        var classPathArgIndex = Array.FindIndex(args, a => a.StartsWith("-Djava.class.path="));
        if (classPathArgIndex != -1)
        {
            var classPathArg = args[classPathArgIndex];
            if (!Regex.IsMatch(classPathArg, @"(?<=[\\\/]|^)rt\.jar(?=;|:|(?:\s?)+$)"))
            {
                var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
                args[classPathArgIndex] = $"{classPathArg}{separator}{rtJar.FullName}";     
            }
        }
        else
        {
            args = args.Concat(new[] { $"-Djava.class.path={rtJar.FullName}" }).ToArray();
        }
        
        // Console.WriteLine(javaHome);
        var options = MudInterface.gen_options_arr(args.Length, args);
        Instance = MudInterface.create_instance(options, args.Length);
        MudInterface.interop_free(options);
    }
    
    /// <summary>
    /// Will locate java home via environment variable JAVA_HOME and forward the call to Initialize(DirectoryInfo, params string[] args)
    /// </summary>
    /// <param name="args">Desired JVM arguments</param>
    /// <exception cref="JavaLocateException">Will throw the environment variable JAVA_HOME is not set</exception>
    public static void Initialize(params string[] args)
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrWhiteSpace(javaHome))
        {
            throw new JavaLocateException("JAVA_HOME environment variable not set");
        }

        Initialize(new DirectoryInfo(javaHome), args);
    }

    /// <summary>
    /// Makes sure the JVM has been inizialized 
    /// </summary>
    /// <exception cref="JvmNotInitializedException"></exception>
    internal static void EnsureInit()
    {
        if (Instance.Jvm == IntPtr.Zero)
        {
            throw new JvmNotInitializedException();
        }
    }

    public static bool TryGetClassInfo(string classPath, [NotNullWhen(true)] out ClassInfo? classInfo)
    {
        EnsureInit();
        classPath = classPath.Replace(".", "/");
        if (!ClassInfos.TryGetValue(classPath, out classInfo))
        {
            var cls = MudInterface.get_class(Instance.Env, classPath);

            if (cls == IntPtr.Zero)
            {
                classInfo = null;
                return false;
            }
            
            ObjPointers.Add(cls);
            // Console.WriteLine($"GetClass `{classPath}`::{cls.HexAddress()}");
            classInfo = new ClassInfo(cls, classPath);
            ClassInfos[classPath] = classInfo;
        }
        return true;
    }

    public static ClassInfo GetClassInfo<T>()
    {
        var classPath = typeof(T).GetCustomAttributes<ClassPathAttribute>().FirstOrDefault()?.ClassPath;
        return GetClassInfo(classPath ?? typeof(T).FullName!.Split('`')[0]);
    }
    
    /// <exception cref="Mud.Exceptions.ClassNotFoundException"></exception>
    public static ClassInfo GetClassInfo(string classPath)
    {
        if (TryGetClassInfo(classPath, out var cls))
        {
            return cls;
        }

        throw new ClassNotFoundException(classPath);
    }

    /// <summary>
    /// Creates a new String in the JVM
    /// </summary>
    /// <param name="str">String to be allocated</param>
    /// <returns>A JavaString struct containing the pointer to the java string as well as the malloc C string </returns>
    internal static IntPtr JavaString(string str)
    {
        return MudInterface.string_new(Instance.Env, str);
    }

    internal static string ExtractStr(IBoundObject jString, bool releaseStrObj = false)
    {
        return ExtractStr(jString.Jobj, releaseStrObj);
    }
    
    /// <summary>
    /// Extracts the provided backing string from the JVM 
    /// </summary>
    /// <param name="jString">The java string pointer</param>
    /// <param name="releaseStrObj">Should the Java string be released as well</param>
    /// <returns></returns>
    internal static string ExtractStr(IntPtr jString, bool releaseStrObj = false)
    {
        [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_jstring_to_string")]
        static extern IntPtr jstring_to_string(IntPtr env, IntPtr jStr);
        
        var mallocStr = jstring_to_string(Instance.Env, jString);
        var str = Marshal.PtrToStringAnsi(mallocStr)!;
        MudInterface.interop_free(mallocStr);
        if (releaseStrObj)
        {
            MudInterface.release_obj(Instance.Env, jString);
        }

        return str;
    }

    private static IntPtr ExceptionCls { get; set; }
    private static IntPtr GetExceptionCauseMethod { get; set; }
    private static IntPtr GetExceptionStackMethod { get; set; }
    private static IntPtr ExceptionToStringMethod { get; set; }
    private static IntPtr FrameCls { get; set; }
    private static IntPtr FrameToStringMethod { get; set; }

    /// <summary>
    /// Gets the exception message and stack trace from the provided exception obj pointer
    /// </summary>
    /// <param name="ex">The exception object pointer</param>
    /// <returns>Message and stack</returns>
    private static string GetException(IntPtr ex)
    {
        if (ExceptionCls == IntPtr.Zero)
        {
            ExceptionCls = MudInterface.get_class(Instance.Env, "java/lang/Throwable");
            FrameCls = MudInterface.get_class(Instance.Env, "java/lang/StackTraceElement");

            GetExceptionCauseMethod =
                MudInterface.get_method(Instance.Env, ExceptionCls, "getCause", "()Ljava/lang/Throwable;");

            GetExceptionStackMethod = MudInterface.get_method(Instance.Env, ExceptionCls, "getStackTrace",
                "()[Ljava/lang/StackTraceElement;");

            ExceptionToStringMethod =
                MudInterface.get_method(Instance.Env, ExceptionCls, "toString", "()Ljava/lang/String;");

            FrameToStringMethod = MudInterface.get_method(Instance.Env, FrameCls, "toString", "()Ljava/lang/String;");
        }

        var mallocStr = MudInterface.get_exception_msg(Instance.Env, ex, GetExceptionCauseMethod,
            GetExceptionStackMethod, ExceptionToStringMethod,
            FrameToStringMethod, true);
        var str = Marshal.PtrToStringAnsi(mallocStr)!;
        MudInterface.interop_free(mallocStr);
        return str;
    }

    internal static T UsingArgs<T>(TypedArg[] args, Func<JavaVal[], JavaCallResp> action)
    {
       return (T) UsingArgs(typeof(T), args, action);
    }
    /// <summary>
    /// Maps the provided arguments to their respective JavaVal type.
    /// Automatically allocates & frees and provided strings or arrays
    /// </summary>
    /// <param name="returnType">The type of the returned object that is to be mapped</param>
    /// <param name="args">Args to be used</param>
    /// <param name="action">Action to be run with mapped args that returns a value</param>
    /// <returns></returns>
    /// <exception cref="JavaException">Will throw if there is an exception in the JVM</exception>
    internal static object UsingArgs(Type returnType, TypedArg[] args, Func<JavaVal[], JavaCallResp> action)
    {
        JavaCallResp resp = new();
        UsingArgs(args, (jArgs) => { resp = action(jArgs); });

        if (resp.IsException)
        {
            var exceptionMsg = GetException(resp.Value.Object);
            ObjPointers.Remove(resp.Value.Object);
            MudInterface.release_obj(Instance.Env, resp.Value.Object);
            var firstLine = exceptionMsg.IndexOf('\n');
            Console.WriteLine(exceptionMsg);
            if (firstLine == -1)
            {
                throw new JavaException(exceptionMsg, "");
            }
            throw new JavaException(exceptionMsg[..firstLine], exceptionMsg[(firstLine + 1)..]);
        }

        return TypeMap.MapJValue(returnType, resp.Value);
    }

    /// <summary>
    /// Loops through the provided arguments and maps them to matching JavaVal, allocates required backing objects for strings and arrays
    /// </summary>
    /// <param name="args">Args to be mapped</param>
    /// <returns>A tuple of the mapped args and any allocated pointers that must be freed</returns>
    private static (JavaVal[] Args, List <IntPtr> Pointers) MapArgs(TypedArg[] args)
    {
        var mapped = new JavaVal[args.Length];
        var pointers = new List<IntPtr>();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i].Val;
            if (a == null)
            {
                mapped[i] = new()
                {
                    Object = IntPtr.Zero
                };
                continue;
            }
            if (a is string str)
            {
                var strResp = JavaString(str);
                pointers.Add(strResp);
                mapped[i] = new()
                {
                    Object = strResp
                };
                continue;
            }
            if (a.GetType().IsArray)
            {
                var type = TypeMap.MapToType(a.GetType().GetElementType()!, null);
                ClassInfo? arrCls = null;
                if (type.ClassPath != null)
                {
                    arrCls = GetClassInfo(type.ClassPath);
                }

                var arrObjItems = new TypedArg[((Array)a).Length];
                var j = 0;
                foreach (var arrItem in (Array) a)
                {
                    arrObjItems[j++] = arrItem as TypedArg ?? new(arrItem);
                }

                var arrayPointers = MapArgs(arrObjItems);
                pointers.AddRange(arrayPointers.Pointers);
                
                mapped[i] = new()
                {
                    Object = MudInterface.array_new(Instance.Env, arrObjItems.Length, arrayPointers.Args, type.Type, arrCls?.Cls ?? IntPtr.Zero)
                };
                continue;
            }
            mapped[i] = JavaVal.MapFrom(a);
        }


        return (mapped, pointers);
    }
    
    /// <summary>
    /// Maps the args provided into respective JavaVal union, then call the action with mapped values.
    /// Will automatically allocate/deallocate strings & arrays
    /// </summary>
    /// <param name="args">Args to be mapped</param>
    /// <param name="action">Action to run with mapped args</param>
    internal static void UsingArgs(TypedArg[] args, Action<JavaVal[]> action)
    {
        EnsureInit();
        var (jArgs, pointers) = MapArgs(args);

        try
        {
            action(jArgs);
        }
        finally
        {
            foreach (var ptr in pointers)
            {
                try
                {
                    MudInterface.release_obj(Instance.Env, ptr);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }

    public static void AddClassPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        
        path = path.Trim();
        if (!path.StartsWith("file://"))
        {
            path = $"file://{Path.GetFullPath(path)}";
        }
        
        MudInterface.add_class_path(Instance.Env, path);
    }

    /// <summary>
    /// Gets the class of the provided java object pointer
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    internal static ClassInfo GetObjClass(IntPtr obj)
    {
        var classInfo = GetClassInfo("java/lang/Class");
        // var getNameMethod = classInfo.GetMethodPtrBySig("getName", "()Ljava/lang/String;", false);
        var cls = MudInterface.get_class_of_obj(Instance.Env, obj);
        ObjPointers.Add(cls);
        var classPath = classInfo.Call<string>(cls, "getName", new TypedArg[]{}, false);
        return GetClassInfo(classPath);
    }

    
    /// <summary>
    ///  Creates the backing java object with the provided argyments
    /// </summary>
    /// <param name="obj">Object to bind to</param>
    /// <param name="classPath">Custom class path if desired</param>
    /// <param name="args">Arguments to use when calling the java objects constructor</param>
    private static void LoadObj(IBoundObject obj, string classPath, params TypedArg[] args)
    {
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        obj.Info ??= GetClassInfo(classPath);
        obj.Env = Instance.Env;

        obj.Bind(args);
        ObjPointers.Add(obj.Jobj);
    }
    
    private static IBoundObject NewObj(Type type, string? classPath, params TypedArg[] args)
    {
        EnsureInit();
        classPath ??= classPath ?? type.GetCustomAttributes<ClassPathAttribute>().FirstOrDefault()?.ClassPath ?? 
            type.FullName!.Split('`')[0];
        var obj = NewUnboundObj(type);
        LoadObj(obj, classPath, args);
        return obj;
    }
    
    
    /// <summary>
    /// Creates a new object with a bound backing java object
    /// </summary>
    /// <param name="args">Arguments to use when calling the java objects constructor</param>
    /// <typeparam name="T">Type of desired object to be created</typeparam>
    /// <returns></returns>
    internal static T NewObj<T>(params TypedArg[] args)
    {
        EnsureInit();
        var classPath = typeof(T).GetCustomAttributes<ClassPathAttribute>().FirstOrDefault()?.ClassPath ?? typeof(T).FullName!.Split('`')[0];
        var obj = NewUnboundObj(typeof(T));
        LoadObj(obj, classPath, args);
        return (T) (object) obj;
    }
    
    // internal static T NewUnboundObj<T>()
    // {
        // return (T) (object) NewUnboundObj(typeof(T));
    // }

    internal static IBoundObject NewUnboundObj(Type type)
    {
        if (!type.IsAssignableTo(typeof(BoundObject)))
        {
            type = TypeGen.Build(type)!;    
        }
        return (BoundObject)Activator.CreateInstance(type)!;
    }

    /// <summary>
    /// Creates a new object with a bound backing java object 
    /// </summary>
    /// <param name="classPath">Custom class path if desired</param>
    /// <param name="args">Arguments to use when calling the java objects constructor</param>
    /// <returns></returns>
    internal static IBoundObject NewObj(string? classPath, params TypedArg[] args)
    {
      return NewObj(typeof(BoundObject), classPath, args);  
    } 
    

    /// <summary>
    /// Release the backing java objects for the provided enumarable 
    /// </summary>
    /// <param name="obj"></param>
    public static void ReleaseObj(IEnumerable<IBoundObject> obj)
    {
        EnsureInit();
        foreach (var boundObject in obj)
        {
            boundObject.Release();
        }
    }

    /// <summary>
    /// Release the backing java objects 
    /// </summary>
    /// <param name="obj"></param>
    public static void ReleaseObj(IntPtr obj)
    {
        try
        {
            EnsureInit();
            ObjPointers.Remove(obj);
            MudInterface.release_obj(Instance.Env, obj);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            Console.WriteLine(e.StackTrace);
        }
    }

    // /// <summary>
    // /// Destroys the JVM
    // /// </summary>
    // public static void Destroy()
    // {
    //     EnsureInit();
    //     var pointers = ObjPointers.Concat(ClassInfos.Values.SelectMany(c =>
    //     {
    //         var clsPointers = c.Props.Values.Concat(c.Methods.Values.SelectMany(m => m.Values));
    //         c.Methods.Clear();
    //         c.Props.Clear();
    //         return clsPointers;
    //     })).ToList();
    //     
    //     pointers.ForEach(ptr =>
    //     {
    //         MudInterface.release_obj(Instance.Env, ptr);
    //     });
    //     ObjPointers.Clear();
    //     MudInterface.destroy_instance(Instance.Jvm);
    // }
    
}


public struct JvmInstance
{
    internal IntPtr Jvm { get; init; }
    internal IntPtr Env { get; init; }
}

internal static class MudInterface
{
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_jvm_create_instance")]
    internal static extern JvmInstance create_instance(IntPtr options, int optionsAmnt);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_jvm_options_str_arr",
        CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr gen_options_arr(int amnt, string[] options);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_jvm_options",
        CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr gen_options(int amnt);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_jvm_options_set",
        CallingConvention = CallingConvention.Cdecl)]
    internal static extern void options_set(IntPtr options, int index, string arg);


    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "interop_free")]
    internal static extern void interop_free(IntPtr ptr);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_get_exception_msg")]
    internal static extern IntPtr get_exception_msg(IntPtr env, IntPtr ex, IntPtr getCauseMethod, IntPtr getStackMethod,
        IntPtr exToStringMethod, IntPtr frameToStringMethod, bool isTop);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_jvm_check_exception")]
    internal static extern IntPtr check_exception(IntPtr env);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_string_new")]
    internal static extern IntPtr string_new(IntPtr env, string str);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_string_release")]
    internal static extern void string_release(IntPtr env, IntPtr jString, IntPtr charStr);


    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_get_method")]
    internal static extern IntPtr get_method(IntPtr env, IntPtr cls, string methodName, string signature);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_call_static_method",
        CallingConvention = CallingConvention.Cdecl)]
    internal static extern JavaCallResp call_static_method(IntPtr env, IntPtr cls, IntPtr method, JavaType type,
        JavaVal[] args);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_call_method",
        CallingConvention = CallingConvention.Cdecl)]
    internal static extern JavaCallResp call_method(IntPtr env, IntPtr obj, IntPtr method, JavaType type,
        JavaVal[] args);


    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_build_class_object")]
    internal static extern IntPtr build_obj_by_path(IntPtr env, string classPath);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_new_object")]
    internal static extern IntPtr new_obj(IntPtr env, IntPtr jClass, string sig, [In, Out] JavaVal[] args);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_get_class")]
    internal static extern IntPtr get_class(IntPtr env, string classPath);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_get_class_of_obj")]
    internal static extern IntPtr get_class_of_obj(IntPtr env, IntPtr obj);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_get_static_method")]
    internal static extern IntPtr get_static_method(IntPtr env, IntPtr cls, string methodName, string signature);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_call_method_by_name")]
    internal static extern JavaCallResp call_method(IntPtr env, IntPtr obj, string method, string signature,
        JavaType type, JavaVal[] args);


    internal static JavaCallResp call_method(IntPtr env, IntPtr obj, IntPtr method, JavaType type) =>
        call_method(env, obj, method, type, Array.Empty<JavaVal>());

    internal static JavaCallResp call_method(IntPtr env, IntPtr cls, string method, string signature, JavaType type) =>
        call_method(env, cls, method, signature, type, Array.Empty<JavaVal>());


    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_call_static_method_by_name")]
    internal static extern JavaCallResp call_static_method(IntPtr env, IntPtr cls, string method, string signature,
        JavaType type, JavaVal[] args);

    internal static JavaCallResp call_static_method(IntPtr env, IntPtr cls, string method, string signature,
        JavaType type) =>
        call_static_method(env, cls, method, signature, type, Array.Empty<JavaVal>());


    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_get_field_id")]
    internal static extern IntPtr get_field(IntPtr env, IntPtr cls, string name, string signature);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_get_static_field_id")]
    internal static extern IntPtr get_static_field(IntPtr env, IntPtr cls, string name, string signature);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_get_field_value")]
    internal static extern JavaVal get_field_value(IntPtr env, IntPtr obj, IntPtr field, JavaType type);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_set_field_value")]
    internal static extern void set_field_value(IntPtr env, IntPtr obj, IntPtr field, JavaType type, JavaVal val);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_get_static_field_value")]
    internal static extern JavaVal get_static_field_value(IntPtr env, IntPtr cls, IntPtr field, JavaType type);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_set_static_field_value")]
    internal static extern void set_static_field_value(IntPtr env, IntPtr obj, IntPtr field, JavaType type, JavaVal val);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_release_object")]
    internal static extern void release_obj(IntPtr env, IntPtr obj);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_instance_of")]
    internal static extern bool instance_of(IntPtr env, IntPtr obj, IntPtr cls);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_array_new")]
    internal static extern IntPtr array_new(IntPtr env, int size, JavaVal[] values, JavaType type, IntPtr objCls);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_array_length")]
    internal static extern int array_length(IntPtr env, IntPtr obj);

    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_array_get_at")]
    internal static extern JavaVal array_get_at(IntPtr env, IntPtr arr, int index, JavaType type);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_jvm_destroy_instance")]
    internal static extern void destroy_instance(IntPtr jvm);
    
    [DllImport("libMud", CharSet = CharSet.Ansi, EntryPoint = "mud_add_class_path")]
    internal static extern void add_class_path(IntPtr env, string path);
    
    
}

public static class IntPtrExtensions
{
    public static string HexAddress(this IntPtr ptr) => $"0x{ptr:X}".ToLower();
}