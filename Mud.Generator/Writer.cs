using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Text.RegularExpressions;
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

    public Writer(ReconstructedClass cls)
    {
        _codeNamespace = new(EscapedIdent(cls.Package));
        Load(null, cls);
    }

    private string EscapedPath(string path) => string.Join('.', path.Split('.').Select(EscapedIdent));
    private string EscapedIdent(string ident) => _codeGen.CreateEscapedIdentifier(ident);

    public string[] UsedClassPaths => ClassesUsed.ToArray();
    
    private void Load(CodeTypeDeclaration? parent, ReconstructedClass target)
    {
        var codeType = new CodeTypeDeclaration(EscapedIdent(target.Name))
        {
            IsClass = target.IsClass,
            IsInterface = target.IsInterface,
        };
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        parent?.Members.Add(codeType);
        _codeNamespace.Types.Add(codeType);
        
        codeType.TypeAttributes |= TypeAttributes.Abstract;

        codeType.CustomAttributes.Add(new("ClassPath",
            new CodeAttributeArgument(new CodePrimitiveExpression(EscapedPath(target.ClassPath)))));
        // _codeType.CustomAttributes.Add(new("TypeSignature",
        // new CodeAttributeArgument(new CodePrimitiveExpression($"{_cls.ClassPath}"))));


        if (target.SuperClass != null)
        {
            codeType.BaseTypes.Add(JavaTypeRefStr(target.SuperClass));
        } 
        else if (target.Interfaces.Count == 0)
        {
            codeType.BaseTypes.Add(target.IsInterface ? typeof(IJavaObject) : typeof(JavaObject));
        } 
        target.Interfaces.ForEach(iface =>
        {
            codeType.BaseTypes.Add(JavaTypeRefStr(iface));
        });

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
            p.Bounds.ForEach(b =>
            {
                param.Constraints.Add(new CodeTypeReference(JavaTypeRefStr(b)));
            });
            
            codeType.TypeParameters.Add(param);
        });
        
        
        
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


    private CodeTypeReference JavaTypeRef(JavaFullType type)
    {
        if (type.Type is JavaType.Object)
        {
            if (type.ArrayType != null)
            {
                return new CodeTypeReference(JavaTypeRef(type.ArrayType), 1);
            }

            var classPath = type.CustomType!.Replace('/', '.');
            var (package, name) = classPath.SplitClassPath();
            PackageUsages.Add(package);
            
            ClassesUsed.Add(classPath.Split('$')[0]);
            static bool IsDotNetType(string name)
            {
                return AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()
                    .Where(t => t.Namespace?.Split('.')[0] == "System")).Any(t => t.Name == name);
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

    private string JavaTypeRefStr(string classPath)
    {
        classPath = classPath.Replace('/', '.');
        ClassesUsed.Add(classPath.Split('$')[0]);
        var (package, name) = classPath.SplitClassPath();
        PackageUsages.Add(package);

        static bool IsDotNetType(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()
                .Where(t => t.Namespace?.Split('.')[0] == "System")).Any(t => t.Name == name);
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


    private string JavaTypeRefStr(ReconstructedTypeInfo type)
    {

        if (type.Name != null) return type.Name;
        if (type.Class == null)
        {
            return "java.lang.Object";
        }

        // this occurs from wildcard generics
        if (type.Class.ClassPath == "void")
        {
            return "java.lang.Object";
        }
        
        var typeStr = $"{JavaTypeRefStr(type.Class.ClassPath)}";
        if (!type.Args.Any())
        {
            return typeStr;
        }
        return $"{typeStr}<{string.Join(", ", type.Args.Select(JavaTypeRefStr))}>";
    }

    private CodeMemberProperty[] LoadFields(ReconstructedClass cls)
    {

        return cls.Fields.Select(field =>
        {
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

    private CodeConstructor[]  LoadConstructors(ReconstructedClass cls)
    {
        return cls.Constructors.Select(method =>
        {
            var codeMethod = new CodeConstructor()
            {
                Attributes = method.Attributes
            };

            if (method.Throws.Any())
            {
                var fmtThrow = (JavaFullType type) =>
                    $"<exception cref=\"{type.CustomType!.Replace('/', '.')}\"></exception>";
                method.Throws.ForEach(t => codeMethod.Comments.Add(new(fmtThrow(t), true)));
            }

            var paramIndex = 0;
            var paramNames = new List<string>();
            foreach (var param in method.ParamTypes)
            {
                var name = EscapedIdent($"param_{paramIndex++}");
                codeMethod.Parameters.Add(new(JavaTypeRef(param), name));
                paramNames.Add(name);
            }

            var bindMethod = new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), "BindBySig");
            var bindInvoke = new CodeMethodInvokeExpression(bindMethod,
                new CodeExpression[]
                        { new CodePrimitiveExpression(method.Name), new CodePrimitiveExpression(method.Signature) }
                    .Concat(paramNames.Select(p => new CodeVariableReferenceExpression(p))).ToArray<CodeExpression>());
            codeMethod.Statements.Add(bindInvoke);
            AddSignatureAttribute(codeMethod, method.Signature);
            return codeMethod;
        }).ToArray();
    }

    private CodeMemberMethod[] LoadMethods(ReconstructedClass cls)
    {
        return cls.Methods.Select(method =>
        {
            // Console.WriteLine($"[{method.Name}] -> {method.Signature}");
            var codeMethod = new CodeMemberMethod()
            {
                Name = EscapedIdent(method.Name),
                Attributes = method.Attributes
            };

            if (method.Throws.Any())
            {
                var fmtThrow = (JavaFullType type) =>
                    $"<exception cref=\"{type.CustomType!.Replace('/', '.')}\"></exception>";
                method.Throws.ForEach(t => codeMethod.Comments.Add(new(fmtThrow(t), true)));
            }

            codeMethod.ReturnType = JavaTypeRef(method.ReturnType);

            var paramIndex = 0;
            var paramNames = new List<string>();
            foreach (var param in method.ParamTypes)
            {
                var name = $"param_{paramIndex++}";
                codeMethod.Parameters.Add(new(JavaTypeRef(param), name));
                paramNames.Add(name);
            }
            // return CallBySig<int>(method.Name, method.GetCustomAttribute<TypeSignatureAttribute>().Signature);

            CodeStatement forwarder;
            if (method.Attributes.HasFlag(MemberAttributes.Static))
            {
                var args = string.Join(", ", new[] { $"\"{method.Name}\"" }.Concat(paramNames));

                if (method.ReturnType.Type == JavaType.Void)
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
                if (method.ReturnType.Type == JavaType.Void)
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
                if (method.ReturnType.Type == JavaType.Void)
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