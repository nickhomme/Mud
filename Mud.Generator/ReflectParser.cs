using System.CodeDom;
using System.Reflection;
using System.Text;
using Mud.Types;

// ReSharper disable BitwiseOperatorOnEnumWithoutFlags

namespace Mud.Generator;

public static class ReflectParser
{
    private static Dictionary<string, ReconstructedClass> Completed { get; } = new();

    public static JavaFullType TypeFromClass(JavaClass cls)
    {
        var name = cls.GetName().Replace('.', '/');
        if (cls.IsPrimitive())
        {
            var primitives = JavaClass.Primitives.All.Select(p => p.GetName()).ToArray();
            var primitive = primitives.First(p => p == name);
            return primitive switch
            {
                "int" => new JavaFullType(JavaType.Int),
                "long" => new JavaFullType(JavaType.Long),
                "boolean" => new JavaFullType(JavaType.Bool),
                "byte" => new JavaFullType(JavaType.Byte),
                "short" => new JavaFullType(JavaType.Short),
                "char" => new JavaFullType(JavaType.Char),
                "float" => new JavaFullType(JavaType.Float),
                "double" => new JavaFullType(JavaType.Double),
                "void" or "Void" => new JavaFullType(JavaType.Void),
            };
        }

        if (cls.IsArray())
        {
            using var elemType = cls.GetComponentType();
            return new JavaFullType(JavaType.Object)
            {
                ArrayType = TypeFromClass(elemType)
            };
        }

        if (name == "Void")
        {
            return new(JavaType.Void);
        }

        return new(name);
    }
}

static class IntModifierExtension
{
    public static bool Has(this int flags, Modifiers modifier)
    {
        return (flags & (int)modifier) == (int)modifier;
    }
}

internal enum Modifiers
{
    Public = 0x1,
    Private = 0x2,
    Protected = 0x4,
    Static = 0x8,
    Final = 0x10,
    Synchronized = 0x20,

    // ReSharper disable InconsistentNaming
    Volatile_Bridge = 0x40,
    Transient_Varargs = 0x80,
    // ReSharper restore InconsistentNaming
    Native = 0x100,
    Interface = 0x200,
    Abstract = 0x400,
    Synthetic = 0x1000,
    Annotation = 0x2000,
    Enum = 0x4000,
    Mandated = 0x8000,
}

[ClassPath("java.lang.reflect.Method")]
public class JavaMethod : JavaObject
{
    public string GetName() => Call<string>("getName");
    public int GetParameterCount() => Call<int>("getParameterCount");
    public Type GetGenericReturnType() => Call<Type>("getGenericReturnType");
    public JavaClass GetReturnType() => Call<JavaClass>("getReturnType");
    public JavaParameter[] GetParameters() => Call<JavaParameter[]>("getParameters");
    public JavaClass[] GetParameterTypes() => Call<JavaClass[]>("getParameterTypes");
    public JavaClass[] GetExceptionTypes() => Call<JavaClass[]>("getExceptionTypes");
    public MethodRepository GetGenericInfo() => Call<MethodRepository>("getGenericInfo");

    public bool HasGenericInformation => Call<bool>("hasGenericInformation");
    public bool IsSynthetic => Call<bool>("isSynthetic");
    public int Modifiers => Call<int>("getModifiers");

    public string GetSignature()
    {
        // using var cls = JavaClass<JavaMethod>.Get();
        // using var sigField = cls.Call<JavaField>("getDeclaredField", "signature");
        // sigField.SetAccessible(true);
        // var sigObj = GetProp<string?>("signature");
        // if (sigObj != null)
        // {
        //     return sigObj;
        // }

        var paramTypeCls = GetParameterTypes();
        using var returnTypeCls = GetReturnType();
        var paramsType = string.Join("", paramTypeCls.Select(p => p.TypeSignature));
        Jvm.ReleaseObj(paramTypeCls);
        return $"({paramsType}){returnTypeCls.TypeSignature}";
    }
}

[ClassPath("java.lang.reflect.Parameter")]
public class JavaParameter : JavaObject
{
    public Type GetParameterizedType() => Call<Type>("getParameterizedType");
}

[ClassPath("java.lang.reflect.Constructor")]
public class JavaConstructor : JavaObject
{
    public string GetName() => Call<string>("getName");
    public int GetParameterCount() => Call<int>("getParameterCount");
    public JavaParameter[] GetParameters() => Call<JavaParameter[]>("getParameters");
    public JavaClass[] GetParameterTypes() => Call<JavaClass[]>("getParameterTypes");
    public JavaClass[] GetExceptionTypes() => Call<JavaClass[]>("getExceptionTypes");

    public bool IsSynthetic => Call<bool>("isSynthetic");
    public int Modifiers => Call<int>("getModifiers");

    public string GetSignature()
    {
        var sigObj = GetProp<string?>("signature");
        if (sigObj != null)
        {
            return sigObj;
        }

        var paramTypeCls = GetParameterTypes();
        var paramsType = string.Join("", paramTypeCls.Select(p => p.TypeSignature));
        Jvm.ReleaseObj(paramTypeCls);
        return $"({paramsType})V";
    }
}

[ClassPath("java.lang.reflect.Field")]
public class JavaField : JavaObject
{
    public string GetName() => Call<string>("getName");
    public new JavaClass GetType() => Call<JavaClass>("getType");
    public void SetAccessible(bool acc) => Call("setAccessible", acc);

    public bool IsSynthetic => Call<bool>("isSynthetic");
    public int Modifiers => Call<int>("getModifiers");

    public JavaObject? Get(JavaObject obj) => Call<JavaObject?>("get", obj);

    public JavaObject Get(string obj) => Call<JavaObject>("get", obj);

    public string GetSignature()
    {
        using var type = GetType();
        return type.TypeSignature;
    }
}

[ClassPath("java.lang.ClassLoader")]
public class JavaClassLoader : JavaObject
{
    private static JavaObject? _systemClassLoader;

    public static JavaObject SystemClassLoader => _systemClassLoader ??= Jvm.GetClassInfo("java.lang.ClassLoader")
        .CallStaticMethod<JavaClassLoader>("getSystemClassLoader");
}

public class JavaClass : JavaClass<JavaObject>
{
    public static JavaClass<T> ForName<T>(string name) where T : JavaObject => JavaClass<T>.ForName(name);
    public static JavaClass<T> Get<T>() where T : JavaObject => JavaClass<T>.Get();

    public static JavaClass GetPrimitive(string name) => Jvm.GetClassInfo<JavaClass>()
        .CallStaticMethod<JavaClass>(name == "Void" ? "forName" : "getPrimitiveClass", name.Replace('/', '.'));

    public new static JavaClass ForName(string name) => Jvm.GetClassInfo<JavaClass>()
        .CallStaticMethod<JavaClass>("forName", name.Replace('/', '.'), false, JavaClassLoader.SystemClassLoader);

    public static JavaClass Get(string? name = null)
    {
        return ForName(name ?? typeof(JavaClass).GetCustomAttributes<ClassPathAttribute>().First().ClassPath);
    }

    public static class Primitives
    {
        public static JavaClass Int => GetPrimitive("int");
        public static JavaClass Long => GetPrimitive("long");
        public static JavaClass Char => GetPrimitive("char");
        public static JavaClass Bool => GetPrimitive("boolean");
        public static JavaClass Byte => GetPrimitive("byte");
        public static JavaClass Short => GetPrimitive("short");
        public static JavaClass Float => GetPrimitive("float");
        public static JavaClass Double => GetPrimitive("double");
        public static JavaClass Void => GetPrimitive("void");

        public static IReadOnlyList<JavaClass> All =>
            new[] { Int, Long, Char, Bool, Byte, Short, Float, Double, Void }.ToList();
    }
}

[ClassPath("java.lang.Class")]
public class JavaClass<T> : JavaObject, Type where T : JavaObject
{
    public static JavaClass<T> ForName(string name) => Jvm.GetClassInfo<JavaClass<T>>()
        .CallStaticMethod<JavaClass<T>>("forName", name.Replace('/', '.'), true, JavaClassLoader.SystemClassLoader);

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

    public JavaClass[] GetDeclaredClasses() => Call<JavaClass[]>("getDeclaredClasses");
    public JavaMethod[] GetDeclaredMethods() => Call<JavaMethod[]>("getDeclaredMethods");
    public JavaField[] GetDeclaredFields() => Call<JavaField[]>("getDeclaredFields");
    public JavaConstructor[] GetDeclaredConstructors() => Call<JavaConstructor[]>("getDeclaredConstructors");
    public JavaClass GetComponentType() => Call<JavaClass>("getComponentType");
    public JavaClass? GetSuperclass() => Call<JavaClass?>("getSuperclass");
    public TypeVariable[] GetTypeParameters() => Call<TypeVariable[]>("getTypeParameters");
    public JavaClass[] GetInterfaces() => Call<JavaClass[]>("getInterfaces");
    public Type[] GetGenericInterfaces() => Call<Type[]>("getGenericInterfaces");
    public Type[] getEnumConstants() => Call<Type[]>("getGenericInterfaces");
    public string GetName() => Call<string>("getName");
    public bool IsPrimitive() => Call<bool>("isPrimitive");
    public bool IsInterface() => Call<bool>("isInterface");
    public bool IsEnum() => Call<bool>("isEnum");
    public bool IsAnnotation() => Call<bool>("isAnnotation");
    public bool IsSynthetic() => Call<bool>("isSynthetic");
    public bool IsArray() => Call<bool>("isArray");
    public int Modifiers => Call<int>("getModifiers");
    public string GetTypeName() => Call<string>("getTypeName");

    public string TypeSignature
    {
        get
        {
            var name = GetName().Replace('.', '/');
            if (IsPrimitive())
            {
                var primitives = JavaClass.Primitives.All.Select(p => p.GetName()).ToArray();
                var primitive = primitives.First(p => p == name);
                return primitive switch
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
                };
            }

            if (name == "Void")
            {
                return "V";
            }

            return IsArray() ? name : $"L{name};";
        }
    }
}

[ClassPath("java.lang.reflect.AnnotatedType")]
public class GenericDeclaration : AnnotatedElement
{
    public TypeVariable[] GetTypeParameters() => Call<TypeVariable[]>("getTypeParameters");
}

[ClassPath("java.lang.reflect.TypeVariable")]
public interface TypeVariable : Type
{
    public string GetName() => Call<string>("getName");
    public Type[] GetBounds() => Call<Type[]>("getBounds");
}

[ClassPath("java.lang.reflect.Type")]
public interface Type : IJavaObject
{
    public string GetTypeName() => Call<string>("getTypeName");
}

[ClassPath("java.lang.reflect.AnnotatedType")]
public class AnnotatedType : AnnotatedElement
{
    public new Type GetType() => Call<Type>("getType");
}

[ClassPath("java.lang.reflect.AnnotatedElement")]
public class AnnotatedElement : JavaObject
{
}

[ClassPath("sun/reflect/generics/reflectiveObjects/ParameterizedTypeImpl")]
public class ParameterizedTypeImpl : JavaObject, Type
{
    public JavaClass GetRawType() => Call<JavaClass>("getRawType");
    public Type[] GetActualTypeArguments() => Call<Type[]>("getActualTypeArguments");
}

[ClassPath("sun/reflect/generics/reflectiveObjects/TypeVariableImpl")]
public class TypeVariableImpl : JavaObject, TypeVariable
{
}

[ClassPath("sun/reflect/generics/reflectiveObjects/WildcardTypeImpl")]
public class WildcardTypeImpl : JavaObject, Type
{
    public Type[] GetUpperBounds() => Call<Type[]>("getUpperBounds");
}

[ClassPath("sun/reflect/generics/reflectiveObjects/GenericArrayTypeImpl")]
public class GenericArrayTypeImpl : JavaObject, Type
{
    public Type GetGenericComponentType() => Call<Type>("getGenericComponentType");
}

[ClassPath("sun.reflect.generics.repository.MethodRepository")]
public class MethodRepository : JavaObject
{
    public TypeVariable[] GetTypeParameters() => Call<TypeVariable[]>("getTypeParameters");
}


[ClassPath("java.lang.reflect.Modifier")]
public abstract class Modifier : JavaObject
{
    public static int PUBLIC
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("PUBLIC"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("PUBLIC", value); }
    }

    public static int PRIVATE
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("PRIVATE"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("PRIVATE", value); }
    }

    public static int PROTECTED
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("PROTECTED"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("PROTECTED", value); }
    }

    public static int STATIC
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("STATIC"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("STATIC", value); }
    }

    public static int FINAL
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("FINAL"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("FINAL", value); }
    }

    public static int SYNCHRONIZED
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("SYNCHRONIZED"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("SYNCHRONIZED", value); }
    }

    public static int VOLATILE
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("VOLATILE"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("VOLATILE", value); }
    }

    public static int TRANSIENT
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("TRANSIENT"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("TRANSIENT", value); }
    }

    public static int NATIVE
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("NATIVE"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("NATIVE", value); }
    }

    public static int INTERFACE
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("INTERFACE"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("INTERFACE", value); }
    }

    public static int ABSTRACT
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("ABSTRACT"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("ABSTRACT", value); }
    }

    public static int STRICT
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("STRICT"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("STRICT", value); }
    }

    internal static int BRIDGE
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("BRIDGE"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("BRIDGE", value); }
    }

    internal static int VARARGS
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("VARARGS"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("VARARGS", value); }
    }

    internal static int SYNTHETIC
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("SYNTHETIC"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("SYNTHETIC", value); }
    }

    internal static int ANNOTATION
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("ANNOTATION"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("ANNOTATION", value); }
    }

    internal static int ENUM
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("ENUM"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("ENUM", value); }
    }

    internal static int MANDATED
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("MANDATED"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("MANDATED", value); }
    }

    internal static int ACCESS_MODIFIERS
    {
        get { return Jvm.GetClassInfo("java.lang.reflect.Modifier").GetStaticField<int>("ACCESS_MODIFIERS"); }
        set { Jvm.GetClassInfo("java.lang.reflect.Modifier").SetStaticField<int>("ACCESS_MODIFIERS", value); }
    }

    public new static Modifier @New()
    {
        return Jvm.NewObj<Modifier>();
    }

    public static string toString(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<string>("toString", param_0);
    }

    public static bool isInterface(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isInterface", param_0);
    }

    internal static bool isSynthetic(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isSynthetic", param_0);
    }

    public static int classModifiers()
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<int>("classModifiers");
    }

    public static bool isAbstract(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isAbstract", param_0);
    }

    public static bool isStatic(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isStatic", param_0);
    }

    public static bool isProtected(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isProtected", param_0);
    }

    internal static bool isMandated(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isMandated", param_0);
    }

    public static int methodModifiers()
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<int>("methodModifiers");
    }

    public static int constructorModifiers()
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<int>("constructorModifiers");
    }

    public static bool isFinal(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isFinal", param_0);
    }

    public static bool isPublic(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isPublic", param_0);
    }

    public static bool isVolatile(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isVolatile", param_0);
    }

    public static bool isPrivate(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isPrivate", param_0);
    }

    public static bool isNative(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isNative", param_0);
    }

    public static bool isSynchronized(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isSynchronized", param_0);
    }

    public static bool isTransient(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isTransient", param_0);
    }

    public static bool isStrict(int param_0)
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<bool>("isStrict", param_0);
    }

    public static int interfaceModifiers()
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<int>("interfaceModifiers");
    }

    public static int fieldModifiers()
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<int>("fieldModifiers");
    }

    public static int parameterModifiers()
    {
        return Jvm.GetClassInfo("java.lang.reflect.Modifier").CallStaticMethod<int>("parameterModifiers");
    }
}