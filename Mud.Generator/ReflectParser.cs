using System.CodeDom;
using System.Reflection;
using System.Text;
using Mud.Types;
// ReSharper disable BitwiseOperatorOnEnumWithoutFlags

namespace Mud.Generator;

public static class ReflectParser
{
    private static Dictionary<string, ReconstructedClass> Completed { get; } = new();

    private static ReconstructedClass Load(JavaClass cls, int depth)
    {
        depth += 1;
        var name = cls.GetName();
        if (Completed.TryGetValue(name, out var reconstructed))
        {
            return reconstructed;
        }
        Completed[name] = reconstructed = new ReconstructedClass(cls.GetName())
        {
            IsClass = !cls.IsEnum() && !cls.IsInterface(),
            IsInterface = cls.IsInterface(),
            IsEnum = cls.IsEnum(),
        };
        var indent = "".PadLeft(depth, '\t');
        Console.WriteLine($"{depth}" + indent + cls.GetName());
        
        
        
        var methods = cls.GetDeclaredMethods();
        var fields = cls.GetDeclaredFields();
        var clsModifiers = cls.Modifiers;
        using var superClsObj = cls.GetSuperclass();
        var interfaceClsObjs = cls.GetInterfaces();

        if (superClsObj != null)
        {
            reconstructed.SuperClass = Load(superClsObj, depth);
        }

        reconstructed.Interfaces = interfaceClsObjs.Select(c => Load(c, depth)).ToList();
        
        Jvm.ReleaseObj(interfaceClsObjs);

        var typeParmObjs = cls.GetTypeParameters();

        reconstructed.Params = typeParmObjs.Select(p =>
        {
            
            return new ReconstructedTypeParam()
            {
                Name = p.GetName(),
                Bounds = p.GetBounds().Select(b =>
                {
                    if (b is JavaClass boundClass)
                    {
                        return Load(boundClass, depth);
                    }

                    if (b is ParameterizedTypeImpl impl)
                    {
                        using var implCls = impl.GetRawType();
                        return Load(implCls, depth);
                    }
                    
                    if (b is TypeVariableImpl typeVar)
                    {
                        // using var implCls = typeVar.GetRawType();
                        // return Load(implCls, depth);
                        return null!;
                    }

                    return Load((JavaClass)b, depth);
                }).ToList(),
            };
        }).ToList();
        
        
        Jvm.ReleaseObj(typeParmObjs);
        
        
        if (!clsModifiers.Has(Modifiers.Public))
        {
            reconstructed.Attributes |= TypeAttributes.NotPublic;
        }

        if (clsModifiers.Has(Modifiers.Abstract))
        {
            reconstructed.Attributes |= TypeAttributes.Abstract;
        }
        if (clsModifiers.Has(Modifiers.Final))
        {
            reconstructed.Attributes |= TypeAttributes.Sealed;
        }
        
        
        foreach (var javaMethod in methods.Where(m => !m.IsSynthetic))
        {
            var paramTypeObjs = javaMethod.GetParameterTypes();
            var exceptionTypeObjs = javaMethod.GetExceptionTypes();
            using var returnTypeObj = javaMethod.GetReturnType();
            var modifiers = javaMethod.Modifiers;
            
            var method = new ReconstructedMethod
            {
                Name = javaMethod.GetName(),
                Signature = javaMethod.GetSignature(),
                ParamTypes = paramTypeObjs.Select(TypeFromClass).ToList(),
                ReturnType = TypeFromClass(returnTypeObj),
                Throws = exceptionTypeObjs.Select(TypeFromClass).ToList(),
                Attributes = GetAttributes(modifiers)
            };
            Console.WriteLine($"{depth}{indent}\t[{name}]Method: [{method.Name}] -> {method.Signature}");
            if (modifiers.Has(Modifiers.Synchronized))
            {
                method.Synchronized = true;
            }
            reconstructed.Methods.Add(method);
            Jvm.ReleaseObj(paramTypeObjs);
            Jvm.ReleaseObj(exceptionTypeObjs);
        }
        
        foreach (var javaField in fields.Where(m => !m.IsSynthetic))
        {
            using var type = javaField.GetType();
            var modifiers = javaField.Modifiers;
            var field = new ReconstructedField
            {
                Name = javaField.GetName(),
                Signature = javaField.GetSignature(),
                Type = TypeFromClass(type),
                Attributes = GetAttributes(modifiers)
            };
            
            
            Console.WriteLine($"{depth}{indent}\t[{name}]Field: [{field.Name}] -> {field.Signature}");
        }

        var nestedClasses = cls.GetDeclaredClasses();
        reconstructed.Nested.AddRange(nestedClasses.Select(c => Load(c, depth)));
        Jvm.ReleaseObj(nestedClasses);
        Completed[name] = reconstructed;
        return reconstructed;
    }
    
    
    public static ReconstructedClass Load(string path, int depth = 0)
    {
        using var cls = JavaClass.ForName(path);
        return Load(cls, depth);
    }

    private static MemberAttributes GetAttributes(int modifiers)
    {
        MemberAttributes attributes = 0;
        if (modifiers.Has(Modifiers.Private))
        {
            attributes |= MemberAttributes.Private;
        }
        else if (modifiers.Has(Modifiers.Public))
        {
            attributes |= MemberAttributes.Public;
        }
        else if (modifiers.Has(Modifiers.Protected))
        {
            attributes |= MemberAttributes.Family;
        }
        else
        {
            attributes |= MemberAttributes.Assembly;
        }

        if (modifiers.Has(Modifiers.Static))
        {
            attributes |= MemberAttributes.Static;
        }
        if (modifiers.Has(Modifiers.Abstract))
        {
            attributes |= MemberAttributes.Abstract;
        }
        if (modifiers.Has(Modifiers.Final))
        {
            attributes |= MemberAttributes.Final;
        }

        return attributes;
    } 



    private static JavaFullType TypeFromClass(JavaClass cls)
    {
        if (cls.IsPrimitive())
        {
            var primitive = JavaClass.Primitives.All.First(p => p.Info.ClassPath == cls.Info.ClassPath).GetName();
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
        var name = cls.GetName().Replace('.', '/');
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
class JavaMethod : JavaObject
{
    public string GetName() => Call<string>("getName");
    public int GetParameterCount() => Call<int>("getParameterCount");
    public JavaClass GetReturnType() => Call<JavaClass>("getReturnType");
    public JavaClass[] GetParameterTypes() => Call<JavaClass[]>("getParameterTypes");
    public JavaClass[] GetExceptionTypes() => Call<JavaClass[]>("getExceptionTypes");
    
    public bool IsSynthetic  => Call<bool>("isSynthetic");
    public int Modifiers  => Call<int>("getModifiers");
    
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
    public int Modifiers  => Call<int>("getModifiers");

    public JavaObject? Get(JavaObject obj) => Call<JavaObject?>("get", obj);

    public JavaObject Get(string obj) => Call<JavaObject>("get", obj);
    
    public string GetSignature()
    {
        using var type = GetType();
        return type.TypeSignature;
    }
}

[ClassPath("java.lang.ClassLoader")]
class JavaClassLoader : JavaObject
{
    private static JavaObject? _systemClassLoader;
    public static JavaObject SystemClassLoader => _systemClassLoader ??= Jvm.GetClassInfo("java.lang.ClassLoader").CallStaticMethod<JavaClassLoader>("getSystemClassLoader");
}

class JavaClass : JavaClass<JavaObject>
{
    
    
    public static JavaClass<T> ForName<T>(string name) where T : JavaObject => JavaClass<T>.ForName(name);
    public static JavaClass<T> Get<T>() where T : JavaObject => JavaClass<T>.Get();
    public static JavaClass GetPrimitive(string name) => Jvm.GetClassInfo<JavaClass>()
        .CallStaticMethod<JavaClass>(name == "Void" ? "forName" : "getPrimitiveClass", name.Replace('/', '.'));
    
    public new static JavaClass ForName(string name) => Jvm.GetClassInfo<JavaClass>()
        .CallStaticMethod<JavaClass>("forName", name.Replace('/', '.'), true, JavaClassLoader.SystemClassLoader);

    public static JavaClass Get(string? name = null)
    {
        return ForName(name ?? typeof(JavaClass).GetCustomAttributes<ClassPathAttribute>().First().ClassPath);
    }
    
    public static class Primitives
    {
        public static JavaClass Int => GetPrimitive("int");
        public static JavaClass Long => GetPrimitive("long");
        public static JavaClass Bool => GetPrimitive("boolean");
        public static JavaClass Byte => GetPrimitive("byte");
        public static JavaClass Short => GetPrimitive("short");
        public static JavaClass Float => GetPrimitive("float");
        public static JavaClass Double => GetPrimitive("double");
        public static JavaClass Void => GetPrimitive("void");

        public static IReadOnlyList<JavaClass> All =>
            new[] { Int, Long, Bool, Byte, Short, Float, Double, Void }.ToList();
    }
}
[ClassPath("java.lang.Class")]
class JavaClass<T> : JavaObject, Type where T : JavaObject
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
    public JavaClass GetComponentType() => Call<JavaClass>("getComponentType");
    public JavaClass? GetSuperclass() => Call<JavaClass?>("getSuperclass");
    public TypeVariable[] GetTypeParameters() => Call<TypeVariable[]>("getTypeParameters");
    public JavaClass[] GetInterfaces() => Call<JavaClass[]>("getInterfaces");
    public string GetName() => Call<string>("getName");
    public bool IsPrimitive() => Call<bool>("isPrimitive");
    public bool IsInterface() => Call<bool>("isInterface");
    public bool IsEnum() => Call<bool>("isEnum");
    public bool IsAnnotation() => Call<bool>("isAnnotation");
    public bool IsSynthetic() => Call<bool>("isSynthetic");
    public bool IsArray() => Call<bool>("isArray");
    public int Modifiers  => Call<int>("getModifiers");
    public string GetTypeName() => Call<string>("getTypeName");
    
    public string TypeSignature
    {
        get
        {
            if (IsPrimitive())
            {
                var primitive = JavaClass.Primitives.All.First(p => p.Info.ClassPath == Info.ClassPath).GetName();
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
            var name = GetName().Replace('.', '/');
            if (name == "Void")
            {
                return "V";
            }
            return IsArray() ? name : $"L{name};";
        }
        
    }
}

[ClassPath("java.lang.reflect.AnnotatedType")]
class GenericDeclaration : AnnotatedElement
{
    public TypeVariable[] GetTypeParameters() => Call<TypeVariable[]>("getTypeParameters");
}

[ClassPath("java.lang.reflect.TypeVariable")]
interface TypeVariable : Type
{
    public string GetName() => Call<string>("getName");
    public Type[] GetBounds() => Call<Type[]>("getBounds");
    public GenericDeclaration GetGenericDeclaration() => Call<GenericDeclaration>("getGenericDeclaration");
    public string GetTypeName() => Call<string>("getTypeName");
}

[ClassPath("java.lang.reflect.Type")]
interface Type : IJavaObject
{
    public string GetTypeName();
}

[ClassPath("java.lang.reflect.AnnotatedType")]
class AnnotatedType : AnnotatedElement
{
    public new Type GetType() => Call<Type>("getType");
}



[ClassPath("java.lang.reflect.AnnotatedElement")]
class AnnotatedElement : JavaObject
{
    
}

[ClassPath("sun/reflect/generics/reflectiveObjects/ParameterizedTypeImpl")]
class ParameterizedTypeImpl : JavaObject, Type
{
    public string GetTypeName() => Call<string>("getTypeName");
    public JavaClass GetRawType() => Call<JavaClass>("getRawType");
}

[ClassPath("sun/reflect/generics/reflectiveObjects/TypeVariableImpl")]
class TypeVariableImpl : JavaObject, TypeVariable
{
    public string GetTypeName() => Call<string>("getTypeName");
    public JavaClass GetRawType() => Call<JavaClass>("getRawType");
}