using System.Reflection;

namespace Mud.Types;

public interface IBoundObject : IDisposable
{

    internal IntPtr Jobj { get; set; }
    internal ClassInfo Info { get; set; }
    internal IntPtr Env { get; set; }
    internal bool IsStatic { get; set; }
    public string ClassPath { get; }
    
    /// <summary>
    /// Checks whether the two java objects are the same instance
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public bool Equals(IBoundObject? obj);

    /// <summary>
    /// Checks whether the object is an instance of a class via classpath
    /// </summary>
    /// <param name="classPath">Class path to check against</param>
    /// <returns></returns>
    public bool InstanceOf(string classPath);

    /// <summary>
    /// Checks whether the object is an instance of a class via ClassInfo
    /// </summary>
    /// <param name="cls">ClassInfo to check against</param>
    /// <returns></returns>
    public bool InstanceOf(ClassInfo cls);

    /// <summary>
    /// Checks whether the object is an instance of another object's class
    /// </summary>
    /// <param name="obj">Bound object to check against</param>
    /// <returns></returns>
    public bool InstanceOf(IBoundObject obj);

    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="args">Arguments to be passed into the method</param>
    public void Call(string method, params object[] args);
    
    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="args">Typed arguments to be passed into the method</param>
    public void Call(string method, TypedArg[] args);

    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="args">Arguments to be passed into the method</param>
    /// <typeparam name="T"></typeparam>
    public T Call<T>(string method, params object[] args);
    
    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="args">Typed arguments to be passed into the method</param>
    /// <typeparam name="T"></typeparam>
    public T Call<T>(string method, TypedArg[] args);


    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="returnType">Custom return type to be used</param>
    /// <param name="args">Arguments to be passed into the method</param>
    public IBoundObject Call(string method, CustomType returnType, params object[] args);
    
    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="returnType">Custom return type to be used</param>
    /// <param name="args">Arguments to be passed into the method</param>
    public IBoundObject Call(string method, CustomType returnType, TypedArg[] args);
    
    /// <summary>
    /// Calls the provided method with args on the backing java object
    /// </summary>
    /// <param name="method">Method to be called</param>
    /// <param name="returnType">Custom return type to be used</param>
    /// <param name="args">Arguments to be passed into the method</param>
    public T Call<T>(string method, CustomType returnType, TypedArg[] args);

    /// <summary>
    /// Gets the provided field on the backing java object
    /// </summary>
    /// <param name="field">Field to be retrieved from</param>
    /// <param name="type">Custom type to be used when locating the correct field</param>
    /// <returns></returns>
    public IBoundObject GetField(string field, CustomType type);

    /// <summary>
    /// Gets the provided field on the backing java object
    /// </summary>
    /// <param name="field">Field to be retrieved from</param>
    /// <param name="customType">Custom type used for mapping</param>
    /// <returns></returns>
    public T GetField<T>(string field, CustomType? customType = null);

    /// <summary>
    /// Sets the provided value on the backing java object field
    /// </summary>
    /// <param name="field">Field to be set</param>
    /// <param name="value">Value to set</param>
    public void SetField(string field, IBoundObject value);

    /// <summary>
    /// Sets the provided value on the backing java object field
    /// </summary>
    /// <param name="field">Field to be set</param>
    /// <param name="value">Value to set</param>
    /// <param name="customType"></param>
    public void SetField(string field, TypedArg value);

    /// <summary>
    /// Sets the provided value on the backing java object field
    /// </summary>
    /// <param name="field">Field to be set</param>
    /// <param name="value">Value to set</param>
    /// <param name="type">Custom type to be used when locating the correct field</param>
    /// <typeparam name="T"></typeparam>
    public void SetField<T>(string field, T value);


    public void Release();

    internal void Bind(TypedArg[] args);
}