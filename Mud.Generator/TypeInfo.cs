using System.CodeDom;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Mud.Generator;

public class TypeInfoInstance
{
    public TypeInfo? Info { get; }
    public List<TypeInfoInstance> Args { get; init; } = new();
    public string? ForwardedArg { get; }
    public bool IsArray { get; set; }
    public bool IsWildcard { get; set; }

    public TypeInfoInstance(Type type)
    {
        var name = type.GetTypeName();
        switch (type)
        {
            case JavaClass boundClass:
                IsArray = boundClass.IsArray();
                if (IsArray)
                {
                    Args = new()
                    {
                        new (boundClass.GetComponentType())
                    };
                }
                Info = TypeInfo.Get(boundClass);
                return;
            case ParameterizedTypeImpl impl:
            {
                var implCls = impl.GetRawType();

                var argObjs = impl.GetActualTypeArguments();
                var args = argObjs.Select(a => new TypeInfoInstance(a)).ToList();
                Info = TypeInfo.Get(implCls);
                Args = args;
                return;
            }
            case WildcardTypeImpl:
                IsWildcard = true;
                return;
            case TypeVariable typeVar:
                ForwardedArg = typeVar.GetName();
                return;
            case GenericArrayTypeImpl arrayTypeImpl:
                var componentType = arrayTypeImpl.GetGenericComponentType();
                IsArray = true;
                Args = new()
                {
                    new(componentType)
                };
                return;
        }
        throw new NotImplementedException();

    }
}

[SuppressMessage("ReSharper", "BitwiseOperatorOnEnumWithoutFlags")]
public class TypeInfo
{
    public string ClassPath { get; }
    public string Package { get; }
    public string Name { get; }
    public JavaClass Cls { get; }
    public bool IsInterface { get; private set; }
    public bool IsEnum { get; private set; }
    public bool IsPrivate { get; private set; }
    public TypeInfoInstance? Extends { get; private set; }
    public List<TypeInfoInstance> Implements { get; set; } = new();
    public List<TypeParam> Params { get; private set; } = new();
    public TypeAttributes Attributes { get; private set; }
    private bool IsLoaded { get; set; }
    public List<TypeInfo> Nested { get; private set; } = new();
    public static Dictionary<string, TypeInfo> Existing { get; } = new();

    public List<Method> Constructors { get; } = new();
    public List<Method> Methods { get; } = new();
    public List<Field> Fields { get; } = new();
    public List<(string Name, int Value)> Enums { get; } = new();

    private TypeInfo(JavaClass cls, string classPath)
    {
        Cls = cls;
        ClassPath = classPath;
        (Package, Name) = classPath.SplitClassPath();
        Name = Name.Split('$').ElementAtOrDefault(1) ?? Name;
    }

    public static TypeInfo Get(string classPath)
    {
        var cls = JavaClass.ForName(classPath);
        return Get(cls);
    }

    public static TypeInfo GetLoaded(string classPath)
    {
        var nested = Get(classPath);
        nested.Load();
        Console.WriteLine($"Get Loaded {classPath}");
        return nested;
    }
    public static TypeInfo GetLoaded(JavaClass cls)
    {
        var nested = Get(cls);
        nested.Load();
        return nested;
    }
    public static TypeInfo Get(JavaClass cls)
    {
        var classPath = cls.GetName();
        if (classPath.Contains('['))
        {
            // Environment.Exit(9);
        }
        if (!Existing.TryGetValue(classPath, out var info))
        {
            info = new(cls, classPath);
            Existing[classPath] = info;
            info.IsInterface = cls.IsInterface();
            info.IsEnum = cls.IsEnum();
            info.IsPrivate = !Modifier.isPublic(cls.Modifiers);
            var typeParmObjs = cls.GetTypeParameters();
            info.Params = typeParmObjs.Select(p =>
            {
                var bounds = p.GetBounds();
                var boundTypes = bounds.Select(b => new TypeInfoInstance(b)).ToList();
                return new TypeParam(p.GetName(), boundTypes);
            }).ToList();
        } 
        else if (info.Cls != cls)
        {
            Jvm.ReleaseObj(cls);
        }
        return info;
    }

    public void Load()
    {
        if (IsLoaded) return;
        IsLoaded = true;
        Console.WriteLine($"Loading: {ClassPath}");


        if (ClassPath == "java.util.Hashtable$Enumerator")
        {
            var i = 0;
        }
        // if (ClassPath == "java.lang.Class")
        // {
            // Environment.Exit(6);
        // }
        
        
        var clsModifiers = Cls.Modifiers;
        var superClsObj = Cls.GetSuperclass();
        var interfaceClsObjs = Cls.GetGenericInterfaces();
        
        if (superClsObj != null)
        {
            Extends = new(superClsObj);
        }
        Implements = interfaceClsObjs.Select(c => new TypeInfoInstance(c)).ToList();
        
        
        if (!clsModifiers.Has(Modifiers.Public))
        {
            Attributes |= TypeAttributes.NotPublic;
        }

        if (clsModifiers.Has(Modifiers.Abstract))
        {
            Attributes |= TypeAttributes.Abstract;
        }
        if (clsModifiers.Has(Modifiers.Final))
        {
            Attributes |= TypeAttributes.Sealed;
        }
        LoadFields();
        LoadMethods();
        LoadCtors();
        
        var nestedClasses = Cls.GetDeclaredClasses();
        Nested.AddRange(nestedClasses.Select(Get).Where(c => !c.IsPrivate));
    }


    private void LoadMethods()
    {
        var methods = Cls.GetDeclaredMethods();
        foreach (var javaMethod in methods.Where(m => !m.IsSynthetic))
        {
            var paramTypeObjs = javaMethod.GetParameters();
            var exceptionTypeObjs = javaMethod.GetExceptionTypes();
            var returnTypeObj = javaMethod.GetGenericReturnType();
            var modifiers = javaMethod.Modifiers;
            var name = javaMethod.GetName();
            var method = new Method(name,
                javaMethod.GetSignature(), new(returnTypeObj),
                paramTypeObjs.Select(p => new TypeInfoInstance(p.GetParameterizedType())
                ).ToList(), exceptionTypeObjs.Select(e => new TypeInfoInstance(e)).ToList())
            {
                Attributes = GetAttributes(modifiers),
            };

            if (javaMethod.HasGenericInformation)
            {
                method.TypeParams = javaMethod.GetGenericInfo().GetTypeParameters().Select(p =>
                {
                    var bounds = p.GetBounds();
                    var boundTypes = bounds.Select(b => new TypeInfoInstance(b)).ToList();
                    return new TypeParam(p.GetName(), boundTypes);
                }).ToList();
            }
            // if (method.Name == "compareTo" && ClassPath == "java.lang.Comparable")
            // {
            //     var i = 0;
            //     var param = javaMethod.GetParameterTypes()[0];
            //     _ = new TypeInfoInstance(param);
            // }
            
            // Console.WriteLine($"{depth}{indent}\t[{name}]Method: [{method.Name}] -> {method.Signature}");
            if (modifiers.Has(Modifiers.Synchronized))
            {
                method.Synchronized = true;
            }
            if (!modifiers.Has(Modifiers.Public)) continue;
            Methods.Add(method);
        }
    }


    private void LoadFields()
    {
        var fields = Cls.GetDeclaredFields();
        foreach (var javaField in fields.Where(m => !m.IsSynthetic))
        {
            var type = javaField.GetType();
            var modifiers = javaField.Modifiers;
            if (!modifiers.Has(Modifiers.Public)) continue;
            Fields.Add(new Field(javaField.GetName(), javaField.GetSignature(), new(type))
            {
                Attributes = GetAttributes(modifiers)
            });
        }
    }
    
    private void LoadCtors()
    {
        var ctors = Cls.GetDeclaredConstructors();
        foreach (var javaCtor in ctors.Where(m => !m.IsSynthetic))
        {
            var paramTypeObjs = javaCtor.GetParameterTypes();
            var exceptionTypeObjs = javaCtor.GetExceptionTypes();
            var modifiers = javaCtor.Modifiers;
            var ctor = new Method(javaCtor.GetName(), javaCtor.GetSignature(), null,
                paramTypeObjs.Select(p => new TypeInfoInstance(p)
                ).ToList(), exceptionTypeObjs.Select(e => new TypeInfoInstance(e)).ToList())
            {
                Attributes = GetAttributes(modifiers)
            };
            if (modifiers.Has(Modifiers.Synchronized))
            {
                ctor.Synchronized = true;
            }
            if (!modifiers.Has(Modifiers.Public)) continue;
            Constructors.Add(ctor);
        }
    }

    public class TypeParam
    {
        public string Name { get; }
        public List<TypeInfoInstance> Constraints { get; }

        public TypeParam(string name, List<TypeInfoInstance> constraints)
        {
            Name = name;
            Constraints = constraints;
        }
    }

    public class Field
    {
        public string Name { get; }
        public TypeInfoInstance Type { get; }
        public MemberAttributes Attributes { get; set; }
        public string Signature { get; }
        
        public Field(string name, string signature, TypeInfoInstance type)
        {
            Name = name;
            Signature = signature;  
            Type = type;
        }
    }
    
    public class Method 
    {
        public string Name { get; }
        public List<TypeParam> TypeParams { get; set; } = new();
        public List<TypeInfoInstance> Params { get; }
        public TypeInfoInstance? ReturnType { get; }
        public List<TypeInfoInstance> Throws { get; }
        public MemberAttributes Attributes { get; set; }
        public bool Synchronized { get; set; }
        public string Signature { get; set; }

        public Method(string name, string signature, TypeInfoInstance? returnType, List<TypeInfoInstance> @params, List<TypeInfoInstance>? throws = null)
        {
            Name = name;
            ReturnType = returnType;
            Params = @params;
            Throws = throws ?? new();
            Signature = signature;
        }
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
}