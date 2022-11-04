using System.Runtime.InteropServices.ComTypes;
using Mud.Exceptions;
using Mud.Types;

namespace Mud;

public class ClassInfo<T> : ClassInfo
{
    private static T? _static;
    public static T Static
    {
        get
        {
            if (_static != null) return _static;
            _static = (T)Jvm.NewUnboundObj(typeof(T));
            ((IBoundObject)_static!).IsStatic = true;
            return _static;
        }
    }

    // ReSharper disable once StaticMemberInGenericType
    private static ClassInfo? _class;
    public static ClassInfo Class => _class ??= Jvm.GetClassInfo<T>();
    
    internal ClassInfo(IntPtr cls, string classPath) : base(cls, classPath)
    {
        
    }


    public new static T Instance(params object[] args) => Instance(args.Select(a => new TypedArg(a)).ToArray());
    public new static T Instance(TypedArg[] args) => Jvm.NewObj<T>(args);

    internal new static void Call(string method, TypedArg[] args) => 
        Jvm.GetClassInfo<T>().Call(method, args);
    
    internal new static T1 Call<T1>(string method, CustomType returnType, TypedArg[] args) => 
        Class.Call<T1>(method, returnType, args);
    
    internal new static T1 GetField<T1>(string field, CustomType? type = null) => Class.GetField<T1>(field, type);
    internal new static void SetField(string field, TypedArg value) => Class.SetField(field, value);

}

public class ClassInfo
{
    /// <summary>
    /// The java class object pointer
    /// </summary>
    internal IntPtr Cls { get; }
    /// <summary>
    /// The class path of the class
    /// </summary>
    public string ClassPath { get; }
    
    /// <summary>
    /// The java type signature 
    /// </summary>
    public string TypeSignature { get; }
    
    /// <summary>
    /// Cached method lookups
    /// </summary>
    internal Dictionary<string, Dictionary<string, IntPtr>> Methods { get; } = new();
    
    /// <summary>
    /// Cached field lookups
    /// </summary>
    internal Dictionary<string, IntPtr> Props { get; } = new();
    
    
    /// <param name="cls">The java class object pointer</param>
    /// <param name="classPath">The java class path</param>
    internal ClassInfo(IntPtr cls, string classPath)
    {
        Cls = cls;
        ClassPath = classPath;
        TypeSignature = classPath switch
        {
            "int" => "I",
            "long" => "J",
            "boolean" => "Z",
            "byte" => "B",
            "short" => "S",
            "char" => "C",
            "float" => "F",
            "double" => "D",
            "void" or "Void" => "V",
            _ => $"L{classPath.Replace('.', '/')};"
        };
    }


    public IBoundObject Instance(params object[] args) => Instance(args.Select(a => new TypedArg(a)).ToArray());
    public IBoundObject Instance(TypedArg[] args) => Jvm.NewObj(ClassPath, args);

    /// <summary>
    /// Checks if there is a matching instance or static method for this class
    /// </summary>
    /// <param name="method">Method name</param>
    /// <param name="sig">The methods Java type signature</param>
    public bool HasMethod(string method, string sig)
    {
        return GetMethodPtr(method, sig, false) != IntPtr.Zero || GetMethodPtr(method, sig, true) != IntPtr.Zero;
    }

    /// <summary>
    /// Checks if there is a matching instance or static method for this class
    /// </summary>
    /// <param name="field">Field name</param>
    /// <param name="sig">The fields Java type signature</param>
    private bool HasField(string field, string sig)
    {
        return GetFieldPtr(field, sig, false) != IntPtr.Zero || GetFieldPtr(field, sig, true) != IntPtr.Zero;
    }

    /// <summary>
    /// Gets the corresponding method is in the class
    /// </summary>
    /// <param name="method">Method name</param>
    /// <param name="signature">Signature of the method</param>
    /// <param name="isStatic">Whether the method is static or not</param>
    /// <returns>Method pointer</returns>
    internal IntPtr GetMethodPtr(string method, string signature, bool isStatic)
    {
        if (method == "")
        {
            _ = 0;
        }
        if (!Methods.TryGetValue(method, out var methodPointers))
        {
            methodPointers = new();
            Methods[method] = methodPointers;
        }

        if (methodPointers.TryGetValue(signature, out var methodPtr) && methodPtr != IntPtr.Zero)
        {
            return methodPtr;
        }
        Jvm.EnsureInit();
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
            // exception is thrown when method with sig not found, but we throw out own exception, so just capture and release
            MudInterface.release_obj(Jvm.Instance.Env, MudInterface.check_exception(Jvm.Instance.Env));
            throw new MemberNotFoundException(ClassPath, method, signature,
                $"Method {method} with type signature {signature} not found on class {ClassPath}");
        }

        return methodPtr;
    }
    

    internal T Call<T>(IntPtr objOrClass, string method, CustomType returnType, TypedArg[] args, bool isStatic)
    {
        Func<IntPtr, IntPtr, IntPtr, JavaType, JavaVal[], JavaCallResp> func = isStatic ? MudInterface.call_static_method : MudInterface.call_method;
        var methodPtr = GetMethodPtr(method, TypeMap.GenMethodSignature(returnType, args.Select(a => a.Type).ToArray()), isStatic);
        return Jvm.UsingArgs<T>(args, jArgs =>
        {
            var resp = func(Jvm.Instance.Env, objOrClass, methodPtr, returnType.Type, jArgs);
            return resp;
        });
    }
    internal T Call<T>(IntPtr objOrClass, string method, TypedArg[] args, bool isStatic)
    {
        return Call<T>(objOrClass, method,
            TypeMap.MapToType(typeof(T), null), args, isStatic);
    }

    public T Call<T>(string method, params object[] args) => 
        Call<T>(method, args.Select(a => new TypedArg(a)).ToArray());
    
    public T Call<T>(string method, CustomType returnType, params object[] args) => 
        Call<T>(method, returnType, args.Select(a => new TypedArg(a)).ToArray());

    public T Call<T>(string method, TypedArg[] args) => Call<T>(method, TypeMap.MapToType(typeof(T), null), args);
    
    public T Call<T>(string method, CustomType returnType, TypedArg[] args)
    {
        return Call<T>(Cls, method, returnType, args, true);
    }
    
    internal void Call(IntPtr objOrClass, string method, TypedArg[] args, bool isStatic)
    {
        Func<IntPtr, IntPtr, IntPtr, JavaType, JavaVal[], JavaCallResp> func = isStatic ? MudInterface.call_static_method : MudInterface.call_method;
        var methodPtr = GetMethodPtr(method, TypeMap.GenMethodSignature(args), false);
        Jvm.UsingArgs(args, jArgs =>
        {
            func(Jvm.Instance.Env, objOrClass, methodPtr, JavaType.Void, jArgs);
        });
    }

    public void Call(string method, params object[] args) => 
        Call(method, args.Select(a => new TypedArg(a)).ToArray());
    
    public void Call(string method, TypedArg[] args)
    {
        Call(Cls, method, args, true);
    }

    public IBoundObject Call(string method, CustomType returnType, params object[] args) =>
        Call(method, returnType, args.Select(a => new TypedArg(a)).ToArray());
    
    public IBoundObject Call(string method, CustomType returnType, TypedArg[] args)
    {
        return Call<BoundObject>(Cls, method, returnType, args, true);  
    }


    internal IBoundObject Call(IntPtr objOrClass, string method, CustomType returnType, object[] args, bool isStatic) =>
        Call(objOrClass, method, returnType, args.Select(a => new TypedArg(a)).ToArray(), isStatic);
    
    internal IBoundObject Call(IntPtr objOrClass, string method, CustomType returnType, TypedArg[] args, bool isStatic)
    {
        return Call<BoundObject>(objOrClass, method, returnType, args, isStatic);
    }
    
    internal IntPtr GetFieldPtr(string name, string signature, bool isStatic)
    {
        if (!Props.TryGetValue(name, out var fieldPtr) || fieldPtr == IntPtr.Zero)
        {
            Jvm.EnsureInit();
            fieldPtr = isStatic ? MudInterface.get_static_field(Jvm.Instance.Env, Cls, name, signature) :
                MudInterface.get_field(Jvm.Instance.Env, Cls, name, signature);
            Props[name] = fieldPtr;
        }
        MudInterface.release_obj(Jvm.Instance.Env, MudInterface.check_exception(Jvm.Instance.Env));
        if (fieldPtr == IntPtr.Zero)
        {
            // exception is thrown when field with sig not found, but we throw our own exception, so just capture and release
            MudInterface.release_obj(Jvm.Instance.Env, MudInterface.check_exception(Jvm.Instance.Env));
            throw new MemberNotFoundException(ClassPath, name, signature,
                $"Field {name} with type signature {signature} not found on class {ClassPath}");
        }
        return fieldPtr;
    }
    
    public IBoundObject GetField(IntPtr objOrCls, string name, CustomType customType, bool isStatic)
    {
        Func<IntPtr, IntPtr, IntPtr, JavaType, JavaVal>
            func = isStatic ? MudInterface.get_static_field_value : MudInterface.get_field_value;
        
        var fieldPtr = GetFieldPtr(name, customType.TypeSignature, true);
        return TypeMap.MapJValue<BoundObject>(JavaType.Object, func(Jvm.Instance.Env, objOrCls, fieldPtr, JavaType.Object));
    }
    
    public T GetField<T>(IntPtr objOrCls, string name, CustomType customType, bool isStatic)
    {

        Func<IntPtr, IntPtr, IntPtr, JavaType, JavaVal>
            func = isStatic ? MudInterface.get_static_field_value : MudInterface.get_field_value; 

        var fieldPtr = GetFieldPtr(name, customType.TypeSignature, true);
        return TypeMap.MapJValue<T>(customType.Type, func(Jvm.Instance.Env, objOrCls, fieldPtr, customType.Type));
    }
    
    public IBoundObject GetField(string name, CustomType type)
    {
        return GetField(Cls, name, type, true);
    }

    public T GetField<T>(string name, CustomType? type = null) =>
        GetField<T>(Cls, name, type ?? TypeMap.MapToType(typeof(T), null), true);
    


    public void SetField(IntPtr objOrCls, string name, TypedArg val, bool isStatic)
    {
        var fieldPtr = GetFieldPtr(name, val.Type.TypeSignature, isStatic);
        Jvm.UsingArgs(new[] { val }, jArgs =>
        {
            Action<IntPtr, IntPtr, IntPtr, JavaType, JavaVal> func =
                isStatic ? MudInterface.set_static_field_value : MudInterface.set_field_value; 
            func(Jvm.Instance.Env, objOrCls, fieldPtr, val.Type.Type, jArgs[0]);
        });
    }

    /// <summary>
    /// Sets the corresponding static field on the java class
    /// </summary>
    /// <param name="name">Name of the field to be set</param>
    /// <param name="value">Value to set</param>
    public void SetField(string name, IBoundObject value, CustomType? customType = null)
    {
        customType ??= TypeMap.MapToType(value.GetType(), value.ClassPath);
        SetField(Cls, name, new TypedArg(value, customType), true);
    }

    public void SetField(string name, TypedArg value)
    {
        SetField(Cls, name, value, true);
    }
    public void SetField<T>(string name, T value, CustomType? customType = null)
    {
        customType ??= TypeMap.MapToType(typeof(T), (value as IBoundObject)?.ClassPath);
        SetField(Cls, name, new TypedArg(value, customType), true);
    }
    
    public void SetField(string name, object value, CustomType? customType = null)
    {
        customType ??= TypeMap.MapToType(value.GetType(), (value as IBoundObject)?.ClassPath);
        SetField(Cls, name, new TypedArg(value, customType), true);
    }
    
}
