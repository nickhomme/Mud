using System.Data;
using System.Reflection;
using Mud.Exceptions;
using Mud.Types;

namespace Mud;

internal static class TypeMap
{
    /// <summary>
    /// Generates the Java Signature string based off the type provided
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    internal static string GenSignature(CustomType type) => type.TypeSignature;
    
    /// <summary>
    /// Generates the Java Signature string based off the type provided
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    internal static string GenSignature(Type type) => GenSignature(MapToType(type, null));

    /// <summary>
    /// Generates the Java Signature string based off the type of the object provided
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    internal static string GenSignature(IBoundObject obj)
    {
        return obj.Info.TypeSignature;
    }

    /// <summary>
    /// Generates the Java Signature string based off the type of the object provided
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    internal static string GenSignature(TypedArg val)
    {
        if (val.Val is BoundObject boundObject) return GenSignature(boundObject);
        return GenSignature(val.Type);  
    } 

    /// <summary>
    /// Generates the Java Signature string based off the return type and the type of the arguments
    /// </summary>
    /// <param name="returnType"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    internal static string GenMethodSignature(Type? returnType, Type[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){(returnType == null ? "V" : GenSignature(returnType))}";
    }

    /// <summary>
    /// Generates the Java Signature string based off the return type and the type of the arguments
    /// </summary>
    /// <param name="returnType"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static string GenMethodSignature(CustomType? returnType, CustomType[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){(returnType == null ? "V" : GenSignature(returnType))}";
    }
    /// <summary>
    /// Generates the Java Signature string based off the return type and the type of the arguments
    /// </summary>
    /// <param name="returnType"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    internal static string GenMethodSignature(CustomType returnType, TypedArg[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){GenSignature(returnType)}";
    }
    /// <summary>
    /// Generates the Java Signature string based off the return type and the type of the arguments
    /// </summary>
    /// <param name="returnType"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    internal static string GenMethodSignature(Type returnType, TypedArg[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))}){GenSignature(returnType)}";
    }
    
    /// <summary>
    /// Generates the Java Signature string based off the return type and the type of the arguments
    /// </summary>
    /// <param name="returnType"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    internal static string GenMethodSignature(TypedArg[] args)
    {
        return $"({string.Join("", args.Select(GenSignature))})V";
    }


    /// <summary>
    /// Translates the type provided into it's corresponding java type
    /// </summary>
    /// <param name="type"></param>
    /// <param name="classPath"></param>
    /// <returns></returns>
    /// <exception cref="ConstraintException">Throws if there is not any logic in place to handle the mapping of the provided type</exception>
    internal static CustomType MapToType(Type type, string? classPath)
    {
        if (type == typeof(void))
        {
            return new(JavaType.Void);
        }
        if (type == typeof(int))
        {
            return new(JavaType.Int);
        }
        if (type == typeof(byte))
        {
            return new(JavaType.Byte);
        }
        if (type == typeof(bool))
        {
            return new(JavaType.Bool);
        }
        if (type == typeof(float))
        {
            return new(JavaType.Float);
        }
        if (type == typeof(decimal))
        {
            return new(JavaType.Double);
        }
        if (type == typeof(double) || type == typeof(decimal))
        {
            return new(JavaType.Double);
        }
        if (type == typeof(short))
        {
            return new(JavaType.Short);
        }
        if (type == typeof(long))
        {
            return new(JavaType.Long);
        }
        if (type == typeof(char))
        {
            return new(JavaType.Char);
        }


        if (type.IsArray)
        {
            return new CustomType(MapToType(type.GetElementType()!, null));
        }
        
        if (type == typeof(string))
        {
            return new CustomType("java/lang/String");
        }

        classPath ??= type.GetCustomAttributes<ClassPathAttribute>().FirstOrDefault()?.ClassPath ?? type.FullName!.Split('`')[0];

        if (!classPath.StartsWith("System."))
        {
            return new(classPath);
        }

        throw new ConstraintException($"Cannot determine java type for class: {type.FullName ?? type.Name} ");
    }
    
    
    /// <summary>
    /// Maps the JavaVal union to it's respective .NET counterpart
    /// </summary>
    /// <param name="type">Type of the java object</param>
    /// <param name="valType">Desired type to be mapped to</param>
    /// <param name="javaVal">The value to map</param>
    /// <returns></returns>
    /// <exception cref="NoClassMappingException">Throws if the desired type is not assignable to Mud.Types.BoundObject</exception>
    internal static object MapJValue(JavaType type, Type valType, JavaVal javaVal)
    {
        if (type is JavaType.Object)
        {
            if (javaVal.Object == IntPtr.Zero)
            {
                return default!;
            }

            if (valType == typeof(string))
            {
                return Jvm.ExtractStr(javaVal.Object);
            }

            var objCls = Jvm.GetObjClass(javaVal.Object);

            if (valType.IsArray)
            {
                var length = MudInterface.array_length(Jvm.Instance.Env, javaVal.Object);
                var arr = Array.CreateInstance(valType.GetElementType()!, length);
                var arrElemType = MapToType(valType.GetElementType()!, null);
                for (var i = 0; i < length; i++)
                {
                    arr.SetValue(
                        MapJValue(valType.GetElementType()!,
                            MudInterface.array_get_at(Jvm.Instance.Env, javaVal.Object, i, arrElemType.Type)), i);
                }

                return arr;
            }

            var origValType = valType;
            // if it's an interface then we either need to 
            if (valType.IsInterface)
            {
                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes()).Select(t => (t, t.GetCustomAttributes<ClassPathAttribute>().FirstOrDefault())).Where(t => t.Item2 != null)!.ToList<(Type type, ClassPathAttribute Attr)>();
                (valType, _) = types.Find(t =>
                {
                    return objCls.ClassPath == t.Attr.ClassPath;
                });
            }

            // if there is not a class provided with the return class path then we will just return BoundObject if it's possible
            if (valType == null)
            {
                if (!origValType.IsAssignableTo(typeof(BoundObject)))
                {
                    throw new NoClassMappingException(origValType, objCls);
                }

                valType = typeof(BoundObject);
            }

            var jObjVal = Jvm.NewUnboundObj(valType);
            jObjVal.Info = objCls;
            jObjVal.Env = Jvm.Instance.Env;
            jObjVal.Jobj = javaVal.Object;
            Jvm.ObjPointers.Add(javaVal.Object);
            return jObjVal;
        }

        object? val = type switch
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

    /// <summary>
    /// Maps the JavaVal union to it's respective .NET counterpart
    /// </summary>
    /// <param name="valType">Desired type to be mapped to</param>
    /// <param name="javaVal">The value to map</param>
    /// <exception cref="NoClassMappingException">Throws if the desired type is not assignable to Mud.Types.BoundObject</exception>
    internal static object MapJValue(Type valType, JavaVal javaVal)
    {
        return MapJValue(MapToType(valType, null).Type, valType, javaVal);
    }

    /// <summary>
    /// Maps the JavaVal union to it's respective .NET counterpart
    /// </summary>
    /// <param name="type">Type of the java object</param>
    /// <param name="javaVal">The value to map</param>
    /// /// <typeparam name="T">Type to map the value to</typeparam>
    /// <returns></returns>
    /// <exception cref="NoClassMappingException">Throws if the desired type is not assignable to Mud.Types.BoundObject</exception>
    internal static T MapJValue<T>(JavaType type, JavaVal javaVal)
    {
        return (T)MapJValue(type, typeof(T), javaVal);
    }

    /// <summary>
    /// Maps the JavaVal union to it's respective .NET counterpart
    /// </summary>
    /// <param name="javaVal">The value to map</param>
    /// <returns></returns>
    /// <typeparam name="T">Type to map the value to</typeparam>
    /// <exception cref="NoClassMappingException">Throws if the desired type is not assignable to Mud.Types.BoundObject</exception>
    internal static T MapJValue<T>(JavaVal javaVal)
    {
        return (T)MapJValue(typeof(T), javaVal);
    }
}