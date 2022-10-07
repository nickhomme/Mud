using System.CodeDom;
using System.Reflection;
using System.Text.RegularExpressions;
using Mud.Types;

// ReSharper disable BitwiseOperatorOnEnumWithoutFlags

namespace Mud.Generator;

public class ReconstructedClass : ReconstructedInfo
{
    public string ClassPath { get; }
    public string Package { get; }
    public bool IsClass { get; init; }
    public bool IsInterface { get; init; }
    public bool IsEnum { get; init; }
    public bool IsFinal { get; init; }
    public bool IsAbstract { get; init; }
    public string? SubClassOf { get; init; }
    public List<ReconstructedMethod> Constructors { get; } = new();
    public List<ReconstructedMethod> Methods { get; } = new();
    public List<ReconstructedField> Fields { get; } = new();
    public TypeAttributes Attributes { get; init; }

#pragma warning disable CS8618
    private static ClassInfo BoolType { get; set; }
    private static ClassInfo ArrType { get; set; }
    private static ClassInfo ByteType { get; set; }
    private static ClassInfo CharType { get; set; }
    private static ClassInfo ClassType { get; set; }
    private static ClassInfo ClassTypeType { get; set; }
    private static ClassInfo DoubleType { get; set; }
    private static ClassInfo FloatType { get; set; }
    private static ClassInfo FormalTypeParam { get; set; }
    private static ClassInfo IntType { get; set; }
    private static ClassInfo LongType { get; set; }
    private static ClassInfo ShortType { get; set; }
    private static ClassInfo TypeVarType { get; set; }
    private static ClassInfo VoidType { get; set; }
    private static ClassInfo ClassTypeSigType { get; set; }
#pragma warning restore CS8618
    
    public ReconstructedClass(ClassTypeSigLines lines)
    {

        var classIndex = lines.Header.StartsWith("class ") ? 0 : lines.Header.IndexOf(" class ", StringComparison.Ordinal);
        var interfaceIndex = lines.Header.StartsWith("interface ") ? 0 : lines.Header.IndexOf(" interface ", StringComparison.Ordinal);
        var enumIndex = lines.Header.StartsWith("enum ") ? 0 : lines.Header.IndexOf(" enum ", StringComparison.Ordinal);
        string classPath;
        if (classIndex != -1)
        {
            classIndex = classIndex == 0 ? -1 : classIndex;
            classPath = lines.Header[(classIndex + 7)..];
            IsClass = true;
        }
        else if (interfaceIndex != -1)
        {
            interfaceIndex = interfaceIndex == 0 ? -1 : interfaceIndex;
            classPath = lines.Header[(interfaceIndex + 11)..];
            IsInterface = true;
        }
        else if (enumIndex != -1)
        {
            enumIndex = enumIndex == 0 ? -1 : enumIndex;
            classPath = lines.Header[(enumIndex + 6)..];
            Console.WriteLine($"Enum: `{lines.FilePath}`");
            IsEnum = true;
        }
        else
        {
            Console.WriteLine($"Unsupported header line: `{lines.Header}`\n\t{lines.FilePath}");
            Environment.Exit(1);
            classPath = "";
        }
    
        classPath = classPath.Substring(0, classPath.IndexOf(' '));
        ClassPath = classPath;
        (Package, Name) = classPath.SplitClassPath();
        if (Name == "")
        {
            Environment.Exit(1);
        }

        Name = Name.Split('<')[0];
        var subClassParts = Name.Split('$');
        SubClassOf = subClassParts.Length == 1 ? null : subClassParts[0];
        Name = subClassParts.ElementAtOrDefault(1) ?? Name;
        if (Name == "CharSequence")
        {
            var i = 0;
        }

        IsAbstract = lines.Header.StartsWith("abstract ") || lines.Header.Contains(" abstract ");
        IsFinal = lines.Header.StartsWith("final ") || lines.Header.Contains(" final ");

    }


    public void Parse(ClassTypeSigLines lines)
    {
        // ReSharper disable NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        BoolType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.BooleanSignature");
        ArrType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.ArrayTypeSignature");
        ByteType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.ByteSignature");
        CharType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.CharSignature");
        ClassType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.ClassSignature");
        ClassTypeType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.ClassTypeSignature");
        DoubleType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.DoubleSignature");
        FloatType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.FloatSignature");
        FormalTypeParam ??= Jvm.GetClassInfo("sun.reflect.generics.tree.FormalTypeParameter");
        IntType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.IntSignature");
        LongType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.LongSignature");
        ShortType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.ShortSignature");
        TypeVarType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.TypeVariableSignature");
        VoidType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.VoidDescriptor");
        ClassTypeSigType ??= Jvm.GetClassInfo("sun.reflect.generics.tree.SimpleClassTypeSignature");
// ReSharper restore NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        lines.Constructors.ForEach(m =>
        {
            using var sigParser = Jvm.GetClassInfo("sun.reflect.generics.parser.SignatureParser")
                .CallStaticMethod<SignatureParser>("make");

            using var parsedMethod = sigParser.Call<MethodTypeSignature>("parseMethodSig", m.Descriptor);

            var paramTypeObjs = parsedMethod.getParameterTypes();
            var exceptionTypeObjs = parsedMethod.getExceptionTypes();
            var paramTypes = paramTypeObjs.Select(ExtractType).ToArray();
            var exceptionTypes = exceptionTypeObjs.Select(ExtractType).ToList();
            
            m.ThrowsLine?.Split(';').ToList().ForEach(t =>
            {
                t = t.Trim();
                if (string.IsNullOrWhiteSpace(t) || exceptionTypes.Any(e => e.CustomType == t)) return;
                exceptionTypes.Add(new(t));
            });

            Constructors.Add(new()
            {
                Throws = exceptionTypes,
                ParamTypes = paramTypes.ToList(),
                Attributes = m.Attributes,
                Signature = m.Descriptor,
            });
        });

        lines.Methods.ForEach(m =>
        {
            using var sigParser = Jvm.GetClassInfo("sun.reflect.generics.parser.SignatureParser")
                .CallStaticMethod<SignatureParser>("make");
            using var parsedMethod = sigParser.Call<MethodTypeSignature>("parseMethodSig", m.Descriptor);
            using var returnTypeObj = parsedMethod.getReturnType();

            var paramTypeObjs = parsedMethod.getParameterTypes();
            var exceptionTypeObjs = parsedMethod.getExceptionTypes();
            var returnType = ExtractType(returnTypeObj);
            var paramTypes = paramTypeObjs.Select(ExtractType).ToArray();
            var exceptionTypes = exceptionTypeObjs.Select(ExtractType).ToList();
            
            m.ThrowsLine?.Split(';').ToList().ForEach(t =>
            {
                t = t.Trim();
                if (string.IsNullOrWhiteSpace(t) || exceptionTypes.Any(e => e.CustomType == t)) return;
                exceptionTypes.Add(new(t));
            });
            Methods.Add(new()
            {
                Name = m.Name,
                Throws = exceptionTypes,
                ParamTypes = paramTypes.ToList(),
                Attributes = m.Attributes,
                Signature = m.Descriptor,
                ReturnType = returnType,
                // IsPublic = m.Attributes.HasFlag(MemberAttributes.Public),
                // IsProtected = m.Attributes.HasFlag(MemberAttributes.Family),
                // IsPr = m.Attributes.HasFlag(MemberAttributes.Private),
            });

            // Console.WriteLine(
                // $"Attempted Reconstruction: {JavaObject.GenMethodSignature(returnType, paramTypes)}\n___\n");
        });


        lines.Fields.ForEach(f =>
        {
            using var sigParser = Jvm.GetClassInfo("sun.reflect.generics.parser.SignatureParser")
                .CallStaticMethod<SignatureParser>("make");

            using var parsedType = sigParser.Call("parseTypeSig",
                new JavaFullType("sun.reflect.generics.tree.TypeSignature"), f.Descriptor);

            Fields.Add(new()
            {
                Name = f.Name,
                Attributes = f.Attributes,
                Type = ExtractType(parsedType),
                Signature = f.Descriptor,
            });
        });
    }
    
    private JavaFullType ExtractType(JavaObject type)
    {
        Dictionary<ClassInfo, JavaType> primitives = new()
        {
            { BoolType, JavaType.Bool },
            { ByteType, JavaType.Byte },
            { CharType, JavaType.Char },
            { DoubleType, JavaType.Double },
            { FloatType, JavaType.Float },
            { IntType, JavaType.Int },
            { LongType, JavaType.Long },
            { ShortType, JavaType.Short },
            { VoidType, JavaType.Void },
        };
        
        foreach (var primitive in primitives)
        {
            if (type.InstanceOf(primitive.Key))
            {
                return new(primitive.Value);
            }
        }
        if (type.InstanceOf(ArrType))
        {
            using var componentTypeObj = type.Call("getComponentType", new JavaFullType("sun.reflect.generics.tree.TypeSignature"));
            return new(JavaType.Object)
            {
                ArrayType = ExtractType(componentTypeObj)
            };
        }
        if (type.InstanceOf(ClassTypeType))
        {
            using var pathList = type.Call("getPath", new JavaFullType("java/util/List"));
            using var paramSigObj = pathList.Call<JavaObject>("get", 0);
            var paramSig = paramSigObj.Call<string>("getName");
            return new(paramSig);
        }

        throw new NotImplementedException($"Parsing of type signature class `{type.ClassPath}`");
    }
}

public class ReconstructedMethod : ReconstructedInfo
{
    public List<JavaFullType> Throws { get; init; } = new();
    public JavaFullType ReturnType { get; init; }
    public List<JavaFullType> ParamTypes { get; init; }
    public MemberAttributes Attributes { get; init; }
    public string Signature { get; init; }
}

public class ReconstructedField : ReconstructedInfo
{
    public JavaFullType Type { get; init; }
    public MemberAttributes Attributes { get; init; }
    public string Signature { get; init; }
}

public class ReconstructedInfo
{
    public string Name { get; set; }
    // public bool IsPublic { get; set; }
    // public bool IsProtected { get; set; }
    // public bool IsPrivate { get; set; }
    // public bool IsInternal { get; set; }
    // public bool IsStatic { get; set; }
    // public bool IsFinal { get; set; }
    // public bool IsAbstract { get; set; }
    // public bool IsOverride { get; set; }
}

public class ClassTypeSigLines
{
    public string? FilePath { get; set; }
    public string Header { get; set; }
    public List<InfoLine> Fields { get; } = new();
    public List<InfoLine> Methods { get; } = new();
    public List<InfoLine> Constructors { get; } = new();
    
    public ClassTypeSigLines(FileInfo file)
    {
        FilePath = file.FullName;
        var lines = File.ReadAllLines(file.FullName).ToList();
        Header = lines[0];
        var hasCompiledHeader = Header.StartsWith("Compiled from ");
        if (hasCompiledHeader)
        {
            Header = lines[1];
        }
        for (var i = hasCompiledHeader ? 2 : 1; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Contains("static {};")) break;
            if (line.StartsWith('}')) break;
            
            var info = new InfoLine(line.Trim(), lines[++i].Trim().Replace("descriptor: ", ""));
            // Console.WriteLine($"{file.FullName}:[{i}]: `{info.Full}`");
            var throwsParts = line.Split(" throws ");
            line = throwsParts[0].Trim();
            info.ThrowsLine = throwsParts.ElementAtOrDefault(1)?.Trim();


            var isPublic = line.StartsWith("public ");
            if (isPublic)
            {
                line = line[7..];
                info.Attributes = MemberAttributes.Public;
            }

            var isPrivate = line.StartsWith("private ");
            if (isPrivate)
            {
                line = line[8..];
                info.Attributes = MemberAttributes.Private;
            }

            var isProtected = line.StartsWith("protected ");
            if (isProtected)
            {
                line = line[10..];
                info.Attributes = MemberAttributes.Family;
            }

            var isInternal = !isProtected && !isPrivate && !isPublic;
            if (isInternal)
            {
                info.Attributes = MemberAttributes.Assembly;
            }

            var isStatic = line.StartsWith("static ");
            if (isStatic)
            {
                line = line[7..];
                info.Attributes |= MemberAttributes.Static;
            }

            var isFinal = line.StartsWith("final ");
            if (isFinal)
            {
                line = line[6..];
                info.Attributes |= MemberAttributes.Final;
            }
            
            var isAbstract = line.StartsWith("abstract ");
            if (isAbstract)
            {
                line = line[9..];
                info.Attributes |= MemberAttributes.Abstract;
            }
            
            info.HasDefaultKeyword = line.StartsWith("default ");
            if (info.HasDefaultKeyword)
            {
                line = line[8..];
            }
            
            info.HasSyncKeyword = line.StartsWith("synchronized ");
            if (info.HasSyncKeyword)
            {
                line = line[13..];
            }

            if (line.StartsWith("transient "))
            {
                line = line[10..];
                Console.WriteLine("`transient` modifier not supported. Ignoring.");
            }

            if (line.StartsWith("volatile "))
            {
                line = line[9..];
                Console.WriteLine("`volatile` modifier not supported. Ignoring.");
            }

            if (line.StartsWith("native "))
            {
                line = line[7..];
                Console.WriteLine("`native` modifier not supported. Ignoring.");
            }

            if (line.StartsWith("strictfp "))
            {
                line = line[9..];
                Console.WriteLine("`strictfp` modifier not supported. Ignoring.");
            }

            int constraintIndex = 0;
            while ((constraintIndex = line.IndexOf('<')) != -1)
            {
                var constrainAmnt = 0;
                for (var j = constraintIndex; j < line.Length; j++)
                {
                    if (line[j] == '<')
                    {
                        constrainAmnt++;
                        continue;
                    }

                    if (line[j] == '>')
                    {
                        constrainAmnt--;
                        if (constrainAmnt == 0)
                        {
                            // if constraint is at start then it's the type param declaration
                            line = line[..(constraintIndex)] + line[(j + (constraintIndex == 0 ? 2 : 1))..];
                            break;
                        }
                    }
                }

                line.TrimStart();
            }
            
            // is it method?
            if (line.EndsWith(");") || line.EndsWith(")"))
            {
                var openParenIndex = line.IndexOf('(');
                var lineBeforeParen = line[..openParenIndex];

                var spacesCount = lineBeforeParen.Count(c => c == ' ');
                switch (spacesCount)
                {
                    // If more than 1 space before paren then something went wrong in modifiers parsing.
                    case > 1:
                        throw new Exception($"Modifier parsing failed for line: `{line}`\n\t{file.FullName}");
                    case 1:
                        Methods.Add(info);
                        var methodName = "";
                        var paramStart = line.IndexOf('(');
                        for (var j = paramStart - 1; j >= 0; j--)
                        {
                            if (line[j] != ' ') continue;
                            methodName = line.Substring(j + 1, paramStart - j - 1);
                            break;
                        }

                        info.Name = methodName;
                        break;
                    default:
                        Constructors.Add(info);
                        break;
                }
            }
            else
            {
                Fields.Add(info);
                for (var j = line.Length - 2; j >= 0; j--)
                {
                    if (line[j] != ' ') continue;
                    info.Name = line.Substring(j + 1, line.Length - 2 - j);
                    break;
                }

                if (string.IsNullOrWhiteSpace(info.Name))
                {
                    Environment.Exit(1);
                }
            }
        }
    }
}

public class InfoLine
{
    public string Full { get; }
    public string Descriptor { get; }
    public string Name { get; set; }
    public bool HasDefaultKeyword { get; set; }
    public bool HasSyncKeyword { get; set; }
    public string? Type { get; set; }
    public MemberAttributes Attributes { get; set; }
    public string? ThrowsLine { get; set; }
    public string? TypeParams { get; set; }

    public InfoLine(string full, string descriptor)
    {
        Full = full;
        Descriptor = descriptor;
    }
}