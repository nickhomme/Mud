using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Mud.Exceptions;
using Mud.Types;

namespace Mud;

internal class TypeGen
{
    public static Type Build<T>() => Build(typeof(T));

    private static Dictionary<Type, Type> CachedTypes { get; } = new();

    public static Type Build(Type target)
    {
        if (CachedTypes.TryGetValue(target, out var builtType))
        {
            return builtType;
        }


        if (!target.IsInterface)
        {
            throw new InvalidBoundObjectException(target, null);
        }

        var assembly = AssemblyBuilder.DefineDynamicAssembly(new("Mud"), AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Module");
        var type = module.DefineType(target.Name + "_" + target.Name, TypeAttributes.Public, typeof(BoundObject));
        type.AddInterfaceImplementation(target);

        foreach (var p in target.GetProperties())
        {
            GenProp(type, p, target);
        }

        foreach (var m in target.GetMethods())
        {
            var method = type.DefineMethod(
                m.Name,
                MethodAttributes.Public | MethodAttributes.Virtual,
                m.ReturnType,
                m.GetParameters().Select(p => p.ParameterType).ToArray());
            GenMethod(method, m, target);
        }

        builtType = type.CreateType()!;
        CachedTypes[target] = builtType;
        return builtType;
    }


    private static void GenMethod(MethodBuilder method, MethodInfo methodInfo, Type target)
    {
        var returnType = methodInfo.ReturnType;
        var returnCustom = TypeMap.MapToType(returnType, methodInfo.GetCustomAttributes<JavaTypeAttribute>().LastOrDefault()?.ClassPath);

        var isStatic = methodInfo.GetCustomAttributes<JavaStaticAttribute>().LastOrDefault() != null;
        Type targetCls;
        BindingFlags bindingFlags;
        MethodInfo callMethod;
        
        if (isStatic)
        {
            targetCls = typeof(ClassInfo<>).MakeGenericType(target);
            bindingFlags = BindingFlags.NonPublic | BindingFlags.Static;
        }
        else
        {
            targetCls = typeof(BoundObject);
            bindingFlags = BindingFlags.Public | BindingFlags.Instance;
        }
        
        
        if (returnCustom.Type is JavaType.Void)
        {
            callMethod = targetCls.GetMethod("Call", 0, bindingFlags,
                null, new[] { typeof(string), typeof(TypedArg[]) }, null)!;
        }
        else
        {

            callMethod = targetCls
                .GetMethod("Call", 1, bindingFlags, null,
                    new[] { typeof(string), typeof(CustomType), typeof(TypedArg[]) }, null);
            if (callMethod == null)
            {
                _ = 0;
            }
            callMethod = callMethod.MakeGenericMethod(returnType);
        
        }

        var generator = method.GetILGenerator();

        if (!isStatic)
        {
            generator.Emit(OpCodes.Ldarg_0);
        }
        generator.Emit(OpCodes.Ldstr, GetMemberName(methodInfo));

        if (returnCustom.Type is not JavaType.Void)
        {
            EmitCustomType(generator, returnCustom);
        }

        EmitParamsArr(generator, methodInfo);

        generator.Emit(OpCodes.Call, callMethod);
        generator.Emit(OpCodes.Ret);
    }

    private static string GetMemberName(MemberInfo info) => info.GetCustomAttributes<JavaStaticAttribute>().LastOrDefault()?.Name ??
                                                            info.GetCustomAttributes<JavaNameAttribute>().LastOrDefault()?.Name ?? info.Name;

    private static void GenProp(TypeBuilder typeBldr, PropertyInfo propInfo, Type target)
    {
        var name = GetMemberName(propInfo);
        var customType = TypeMap.MapToType(propInfo.PropertyType, propInfo.GetCustomAttributes<JavaTypeAttribute>().LastOrDefault()?.ClassPath);
        
        var property = typeBldr.DefineProperty(propInfo.Name, PropertyAttributes.None, propInfo.PropertyType, Type.EmptyTypes);
        
        var isStatic = propInfo.GetCustomAttributes<JavaStaticAttribute>().LastOrDefault() != null;
        Type targetCls;
        BindingFlags bindingFlags;

        if (isStatic)
        {
            targetCls = typeof(ClassInfo<>).MakeGenericType(target);
            bindingFlags = BindingFlags.NonPublic | BindingFlags.Static;
        }
        else
        {
            targetCls = typeof(BoundObject);
            bindingFlags = BindingFlags.Public | BindingFlags.Instance;
        }
        

        if (propInfo.CanRead)
        {
            var getter = typeBldr.DefineMethod("get_" + propInfo.Name,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, propInfo.PropertyType,
                Type.EmptyTypes);

            var getPropMethod = targetCls.GetMethod("GetField", 1, bindingFlags,
                    null, new[] { typeof(string), typeof(CustomType) }, null)!.MakeGenericMethod(propInfo.PropertyType);

            var getGenerator = getter.GetILGenerator();
            if (!isStatic)
            {
                getGenerator.Emit(OpCodes.Ldarg_0);
            }
            getGenerator.Emit(OpCodes.Ldstr, name);
            EmitCustomType(getGenerator, customType);
            getGenerator.Emit(OpCodes.Call, getPropMethod);
            getGenerator.Emit(OpCodes.Ret);
            
            property.SetGetMethod(getter);
        }

        if (propInfo.CanWrite)
        {
            var setter = typeBldr.DefineMethod("set_" + propInfo.Name,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Virtual, null,
                new[] { propInfo.PropertyType });
            
             var setPropMethod = targetCls.GetMethod("SetField", 0, bindingFlags,
                    null, new[] { typeof(string), typeof(TypedArg), propInfo.PropertyType }, null)!;
            
            
            var setGenerator = setter.GetILGenerator();
            if (!isStatic)
            {
                setGenerator.Emit(OpCodes.Ldarg_0);
            }
            setGenerator.Emit(OpCodes.Ldstr, name);
            setGenerator.Emit(OpCodes.Ldarg_1);
            
            EmitTypedArg(setGenerator, customType);
            
            setGenerator.Emit(OpCodes.Call, setPropMethod);
            setGenerator.Emit(OpCodes.Ret);
            
            property.SetSetMethod(setter);
        }
    }


    private static void EmitTypedArg(ILGenerator generator, CustomType type)
    {
        var ctor = typeof(TypedArg).GetConstructor(new[] { typeof(object), typeof(CustomType) })!;
        EmitCustomType(generator, type);
        generator.Emit(OpCodes.Newobj, ctor);
    }
    
    private static void EmitCustomType(ILGenerator generator, CustomType customType)
    {
        ConstructorInfo ctor;
        if (customType.Type is not JavaType.Object)
        {
            ctor = typeof(CustomType).GetConstructor(new[] { typeof(JavaType) })!;
            EmitInt(generator, (int) customType.Type);
        }
        else if (customType.ArrayOf != null)
        {
            ctor = typeof(CustomType).GetConstructor(new[] { typeof(CustomType) })!;
            EmitCustomType(generator, customType.ArrayOf);
        }
        else
        {
            ctor = typeof(CustomType).GetConstructor(new[] { typeof(string) })!;
            generator.Emit(OpCodes.Ldstr, customType.ClassPath!);
        }
        generator.Emit(OpCodes.Newobj, ctor);
    } 
    
    private static void EmitParamsArr(ILGenerator generator, MethodBase method)
    {
        var parameters = method.GetParameters();
        EmitInt(generator, parameters.Length);
        generator.Emit(OpCodes.Newarr, typeof(TypedArg));

        for (var i = 0; i < parameters.Length; i++)
        {
            generator.Emit(OpCodes.Dup);
            var param = parameters[i];
            // load param index onto stacks
            EmitInt(generator, i);

            // load arg onto stack
            switch (i)
            {
                case 0:
                    generator.Emit(OpCodes.Ldarg_1);
                    break;
                case 1:
                    generator.Emit(OpCodes.Ldarg_2);
                    break;
                case 2:
                    generator.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    generator.Emit(OpCodes.Ldarg, i);
                    break;
            }

            // box the value if the parameter is a value type
            if (param.ParameterType.IsValueType)
            {
                generator.Emit(OpCodes.Box, param.ParameterType);
            }
            var paramCustom = TypeMap.MapToType(param.ParameterType, param.GetCustomAttribute<JavaTypeAttribute>()?.ClassPath);
            EmitTypedArg(generator, paramCustom);

            // set the value into the array
            generator.Emit(OpCodes.Stelem_Ref);
        }
    }
    
    private static void EmitInt(ILGenerator generator, int val)
    {
        if (val > 8)
        {
            generator.Emit(OpCodes.Ldc_I4, val);
        }
        else
        {
#pragma warning disable CS8509
            generator.Emit(val switch
#pragma warning restore CS8509
            {
                0 => OpCodes.Ldc_I4_0,
                1 => OpCodes.Ldc_I4_1,
                2 => OpCodes.Ldc_I4_2,
                3 => OpCodes.Ldc_I4_3,
                4 => OpCodes.Ldc_I4_4,
                5 => OpCodes.Ldc_I4_5,
                6 => OpCodes.Ldc_I4_5,
                7 => OpCodes.Ldc_I4_7,
                8 => OpCodes.Ldc_I4_8,
            });
        }
    }
    
    private static void EmitLog(ILGenerator generator, Type? type = null)
    {
        generator.EmitCall(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new[] { type ?? typeof(object) })!,
            null);
    }

    private static void EmitLog<T>(ILGenerator generator) => EmitLog(generator, typeof(T));

    // private static void EmitLog(ILGenerator generator, int msg)
    // {
    //     generator.Emit(OpCodes.Ldc_I4, msg);
    //     generator.EmitCall(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new[] { typeof(int) })!, null);
    // }
    private static void EmitLog(ILGenerator generator, string msg)
    {
        generator.Emit(OpCodes.Ldstr, msg);
        generator.EmitCall(OpCodes.Call, typeof(Console).GetMethod("WriteLine", new[] { typeof(string) })!, null);
    }
}