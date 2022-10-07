using System.Reflection;
using System.Text;
using Mud.Types;

namespace Mud.Generator;

public static class ReflectParser
{
    public static ReconstructedClass Load(string path)
    {
        using var cls = JavaClass.ForName(path);
        var methods = cls.GetDeclaredMethods();
        var fields = cls.GetDeclaredFields();
        foreach (var javaMethod in methods.Where(m => !m.IsSynthetic))
        {
            Console.WriteLine($"Method: [{javaMethod.GetName()}] -> {javaMethod.GetSignature()}");
        }
        
        foreach (var field in fields.Where(m => !m.IsSynthetic))
        {
            Console.WriteLine($"Field: [{field.GetName()}] -> {field.GetSignature()}");
        }
        return null!;
    }  
}

[ClassPath("java.lang.reflect.Method")]
class JavaMethod : JavaObject
{
    public string GetName() => Call<string>("getName");
    public int GetParameterCount() => Call<int>("getParameterCount");
    public JavaClass GetReturnType() => Call<JavaClass>("getReturnType");
    public JavaClass[] GetParameterTypes() => Call<JavaClass[]>("getParameterTypes");
    
    public bool IsSynthetic  => Call<bool>("isSynthetic");

    public string GetSignature()
    {
        // using var cls = JavaClass<JavaMethod>.Get();
        // using var sigField = cls.Call<JavaField>("getDeclaredField", "signature");
        // sigField.SetAccessible(true);
        var sigObj = GetProp<string?>("signature");
        if (sigObj != null)
        {
            return sigObj;
        }

        var paramTypeCls = GetParameterTypes();
        using var returnTypeCls = GetReturnType();
        var paramsType = string.Join("", paramTypeCls.Select(p => p.TypeSignature));
        Jvm.ReleaseObj(paramTypeCls);
        return $"({paramsType}){returnTypeCls.TypeSignature}";
    }
}

[ClassPath("java.lang.reflect.Field")]
class JavaField : JavaObject
{
    public string GetName() => Call<string>("getName");
    public new JavaClass GetType() => Call<JavaClass>("getType");
    public void SetAccessible(bool acc) => Call("setAccessible", acc);
    
    public bool IsSynthetic  => Call<bool>("isSynthetic");

    public JavaObject? Get(JavaObject obj) => Call<JavaObject?>("get", obj);

    public JavaObject Get(string obj) => Call<JavaObject>("get", obj);
    
    public string GetSignature()
    {
        using var type = GetType();
        return type.GetName();
    }
}

class JavaClass : JavaClass<JavaObject>
{
    public static JavaClass<T> ForName<T>(string name) where T : JavaObject => JavaClass<T>.ForName(name);
    public static JavaClass<T> Get<T>() where T : JavaObject => JavaClass<T>.Get();
    
    public new static JavaClass ForName(string name) => Jvm.GetClassInfo<JavaClass>()
        .CallStaticMethod<JavaClass>("forName", name.Replace('/', '.'));

    public new static JavaClass Get()
    {
        return ForName(typeof(JavaClass).GetCustomAttributes<ClassPathAttribute>().First().ClassPath);
    }
    
    public static class Primitives
    {
        public static JavaClass Int => ForName("int");
        public static JavaClass Long => ForName("long");
        public static JavaClass Bool => ForName("boolean");
        public static JavaClass Byte => ForName("byte");
        public static JavaClass Short => ForName("short");
        public static JavaClass Float => ForName("float");
        public static JavaClass Double => ForName("double");
        public static JavaClass Void => ForName("void");
        public static JavaClass VoidUpper => ForName("Void");

        public static IReadOnlyList<JavaClass> All =>
            new[] { Int, Long, Bool, Byte, Short, Float, Double, Void, VoidUpper }.ToList();
    }
}
[ClassPath("java.lang.Class")]
class JavaClass<T> : JavaObject where T : JavaObject
{
    public static JavaClass<T> ForName(string name) => Jvm.GetClassInfo<JavaClass<T>>()
        .CallStaticMethod<JavaClass<T>>("forName", name.Replace('/', '.'));

    public static JavaClass<T> Get()
    {
        var classPath = typeof(T).GetCustomAttributes<ClassPathAttribute>().FirstOrDefault()?.ClassPath;
        if (string.IsNullOrWhiteSpace(classPath))
        {
            throw new ArgumentException(
                "Missing class path. Make sure the ClassPath attribute is set and has a value");
        }

        return ForName(classPath);
    }
    
    public JavaMethod[] GetDeclaredMethods() => Call<JavaMethod[]>("getDeclaredMethods");
    public JavaField[] GetDeclaredFields() => Call<JavaField[]>("getDeclaredFields");
    public string GetName() => Call<string>("getName");
    public bool IsPrimitive() => Call<bool>("isPrimitive");
    public bool IsArray() => Call<bool>("isArray");

    public string TypeSignature
    {
        get
        {
            if (IsPrimitive())
            {
                return JavaClass.Primitives.All.First(p => p.Equals(this)).Info.TypeSignature;
            }

            var name = GetName().Replace('.', '/');
            return IsArray() ? name : $"L{name};";
        }
        
    }
}