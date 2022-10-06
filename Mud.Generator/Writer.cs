using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using Microsoft.CSharp;
using Mud.Types;

namespace Mud.Generator;

public class Writer
{
    private readonly ReconstructedClass _cls;
    private readonly CSharpCodeProvider _codeGen = new();
    private readonly CodeNamespace _codeNamespace;
    private readonly CodeCompileUnit _codeUnit = new();
    private readonly CodeTypeDeclaration _codeType;
    public HashSet<string> PackageUsages { get; } = new();
    private Dictionary<string, string> NameMappings { get; } = new();

    public Writer(ReconstructedClass cls)
    {
        if (cls.Name == "CharSequence")
        {
            var i = 0;
        }
        var isNested = cls.SubClassOf != null;
        _cls = cls;
        _codeNamespace = new(cls.Package);
        _codeType = new(isNested ? cls.SubClassOf! : cls.Name)
        {
            IsClass = !isNested && cls.IsClass,
            IsInterface = !isNested && cls.IsInterface,
        };
        _codeNamespace.Types.Add(_codeType);
        _codeType.IsPartial = true;
        if (isNested)
        {
            
            var nestedType = new CodeTypeDeclaration(cls.Name) {
                IsClass = cls.IsClass,
                IsInterface = cls.IsInterface,
            };
            _codeType.Members.Add(nestedType);
            _codeType = nestedType;
        }

        _codeType.TypeAttributes |= TypeAttributes.Abstract;

        
        _codeType.CustomAttributes.Add(new("ClassPath",
            new CodeAttributeArgument(new CodePrimitiveExpression($"{_cls.ClassPath}"))));
        // _codeType.CustomAttributes.Add(new("TypeSignature",
            // new CodeAttributeArgument(new CodePrimitiveExpression($"{_cls.ClassPath}"))));
        
        
        _codeType.BaseTypes.Add(cls.IsInterface ? typeof(IJavaObject) : typeof(JavaObject));
        
        LoadFields();
        LoadConstructors();
        LoadMethods();
        _codeNamespace.Imports.Add(new("Mud"));
        _codeNamespace.Imports.Add(new("Mud.Types"));
        _codeNamespace.Imports.AddRange(PackageUsages.Where(p => p != cls.Package).Select(u => new CodeNamespaceImport(u)).ToArray());
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
            
            static bool IsDotNetType(string name)
            {
                return AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()
                    .Where(t => t.Namespace?.Split('.')[0] == "System")).Any(t => t.Name == name);
            }
            // if this is a dotnet type then we have to use full path
            if (IsDotNetType(name))
            {
                return new(classPath);
            }
            
            // Check to see if this name is already being used. If it is and the classpath differs then we
            //      must use the full classpath for this instance to avoid collision;
            if (NameMappings.TryGetValue(name!, out var existingClassPath))
            {
                if (existingClassPath != classPath)
                {
                    return new(classPath);
                }
            }
            else
            {
                NameMappings[name] = classPath;
            }

            return new(name);
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
    
    private string JavaTypeRefStr(JavaFullType type)
    {
        if (type.Type is JavaType.Object)
        {
            if (type.ArrayType != null)
            {
                return $"{JavaTypeRefStr(type.ArrayType)}[]";
            }

            var classPath = type.CustomType!.Replace('/', '.');
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
                return classPath;
            }
            
            // Check to see if this name is already being used. If it is and the classpath differs then we
            //      must use the full classpath for this instance to avoid collision;
            if (NameMappings.TryGetValue(name!, out var existingClassPath))
            {
                if (existingClassPath != classPath)
                {
                    return classPath;
                }
            }
            else
            {
                NameMappings[name] = classPath;
            }

            return name;
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

    
    void LoadFields()
    {
        foreach (var field in _cls.Fields)
        {
            var type = JavaTypeRef(field.Type);
            var codeProp = new CodeMemberProperty();
            codeProp.Name = field.Name;
            codeProp.Type = type;

            CodeStatement getForward;
            CodeStatement setForward;

            if (field.Attributes.HasFlag(MemberAttributes.Static))
            {
                getForward = new CodeMethodReturnStatement()
                {
                    Expression = new CodeSnippetExpression(
                        $"Jvm.GetClass(\"{_cls.ClassPath}\").GetStaticField<{JavaTypeRefStr(field.Type)}>(\"{field.Name}\")")
                };
                
                setForward = new CodeExpressionStatement(new CodeSnippetExpression(
                        $"Jvm.GetClass(\"{_cls.ClassPath}\").SetStaticField<{JavaTypeRefStr(field.Type)}>(\"{field.Name}\", value)"));
            }
            else
            {
                getForward = new CodeMethodReturnStatement
                {
                    Expression = new CodeMethodInvokeExpression(new(new CodeThisReferenceExpression(), "GetProp",
                        type), new CodePrimitiveExpression(field.Name))
                };
            
                setForward = new CodeExpressionStatement(new CodeMethodInvokeExpression(new(new CodeThisReferenceExpression(), "SetProp",
                    type), new CodePrimitiveExpression(field.Name), new CodeVariableReferenceExpression("value")));
            }
            
            
           
            
            codeProp.GetStatements.Add(getForward);
            codeProp.SetStatements.Add(setForward);
            codeProp.Attributes = field.Attributes;
            _codeType.Members.Add(codeProp);
            AddSignatureAttribute(codeProp, field.Signature);
        }  
    }

    void LoadConstructors()
    {
        foreach (var method in _cls.Constructors)
        {
            
            var codeMethod = new CodeConstructor()
            {
                Attributes = method.Attributes
            };

            if (method.Throws.Any())
            {
                var fmtThrow = (JavaFullType type) => $"<exception cref=\"{type.CustomType!.Replace('/', '.')}\"></exception>";
                method.Throws.ForEach(t => codeMethod.Comments.Add(new(fmtThrow(t), true)));
            }
            
            var paramIndex = 0;
            var paramNames = new List<string>();
            foreach (var param in method.ParamTypes)
            {
                var name = $"param_{paramIndex++}";
                codeMethod.Parameters.Add(new(JavaTypeRef(param), name));
                paramNames.Add(name);
            }
            
            var bindMethod = new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), "BindBySig");
            var bindInvoke = new CodeMethodInvokeExpression(bindMethod, new CodeExpression[]{new CodePrimitiveExpression(method.Name), new CodePrimitiveExpression(method.Signature)}.Concat(paramNames.Select(p => new CodeVariableReferenceExpression(p))).ToArray<CodeExpression>());
            codeMethod.Statements.Add(bindInvoke);
            _codeType.Members.Add(codeMethod);
            AddSignatureAttribute(codeMethod, method.Signature);
        }
    }
    
    void LoadMethods()
    {
        foreach (var method in _cls.Methods)
        {
            
            var codeMethod = new CodeMemberMethod()
            {
                Name = method.Name,
                Attributes = method.Attributes
            };

            if (method.Throws.Any())
            {
                var fmtThrow = (JavaFullType type) => $"<exception cref=\"{type.CustomType!.Replace('/', '.')}\"></exception>";
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
                
                var args = string.Join(", ", new[]{$"\"{method.Name}\""}.Concat(paramNames));
                
                if (method.ReturnType.Type == JavaType.Void)
                {
                    forwarder = new CodeExpressionStatement(new CodeSnippetExpression($"Jvm.GetClass(\"{_cls.ClassPath}\").CallStaticMethod({args})"));
                }
                else
                {
                    forwarder = new CodeMethodReturnStatement
                    {
                        Expression = new CodeSnippetExpression($"Jvm.GetClass(\"{_cls.ClassPath}\").CallStaticMethod<{JavaTypeRefStr(method.ReturnType)}>({args})") 
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
                var forwarderInvoke = new CodeMethodInvokeExpression(callBySig, new CodeExpression[]{new CodePrimitiveExpression(method.Name), new CodePrimitiveExpression(method.Signature)}.Concat(paramNames.Select(p => new CodeVariableReferenceExpression(p))).ToArray<CodeExpression>());
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
            _codeType.Members.Add(codeMethod);
            AddSignatureAttribute(codeMethod, method.Signature);
        }
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