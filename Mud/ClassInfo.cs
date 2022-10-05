using Mud.Exceptions;
using Mud.Types;

namespace Mud;

public class ClassInfo
{
    internal Jvm Jvm { get; }
    internal IntPtr Cls { get; }
    public string ClassPath { get; }
    internal Dictionary<string, Dictionary<string, IntPtr>> Methods { get; } = new();
    internal Dictionary<string, IntPtr> Props { get; } = new();

    public ClassInfo(Jvm jvm, IntPtr cls, string classPath)
    {
        Jvm = jvm;
        Cls = cls;
        ClassPath = classPath;
    }

    internal IntPtr GetMethodPtrBySig(string method, string signature, bool isStatic)
    {

        if (!Methods.TryGetValue(method, out var methodPointers))
        {
            methodPointers = new();
            Methods[method] = methodPointers;
        }
        
        if (!methodPointers.TryGetValue(signature, out var methodPtr))
        {
            // Console.WriteLine($"Getting {(isStatic ? "static" : "member")} method {method} with type signature {signature} in class {ClassPath} [{Cls.HexAddress()}] ");
            if (isStatic)
            {
                methodPtr = MudInterface.get_static_method(Jvm.Instance.Env, Cls, method, signature);
            }
            else
            {
                methodPtr = MudInterface.get_method(Jvm.Instance.Env, Cls, method, signature);
            }
            
            methodPointers[signature] = methodPtr;

            if (methodPtr == IntPtr.Zero)
            {
                throw new MethodNotFoundException(ClassPath, method, signature,
                    $"Method {method} with type signature {signature} not found on class {ClassPath}");
            }
        }

        return methodPtr;
    }
    internal IntPtr GetMethodPtr(bool isStatic, string? returnType, string method, params object[] args)
    {
        string sig;
        if (returnType == null)
        {
            sig = $"({string.Join("", args.Select(JavaObject.GenSignature))})V";
        }
        else
        {
            sig = $"({string.Join("", args.Select(JavaObject.GenSignature))})L{returnType.Replace(".", "/")};";
        }

        return GetMethodPtrBySig(method, sig, isStatic);
    }

    internal IntPtr GetMethodPtr(bool isStatic, JavaFullType? returnType, string method, params object[] args)
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

        return GetMethodPtrBySig(method, sig, isStatic);
    }
    internal IntPtr GetMethodPtr(bool isStatic, Type? returnType, string method, params object[] args)
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

        return GetMethodPtrBySig(method, sig, isStatic);
    }
    internal IntPtr GetMethodPtr<T>(bool isStatic, string method, params object[] args)
    {
        return GetMethodPtr(isStatic, typeof(T), method, args);
    }
    
    internal IntPtr GetMethodNamedReturnPtr<T>(bool isStatic, string method, string returnPath, params object[] args)
    {
        return GetMethodPtr(isStatic, typeof(T), method, args);
    }
    
    internal IntPtr GetVoidMethodPtr(bool isStatic, string method, params object[] args)
    {
        return GetMethodPtr(isStatic, (string?) null, method, args);
    }

    public JavaObject CallMethodNamedReturn(IntPtr obj, string method, JavaFullType returnType, params object[] args) =>
        CallMethodNamedReturn<JavaObject>(obj, method, returnType, args); 
    public T CallMethodNamedReturn<T>(IntPtr obj, string method, JavaFullType returnType, params object[] args)
    {
        return Jvm.UsingArgs<T>(args, jArgs =>
        {
            var methodPtr = GetMethodPtr(false, returnType, method, args);
            var resp = MudInterface.call_method(Jvm.Instance.Env, obj, methodPtr, JavaObject.MapToType(typeof(T)).Type, jArgs);

            return resp;
        });
    }
    public T CallMethod<T>(IntPtr obj, string method, params object[] args)
    {
        return Jvm.UsingArgs<T>(args, jArgs =>
        {
            var methodPtr = GetMethodPtr<T>(false, method, args);
            return MudInterface.call_method(Jvm.Instance.Env, obj, methodPtr, JavaObject.MapToType(typeof(T)).Type, jArgs);
        });
    }
    
    public void CallMethod(IntPtr obj, string method, params object[] args)
    {
        Jvm.UsingArgs(args, jArgs => MudInterface.call_method(Jvm.Instance.Env, obj, GetVoidMethodPtr(false, method, args), JavaType.Void, jArgs));
    }
    
    public T CallStaticMethod<T>(string method, params object[] args)
    {
        return Jvm.UsingArgs<T>(args, jArgs =>
        {
            var methodPtr = GetMethodPtr<T>(true, method, args);
            var type = JavaObject.MapToType(typeof(T)).Type;
            return MudInterface.call_static_method(Jvm.Instance.Env, Cls, methodPtr, type, jArgs);
        });
    }
    
    public void CallStaticMethod(string method, params object[] args)
    {
        Jvm.UsingArgs(args, jArgs => MudInterface.call_static_method(Jvm.Instance.Env, Cls, GetVoidMethodPtr(true, method, args), JavaType.Void, jArgs));
    }

    public JavaObject CallStaticMethodNamedReturn(string method, string returnType, params object[] args) =>
        CallStaticMethodNamedReturn<JavaObject>(method, returnType, args);
    
    public T CallStaticMethodNamedReturn<T>(string method, string returnType, params object[] args)
    {
        return Jvm.UsingArgs<T>(args, jArgs =>
        {
            var methodPtr = GetMethodPtr(true, returnType, method, args);
            Console.WriteLine($"call {jArgs[0].Int}");
            var resp = MudInterface.call_static_method(Jvm.Instance.Env, Cls, methodPtr, JavaType.Object, jArgs);
            Console.WriteLine($"resp {resp.Value.Object.HexAddress()}");
            return resp;
        });
    }


    internal IntPtr GetFieldPtr(Type fieldType, string name)
    {
        if (!Props.TryGetValue(name, out var fieldPtr))
        {
            
            fieldPtr = MudInterface.get_field(Jvm.Instance.Env, Cls, name, JavaObject.GenSignature(fieldType));
            Props[name] = fieldPtr;
        }
        return fieldPtr;
    }
    internal IntPtr GetFieldPtr<T>(string name)
    {
        return GetFieldPtr(typeof(T), name);
    }

    public T GetField<T>(IntPtr obj, string name, JavaFullType? type = null)
    {
        var fieldPtr = GetFieldPtr<T>(name);
        type ??= JavaObject.MapToType(typeof(T));
        return Jvm.MapJValue<T>(type, MudInterface.get_field_value(Jvm.Instance.Env, obj, fieldPtr, type.Type));
    }
    
    public T GetStaticField<T>(string name, JavaFullType? type = null)
    {
        var fieldPtr = GetFieldPtr<T>(name);
        type ??= JavaObject.MapToType(typeof(T));
        return Jvm.MapJValue<T>(type, MudInterface.get_static_field_value(Jvm.Instance.Env, Cls, fieldPtr, type.Type));
    }
    
}
