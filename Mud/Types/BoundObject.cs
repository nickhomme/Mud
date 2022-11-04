using System.Dynamic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mud.Exceptions;

[assembly: InternalsVisibleTo("Mud")]
namespace Mud.Types;

[AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
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


[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public class JavaNameAttribute : Attribute
{
    public string Name { get; }

    public JavaNameAttribute(string name)
    {
        Name = name;
    }
}
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter)]
public class JavaTypeAttribute : Attribute
{
    public string ClassPath { get; }

    public JavaTypeAttribute(string classPath)
    {
        ClassPath = classPath;
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
public class JavaStaticAttribute : JavaNameAttribute
{

    public JavaStaticAttribute(string? name = null) : base(name!)
    {
    }
}

// public interface IBoundObject : IDisposable {
//     public string ClassPath { get; }
// }

/// <summary>
/// The base class used that is used to bind the .NET class to the Java class 
/// </summary>
internal class BoundObject : IBoundObject
{
    private IntPtr _jobj = IntPtr.Zero;
    private IntPtr _env;
    private ClassInfo _info;
    private bool _isStatic;
    
    
    public string JavaObjAddress => _jobj.HexAddress();
    public string ClassPath => _info.ClassPath;

    IntPtr IBoundObject.Env
    {
        get => _env;
        set => _env = value;
    }
    
    IntPtr IBoundObject.Jobj
    {
        get => _jobj;
        set => _jobj = value;
    }

    bool IBoundObject.IsStatic
    {
        get => _isStatic;
        set => _isStatic = value;
    }
    ClassInfo IBoundObject.Info
    {
        get => _info;
        set => _info = value;
    }

    /// <summary>
    /// If the Backing java object is initialized it will call the "toString" method on it.
    /// Otherwise it will print the object's class path if the class has been set.
    /// If the class has not been set it will fall back to the .NET default ToString method
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        if (_jobj != IntPtr.Zero)
        {
            return Call<string>("toString");
        }

        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        return _info != null ? $"{_info.ClassPath}" : base.ToString()!;
    }

    /// <summary>
    /// Checks whether the two java objects are the same instance
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public bool Equals(IBoundObject? obj)
    {
        return obj?.Jobj == _jobj;
    }

    /// <summary>
    /// Checks whether the object is an instance of a class via classpath
    /// </summary>
    /// <param name="classPath">Class path to check against</param>
    /// <returns></returns>
    public bool InstanceOf(string classPath)
    {
        return MudInterface.instance_of(_env, _jobj, Jvm.GetClassInfo(classPath).Cls);
    }
    
    /// <summary>
    /// Checks whether the object is an instance of a class via ClassInfo
    /// </summary>
    /// <param name="cls">ClassInfo to check against</param>
    /// <returns></returns>
    public bool InstanceOf(ClassInfo cls)
    {
        return MudInterface.instance_of(_env, _jobj, cls.Cls);
    }
    
    /// <summary>
    /// Checks whether the object is an instance of a class ptr
    /// </summary>
    /// <param name="cls">Class pointer to check against</param>
    /// <returns></returns>
    internal bool InstanceOf(IntPtr cls)
    {
        return MudInterface.instance_of(_env, _jobj, cls);
    }
    
    /// <summary>
    /// Checks whether the object is an instance of another object's class
    /// </summary>
    /// <param name="obj">Bound object to check against</param>
    /// <returns></returns>
    public bool InstanceOf(IBoundObject obj)
    {
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        obj.Info ??= Jvm.GetClassInfo(obj.GetType().GetCustomAttributes<ClassPathAttribute>().First().ClassPath);
        return InstanceOf(obj.Info);
    }


    /// <summary>
    /// Creates the backing Java object
    /// </summary>
    /// <param name="signature">The java type signature of the desired constructor</param>
    /// <param name="args">Args to use for the Java class constructor</param>
    internal void BindBySig(string signature, TypedArg[] args)
    {
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        _info ??= Jvm.GetClassInfo(GetType().GetCustomAttributes<ClassPathAttribute>().FirstOrDefault()?.ClassPath ?? GetType().FullName!.Split('`')[0]);
#pragma warning disable CS8073
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (_jobj != IntPtr.Zero) return;
#pragma warning restore CS8073
        Jvm.UsingArgs(args, jArgs =>
        {
            _jobj = MudInterface.new_obj(Jvm.Instance.Env, _info.Cls, signature, jArgs);
        });
        // Console.WriteLine($"Bound: {_jobj.HexAddress()}");
    }

    
    /// <summary>
    /// Creates the backing Java object
    /// </summary>
    /// <param name="args">Args to use for the Java class constructor</param>
    void IBoundObject.Bind(TypedArg[] args) => BindBySig(TypeMap.GenMethodSignature(args), args);







    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="args">Arguments to be passed into the method</param>
    public void Call(string method, params object[] args) => 
        Call(method, args.Select(a => new TypedArg(a)).ToArray());
    public void Call(string method, TypedArg[] args)
    {
        if (_isStatic)
        {
            throw new InstanceMemberOnStaticException(method, _info);
        }
        _info.Call(_jobj, method, args, false);
    }

    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="args">Arguments to be passed into the method</param>
    /// <typeparam name="T"></typeparam>
    public T Call<T>(string method, params object[] args) => Call<T>(method, args.Select(a => new TypedArg(a)).ToArray());
    public T Call<T>(string method, TypedArg[] args)
    {
        if (_isStatic)
        {
            throw new InstanceMemberOnStaticException(method, _info);
        }
        // Console.WriteLine($"Calling Method: {_jobj.HexAddress()}->{method}({string.Join(", ", args.Select(a => a.ToString()))})");
        return _info.Call<T>(_jobj, method, args, false);
    }


    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="returnType">Custom return type to be used</param>
    /// <param name="args">Arguments to be passed into the method</param>
    public IBoundObject Call(string method, CustomType returnType, params object[] args) =>
        Call(method, returnType, args.Select(a => new TypedArg(a)).ToArray());
    
    public IBoundObject Call(string method, CustomType returnType, TypedArg[] args)
    {
        if (_isStatic)
        {
            throw new InstanceMemberOnStaticException(method, _info);
        }
        return _info.Call(_jobj, method, returnType, args, false);
    }
    
    public T Call<T>(string method, CustomType returnType, TypedArg[] args)
    {
        if (_isStatic)
        {
            throw new InstanceMemberOnStaticException(method, _info);
        }
        return _info.Call<T>(_jobj, method, returnType, args, false);
    }
    
    
    /// <summary>
    /// Gets the provided field on the backing java object
    /// </summary>
    /// <param name="field">Field to be retrieved from</param>
    /// <param name="type">Custom type to be used when locating the correct field</param>
    /// <returns></returns>
    public IBoundObject GetField(string field, CustomType type)
    {
        if (_isStatic)
        {
            throw new InstanceMemberOnStaticException(field, _info);
        }
        return _info.GetField(_jobj, field, type, false);
    }

    /// <summary>
    /// Gets the provided field on the backing java object
    /// </summary>
    /// <param name="field">Field to be retrieved from</param>
    /// <param name="customType">Custom type to be used for return mapping</param>
    /// <returns></returns>
    public T GetField<T>(string field, CustomType? customType = null)
    {
        if (_isStatic)
        {
            throw new InstanceMemberOnStaticException(field, _info);
        }
        return _info.GetField<T>(_jobj, field, customType ?? TypeMap.MapToType(typeof(T), null), false);
    }

    /// <summary>
    /// Sets the provided value on the backing java object field
    /// </summary>
    /// <param name="field">Field to be set</param>
    /// <param name="value">Value to set</param>
    public void SetField(string field, IBoundObject value)
    {
        if (_isStatic)
        {
            throw new InstanceMemberOnStaticException(field, _info);
        }
        _info.SetField(_jobj, field, new TypedArg(value), false);
    }

    /// <summary>
    /// Sets the provided value on the backing java object field
    /// </summary>
    /// <param name="field">Field to be set</param>
    /// <param name="value">Value to set</param>
    public void SetField(string field, TypedArg value)
    {
        if (_isStatic)
        {
            throw new InstanceMemberOnStaticException(field, _info);
        }
        _info.SetField(_jobj, field, value, false);
    } 
    
    /// <summary>
    /// Sets the provided value on the backing java object field
    /// </summary>
    /// <param name="field">Field to be set</param>
    /// <param name="value">Value to set</param>
    /// <param name="type">Custom type to be used when locating the correct field</param>
    /// <typeparam name="T"></typeparam>
    public void SetField<T>(string field, T value) 
    {
        if (_isStatic)
        {
            throw new InstanceMemberOnStaticException(field, _info);
        }
        _info.SetField(_jobj, field, new TypedArg(value!), false);
    }


    public void Release()
    {
        // Console.WriteLine($"Releasing: [{ClassPath}]{_jobj.HexAddress()}");
        if (_env == IntPtr.Zero || _jobj == IntPtr.Zero)
        {
            return;
        }

        Jvm.ReleaseObj(_jobj);
        _jobj = IntPtr.Zero;
    }

    /// <summary>
    /// Releases the backing java object
    /// </summary>
    public void Dispose()
    {
        Release();
    }

    
    ~BoundObject()
    {
        Release();
    }
}
