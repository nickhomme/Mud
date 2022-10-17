using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CSharp;
using Mud.Types;

namespace Mud.Generator;

public class Writer
{
    private readonly CSharpCodeProvider _codeGen = new();
    private readonly CodeNamespace _codeNamespace;
    private readonly CodeCompileUnit _codeUnit = new();
    public HashSet<string> PackageUsages { get; } = new();
    public HashSet<string> ClassesUsed { get; } = new();
    private Dictionary<string, string> NameMappings { get; } = new();
    public static HashSet<string> Written { get; } = new();

    public Writer(TypeInfo cls)
    {
        Written.Add(cls.ClassPath);
        if (cls.Package.Contains('['))
        {
            Environment.Exit(6);
        }
        _codeNamespace = new(EscapedIdent(cls.Package));
        Load(null, cls);
    }

    private string EscapedPath(string path) => string.Join('.', path.Split('.').Select(EscapedIdent));
    private string EscapedIdent(string ident) => _codeGen.CreateEscapedIdentifier(ident);

    public string[] UsedClassPaths => ClassesUsed.ToArray();
    
    private void Load(CodeTypeDeclaration? parent, TypeInfo target)
    {

        target.Load();
        var codeType = new CodeTypeDeclaration(EscapedIdent(target.Name))
        {
            IsClass = !target.IsInterface && !target.IsEnum,
            IsInterface = target.IsInterface,
            IsEnum = target.IsEnum
        };
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        parent?.Members.Add(codeType);
        if (parent == null)
        {
            _codeNamespace.Types.Add(codeType);
        }
        
        codeType.TypeAttributes |= TypeAttributes.Abstract;

        codeType.CustomAttributes.Add(new("ClassPath",
            new CodeAttributeArgument(new CodePrimitiveExpression(EscapedPath(target.ClassPath)))));
        // _codeType.CustomAttributes.Add(new("TypeSignature",
        // new CodeAttributeArgument(new CodePrimitiveExpression($"{_cls.ClassPath}"))));

        if (target.IsEnum)
        {
            codeType.BaseTypes.Add(typeof(long));
        } 
        else if (target.Extends != null)
        {
            codeType.BaseTypes.Add(JavaTypeRefStr(target.Extends));
        } 
        else if (target.Implements.Count == 0)
        {
            // codeType.BaseTypes.Add(target.IsInterface ? typeof(IJavaObject) : typeof(JavaObject));
        }
        target.Implements.ForEach(iface =>
        {
            codeType.BaseTypes.Add(JavaTypeRefStr(iface));
        });


        if (target.ClassPath == "java.lang.Object")
        {
            codeType.BaseTypes.Add(typeof(JavaObject));
        }

        // ReSharper disable CoVariantArrayConversion
        codeType.Members.AddRange(LoadFields(target));
        codeType.Members.AddRange(LoadConstructors(target));
        codeType.Members.AddRange(LoadMethods(target));
        // ReSharper restore CoVariantArrayConversion
        _codeNamespace.Imports.Add(new("Mud"));
        _codeNamespace.Imports.Add(new("Mud.Types"));
        _codeNamespace.Imports.AddRange(PackageUsages.Where(p => p != target.Package)
            .Select(u => new CodeNamespaceImport(EscapedPath(u))).ToArray());



        target.Params.ForEach(p =>
        {
            var param = new CodeTypeParameter(p.Name);
            p.Constraints.ForEach(c =>
            {
                var typeStr = JavaTypeRefStr(c);
                if (typeStr == "java.lang.Object")
                {
                    return;
                }
                param.Constraints.Add(new CodeTypeReference(typeStr));
            });
            codeType.TypeParameters.Add(param);
        });

        // if (target.Params.Any())
        // {
        //     var nonArgType = new CodeTypeDeclaration(EscapedIdent(target.Name))
        //     {
        //         IsClass = codeType.IsClass,
        //         IsInterface = codeType.IsInterface,
        //         IsEnum = codeType.IsEnum,
        //         Attributes = codeType.Attributes,
        //         TypeAttributes = codeType.TypeAttributes
        //     };
        //     _codeNamespace.Types.Add(nonArgType);
        //     nonArgType.BaseTypes.Add(JavaTypeRef(new TypeInfoInstance(target.Cls)));
        // }
        
        
        
        target.Nested.ForEach(n => Load(codeType, n));
    }
    
    
    
    public void WriteTo(FileInfo path) =>
        WriteTo(path.FullName);

    public void WriteTo(string path)
    {
        using var fs = File.Create(path);
        using var stream = new StreamWriter(fs);
        WriteTo(stream);
    }

    public void WriteTo(TextWriter stream)
    {
        _codeUnit.Namespaces.Add(_codeNamespace);

        using var tw1 = new IndentedTextWriter(stream, "    ");
        _codeGen.GenerateCodeFromCompileUnit(_codeUnit, tw1, new CodeGeneratorOptions());
        tw1.Close();
    }


    private CodeTypeReference JavaTypeRef(TypeInfoInstance? type)
    {
        if (type == null) return new(typeof(void));
        if (type.IsArray)
        {
            return new(JavaTypeRef(type.Args[0]), 1);
        }
        if (type.ForwardedArg != null) return new(type.ForwardedArg);
        if (type.Info == null) return new CodeTypeReference("java.lang.Object");
        if (type.Info.Cls.IsPrimitive())
        {
            return JavaTypeRef(ReflectParser.TypeFromClass(type.Info.Cls));
        }

        if (type.Info.Cls.IsArray())
        {
            return new(JavaTypeRef(new TypeInfoInstance(type.Info.Cls.GetComponentType())), 1);
        }


        var argsStrings = type.Info.Params.Select((p, i) =>
        {
            var arg = type.Args.ElementAtOrDefault(i);
            var param = p.Constraints.ElementAtOrDefault(0);
            string paramStr;
            if (arg?.IsWildcard ?? false)
            {
                paramStr = JavaTypeRefStr(p.Constraints.ElementAtOrDefault(0));    
            }
            else
            {
                paramStr = JavaTypeRefStr(arg ?? p.Constraints.ElementAtOrDefault(0));
            }
            
            if (paramStr == "java.lang.Object")
            {
                paramStr = "object";
                // paramStr = "Mud.Types.IJavaObject";
            }

            return paramStr;
        });
        
        if (type.Info.ClassPath == "java.lang.String") return new(typeof(string));
        var str = JavaTypeRefStr(type.Info.ClassPath);
        // if (str == "Class")
        // {
        //     Environment.Exit(5);
        // }
        // var argsStrings = type.Args.Select((a, i) =>
        // {
        //     if (a.Info != null)
        //     {
        //         return JavaTypeRefStr(a);
        //     }
        //     return JavaTypeRefStr(type.Info.Params[i].Constraints.ElementAtOrDefault(0) ?? a);
        // });
        
        if (type.Info.Params.Count != 0)
        {
            str = $"{str}<{string.Join(", ", argsStrings)}>";
        }

        return new(str);
    }
    
    private CodeTypeReference JavaTypeRef(JavaFullType type)
    {
        if (type.Type is JavaType.Object)
        {
            if (type.ArrayType != null)
            {
                return new CodeTypeReference(JavaTypeRef(type.ArrayType), 1);
            }

            var classPath = type.CustomType!.Replace('/', '.');
            if (classPath == "java.lang.String") return new(typeof(string));
            var (package, name) = classPath.SplitClassPath();
            PackageUsages.Add(package);
            
            ClassesUsed.Add(classPath.Split('$')[0]);
            if (ClassesUsed.Any(u => u.Contains('[')))
            {
                Environment.Exit(1);
            }

            static bool IsDotNetType(string name)
            {
                return SystemTypes.Contains(name);
            }

            // if this is a dotnet type then we have to use full path
            if (IsDotNetType(name))
            {
                return new(EscapedPath(classPath.Replace('$', '.')));
            }

            // Check to see if this name is already being used. If it is and the classpath differs then we
            //      must use the full classpath for this instance to avoid collision;
            if (NameMappings.TryGetValue(name!, out var existingClassPath))
            {
                if (existingClassPath != classPath)
                {
                    return new(EscapedPath(classPath.Replace('$', '.')));
                }
            }
            else
            {
                NameMappings[name] = classPath;
            }

            return new(EscapedPath(name.Replace('$', '.')));
        }


        return new(type.Type switch
        {
            JavaType.Int => typeof(int),
            JavaType.Bool => typeof(bool),
            JavaType.Byte => typeof(byte),
            JavaType.Char => typeof(char),
            JavaType.Short => typeof(short),
            JavaType.Long => typeof(long),
            JavaType.Float => typeof(float),
            JavaType.Double => typeof(double),
            JavaType.Void => typeof(void),
            _ => throw new ArgumentOutOfRangeException()
        });
    }


    private static string[] SystemTypes { get; } =  AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()
        .Where(t => t.Namespace?.Split('.')[0] == "System")).Select(t => t.Name.Split('`')[0]).ToArray();

    private string JavaTypeRefStr(string classPath)
    {
        classPath = classPath.Replace('/', '.');
        if (classPath == "java.lang.String") return "string";
        ClassesUsed.Add(classPath.Split('$')[0]);
        if (ClassesUsed.Any(u => u.Contains('[')))
        {
            Environment.Exit(2);
        }
        var (package, name) = classPath.SplitClassPath();
        PackageUsages.Add(package);

        static bool IsDotNetType(string name)
        {
            return SystemTypes.Contains(name);
        }

        
        // if this is a dotnet type then we have to use full path
        if (IsDotNetType(name))
        {
            return EscapedPath(classPath.Replace('$', '.'));
        }

        // Check to see if this name is already being used. If it is and the classpath differs then we
        //      must use the full classpath for this instance to avoid collision;
        if (NameMappings.TryGetValue(name!, out var existingClassPath))
        {
            if (existingClassPath != classPath)
            {
                return EscapedPath(classPath.Replace('$', '.'));
            }
        }
        else
        {
            NameMappings[name] = classPath;
        }

        return EscapedPath(name.Replace('$', '.'));
    }
    
    private string JavaTypeRefStr(JavaFullType type)
    {
        if (type.Type is JavaType.Object)
        {
            if (type.ArrayType != null)
            {
                return $"{JavaTypeRefStr(type.ArrayType)}[]";
            }
            return JavaTypeRefStr(type.CustomType!);
        }


        return new(type.Type switch
        {
            JavaType.Int => "int",
            JavaType.Bool => "bool",
            JavaType.Byte => "byte",
            JavaType.Char => "char",
            JavaType.Short => "short",
            JavaType.Long => "long",
            JavaType.Float => "float",
            JavaType.Double => "double",
            JavaType.Void => "void",
            _ => throw new ArgumentOutOfRangeException()
        });
    }


    private string JavaTypeRefStr(TypeInfoInstance? type)
    {
        if (type == null) return "java.lang.Object";
        if (type.IsArray)
        {
            return $"{JavaTypeRefStr(type.Args[0])}[]";
        }
        if (type.ForwardedArg != null) return type.ForwardedArg;
        if (type.Info == null) return "java.lang.Object";


        var fullType = ReflectParser.TypeFromClass(type.Info.Cls);
        if (fullType.Type != JavaType.Object)
        {
            return JavaTypeRefStr(fullType);
        }
        
        var typeStr = $"{JavaTypeRefStr(type.Info.ClassPath)}";

        if (typeStr == "java.lang.String") return "string";
        
        if (!type.Info.Params.Any())
        {
            return typeStr;
        }

        if (typeStr == "Spliterator.OfPrimitive")
        {
            var i = 0;
        }

        var argsStrings = type.Info.Params.Select((p, i) =>
        {
            var arg = type.Args.ElementAtOrDefault(i);
            var param = JavaTypeRefStr(arg ?? p.Constraints.ElementAtOrDefault(0));
            if (param == "java.lang.Object")
            {
                param = "object";
                // param = "Mud.Types.IJavaObject";
            }

            return param;
        });
        // var argsStrings = type.Args.Select((a, i) =>
        // {
        //     if (a.Info != null)
        //     {
        //         return JavaTypeRefStr(a);
        //     }
        //     return JavaTypeRefStr(type.Info.Params[i].Constraints.ElementAtOrDefault(0) ?? a);
        // });
        
        return $"{typeStr}<{string.Join(", ", argsStrings)}>";
    }

    private CodeTypeMember[] LoadFields(TypeInfo cls)
    {

        if (cls.IsEnum)
        {
            // ReSharper disable once CoVariantArrayConversion
            return cls.Fields.Select(f => new CodeMemberField(cls.Name, f.Name)).ToArray();
        }        
        
        
        // abstract classes don't have to fully implement interface but they do in c# so we need retrieve the missing ones and apply them here
        var fields = cls.Fields.ToList();
        if (cls.Attributes.HasFlag(TypeAttributes.Abstract))
        {
            var implmentsMethods = cls.Implements.SelectMany(iface =>
            {
                iface.Info.Load();
                return iface.Info.Fields;
            }).Distinct(new FieldEqualityComparer()).Where(m => !cls.Fields.Any(m2 => m.Name == m2.Name && m.Signature == m2.Signature));
            
            fields.AddRange(implmentsMethods);
            
            var i = 0;

        }

        // ReSharper disable once CoVariantArrayConversion
        return fields.Select(field =>
        {
            // Console.WriteLine($"[{field.Name}] -> {field.Signature}");
            var type = JavaTypeRef(field.Type);
            var codeProp = new CodeMemberProperty();
            codeProp.Name = EscapedIdent(field.Name);
            codeProp.Type = type;

            CodeStatement getForward;
            CodeStatement setForward;

            if (field.Attributes.HasFlag(MemberAttributes.Static))
            {
                getForward = new CodeMethodReturnStatement()
                {
                    Expression = new CodeSnippetExpression(
                        $"Jvm.GetClassInfo(\"{cls.ClassPath}\").GetStaticField<{JavaTypeRefStr(field.Type)}>(\"{field.Name}\")")
                };

                setForward = new CodeExpressionStatement(new CodeSnippetExpression(
                    $"Jvm.GetClassInfo(\"{cls.ClassPath}\").SetStaticField<{JavaTypeRefStr(field.Type)}>(\"{field.Name}\", value)"));
            }
            else
            {
                getForward = new CodeMethodReturnStatement
                {
                    Expression = new CodeMethodInvokeExpression(new(new CodeThisReferenceExpression(), "GetProp",
                        type), new CodePrimitiveExpression(field.Name))
                };

                setForward = new CodeExpressionStatement(new CodeMethodInvokeExpression(new(
                    new CodeThisReferenceExpression(), "SetProp",
                    type), new CodePrimitiveExpression(field.Name), new CodeVariableReferenceExpression("value")));
            }


            codeProp.GetStatements.Add(getForward);
            codeProp.SetStatements.Add(setForward);
            codeProp.Attributes = field.Attributes;
            AddSignatureAttribute(codeProp, field.Signature);
            return codeProp;
        }).ToArray();
    }

    private CodeMemberMethod[]  LoadConstructors(TypeInfo cls)
    {
        // return Array.Empty<CodeConstructor>();
        
        return cls.Constructors.Select(method =>
        {
            var codeMethod = new CodeMemberMethod()
            {
                Name = $"@New",
                Attributes = method.Attributes,
                ReturnType = JavaTypeRef(new TypeInfoInstance(cls.Cls))
            };
            codeMethod.Attributes |= MemberAttributes.Static|MemberAttributes.New; 
            // codeMethod.Attributes |= ; 

            if (method.Throws.Any())
            {
                var fmtThrow = (TypeInfoInstance type) =>
                    $"<exception cref=\"{type.Info!.ClassPath.Replace('/', '.')}\"></exception>";
                method.Throws.ForEach(t => codeMethod.Comments.Add(new(fmtThrow(t), true)));
            }

            var paramIndex = 0;
            var paramNames = new List<string>();
            foreach (var param in method.Params)
            {
                var name = EscapedIdent($"param_{paramIndex++}");
                codeMethod.Parameters.Add(new(JavaTypeRef(param), name));
                // codeMethod.BaseConstructorArgs.Add(new CodeVariableReferenceExpression(name));
                paramNames.Add(name);
            }

            var newObjCallStr = $"Jvm.NewObj<{cls.Name}>({string.Join(", ", paramNames)})";
            if (cls.ClassPath == "java.lang.String")
            {
                newObjCallStr = $"Jvm.ExtractStr({newObjCallStr})";
            }

            codeMethod.Statements.Add(new CodeMethodReturnStatement(
                new CodeSnippetExpression(newObjCallStr)));
            
            
            
            // var bindMethod = new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), "BindBySig");
            // var bindInvoke = new CodeMethodInvokeExpression(bindMethod,
            //     new CodeExpression[]
            //             { new CodePrimitiveExpression(method.Name), new CodePrimitiveExpression(method.Signature) }
            //         .Concat(paramNames.Select(p => new CodeVariableReferenceExpression(p))).ToArray<CodeExpression>());
            // codeMethod.Statements.Add(bindInvoke);
            AddSignatureAttribute(codeMethod, method.Signature);
            return codeMethod;
        }).ToArray();
    }

    private CodeMemberMethod[] LoadMethods(TypeInfo cls)
    {
        
        // abstract classes don't have to fully implement interface but they do in c# so we need retrieve the missing ones and apply them here
        var methods = cls.Methods.ToList();
        if (cls.Attributes.HasFlag(TypeAttributes.Abstract))
        {
            var implmentsMethods = cls.Implements.SelectMany(iface =>
            {
                iface.Info.Load();
                return iface.Info.Methods;
            }).Distinct(new MethodEqualityComparer()).Where(m => !cls.Methods.Any(m2 => m.Name == m2.Name && m.Signature == m2.Signature));
            
            methods.AddRange(implmentsMethods);
            
            var i = 0;

        }
        
        
        
        return methods.Select(method =>
        {
            if (method.Name == "remove" && cls.Name == "Hashtable")
            {
                var i = 0;
            }
            var codeMethod = new CodeMemberMethod()
            {
                Name = EscapedIdent(method.Name),
                Attributes = method.Attributes
            };
            
            // this is annoying and not what I want to do but we need to check parent classes to see if we need to override
            var extends = cls.Extends;
            while (extends?.Info != null)
            {
                extends.Info!.Load();
                TypeInfo.Method parentMethod;
                // Console.WriteLine($"[{method.Name}] -> {method.Signature}");
                try
                {
                    parentMethod = extends.Info!.Methods.SingleOrDefault(m => m.Name == method.Name && m.Signature == method.Signature)!;
                }
                catch (System.InvalidOperationException ex)
                {
                    var parentMethods = extends.Info!.Methods.Where(m => m.Name == method.Name && m.Signature == method.Signature).ToArray();
                    throw;
                }

                if (parentMethod != null)
                {
                    codeMethod.Attributes |= MemberAttributes.Override;
                    break;
                }
                
                extends = extends.Info.Extends;
            }

            if (!codeMethod.Attributes.HasFlag(MemberAttributes.Override))
            {
                method.TypeParams.ForEach(p =>
                {
                    var param = new CodeTypeParameter(p.Name);
                    p.Constraints.ForEach(c =>
                    {
                        var typeStr = JavaTypeRefStr(c);
                        if (typeStr == "java.lang.Object")
                        {
                            return;
                            typeStr = "Mud.Types.IJavaObject";
                        }
                        param.Constraints.Add(new CodeTypeReference(typeStr));
                    });
                    codeMethod.TypeParameters.Add(param);
                });
            }
            
            

            if (method.Throws.Any())
            {
                var fmtThrow = (TypeInfoInstance type) =>
                    $"<exception cref=\"{type.Info!.ClassPath.Replace('/', '.')}\"></exception>";
                method.Throws.ForEach(t => codeMethod.Comments.Add(new(fmtThrow(t), true)));
            }

            codeMethod.ReturnType = JavaTypeRef(method.ReturnType);

            var paramIndex = 0;
            var paramNames = new List<string>();
            foreach (var param in method.Params)
            {
                var name = $"param_{paramIndex++}";
                codeMethod.Parameters.Add(new(JavaTypeRef(param), name));
                paramNames.Add(name);
            }
            // return CallBySig<int>(method.Name, method.GetCustomAttribute<TypeSignatureAttribute>().Signature);

            CodeStatement forwarder;
            var returnType = method.ReturnType?.Info == null
                ? new JavaFullType(JavaType.Object)
                : ReflectParser.TypeFromClass(method.ReturnType!.Info!.Cls);
            if (method.Attributes.HasFlag(MemberAttributes.Static))
            {
                var args = string.Join(", ", new[] { $"\"{method.Name}\"" }.Concat(paramNames));
                
                
                if (returnType.Type == JavaType.Void)
                {
                    forwarder = new CodeExpressionStatement(
                        new CodeSnippetExpression($"Jvm.GetClassInfo(\"{cls.ClassPath}\").CallStaticMethod({args})"));
                }
                else
                {
                    forwarder = new CodeMethodReturnStatement
                    {
                        Expression = new CodeSnippetExpression(
                            $"Jvm.GetClassInfo(\"{cls.ClassPath}\").CallStaticMethod<{JavaTypeRefStr(method.ReturnType)}>({args})")
                    };
                }
            }
            else
            {
                CodeMethodReferenceExpression callBySig;
                if (returnType.Type == JavaType.Void)
                {
                    callBySig = new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), "CallBySig");
                }
                else
                {
                    callBySig = new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), "CallBySig",
                        codeMethod.ReturnType);
                }

                var forwarderInvoke = new CodeMethodInvokeExpression(callBySig,
                    new CodeExpression[]
                            { new CodePrimitiveExpression(method.Name), new CodePrimitiveExpression(method.Signature) }
                        .Concat(paramNames.Select(p => new CodeVariableReferenceExpression(p)))
                        .ToArray<CodeExpression>());
                if (returnType.Type == JavaType.Void)
                {
                    forwarder = new CodeExpressionStatement(forwarderInvoke);
                }
                else
                {
                    forwarder = new CodeMethodReturnStatement
                    {
                        Expression = forwarderInvoke
                    };
                }
            }


            codeMethod.Statements.Add(forwarder);
            AddSignatureAttribute(codeMethod, method.Signature);
            return codeMethod;
        }).ToArray();
    }

    private void AddSignatureAttribute(CodeTypeMember member, string signature)
    {
        // member.CustomAttributes.Add(new("TypeSignature",
        // new CodeAttributeArgument(new CodePrimitiveExpression($"{signature}"))));
    }
}

internal static class StringExtension
{
    public static (string Pacakge, string Name) SplitClassPath(this string classPath)
    {
        var parts = classPath.Split('.').ToList();
        var name = parts.Last();
        parts.RemoveAt(parts.Count - 1);
        var package = string.Join('.', parts);
        return (package, name);
    }
}


// var method = MethodBase.GetCurrentMethod();
// return CallBySig<int>(method.Name, method.GetCustomAttribute<TypeSignatureAttribute>().Signature);

class MethodEqualityComparer : IEqualityComparer<TypeInfo.Method>
{

    public bool Equals(TypeInfo.Method x, TypeInfo.Method y)
    {
        return x.Name == y.Name && x.Signature == y.Signature;
    }

    public int GetHashCode(TypeInfo.Method obj)
    {
        return obj.GetHashCode();
    }
}

class FieldEqualityComparer : IEqualityComparer<TypeInfo.Field>
{

    public bool Equals(TypeInfo.Field x, TypeInfo.Field y)
    {
        return x.Name == y.Name && x.Signature == y.Signature;
    }

    public int GetHashCode(TypeInfo.Field obj)
    {
        return obj.GetHashCode();
    }
}