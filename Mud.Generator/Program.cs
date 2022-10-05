// See https://aka.ms/new-console-template for more information


using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using Mud;
using Mud.Generator;
using Mud.Types;

// var jclass = new FileInfo(args[0]);
// Console.WriteLine("Hello, World!");
int jValSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(JavaVal));
int callRespsize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(JavaCallResp));

Console.WriteLine(callRespsize);
Console.WriteLine(jValSize);
Console.WriteLine(System.Runtime.InteropServices.Marshal.SizeOf(typeof(bool)));
Console.WriteLine(System.Runtime.InteropServices.Marshal.SizeOf(typeof(char)));

var jvm = new Jvm();

var boolType = jvm.GetClass("sun.reflect.generics.tree.BooleanSignature");
var arrType = jvm.GetClass("sun.reflect.generics.tree.ArrayTypeSignature");
var byteType = jvm.GetClass("sun.reflect.generics.tree.ByteSignature");
var charType = jvm.GetClass("sun.reflect.generics.tree.CharSignature");
var classType = jvm.GetClass("sun.reflect.generics.tree.ClassSignature");
var classTypeType = jvm.GetClass("sun.reflect.generics.tree.ClassTypeSignature");
var doubleType = jvm.GetClass("sun.reflect.generics.tree.DoubleSignature");
var floatType = jvm.GetClass("sun.reflect.generics.tree.FloatSignature");
var formalTypeParam = jvm.GetClass("sun.reflect.generics.tree.FormalTypeParameter");
var intType = jvm.GetClass("sun.reflect.generics.tree.IntSignature");
var longType = jvm.GetClass("sun.reflect.generics.tree.LongSignature");
var shortType = jvm.GetClass("sun.reflect.generics.tree.ShortSignature");
var typeVarType = jvm.GetClass("sun.reflect.generics.tree.TypeVariableSignature");
var voidType = jvm.GetClass("sun.reflect.generics.tree.VoidDescriptor");
var classTypeSigType = jvm.GetClass("sun.reflect.generics.tree.SimpleClassTypeSignature");


Dictionary<ClassInfo, JavaType> primitives = new()
{
    { boolType, JavaType.Bool },
    { byteType, JavaType.Byte },
    { charType, JavaType.Char },
    { doubleType, JavaType.Double },
    { floatType, JavaType.Float },
    { intType, JavaType.Int },
    { longType, JavaType.Long },
    { shortType, JavaType.Short },
    { voidType, JavaType.Void },
};
JavaFullType ExtractType(JavaObject type)
{
    foreach (var primitive in primitives)
    {
        if (type.InstanceOf(primitive.Key))
        {
            return new(primitive.Value);
        }
    }
    if (type.InstanceOf(arrType))
    {
        using var componentTypeObj = type.CallNamedReturn("getComponentType", new("sun.reflect.generics.tree.TypeSignature"));
        return new(JavaType.Object)
        {
            ArrayType = ExtractType(componentTypeObj)
        };
    }
    if (type.InstanceOf(classTypeType))
    {
        using var pathList = type.CallNamedReturn("getPath", new("java/util/List"));
        using var paramSigObj = pathList.Call<JavaObject>("get", 0);
        var paramSig = paramSigObj.Call<string>("getName");
        return new(paramSig);
    }

    throw new NotImplementedException($"Parsing of type signature class `{type.ClassPath}`");
}


var fileSigs = new ClassTypeSigLines(new FileInfo("/Volumes/NH/Mud/signatures/java/lang/Float.class.txt"));

var classPath = fileSigs.Header[(fileSigs.Header.IndexOf(" class ", StringComparison.Ordinal) + 7)..];
classPath = classPath.Substring(0, classPath.IndexOf(' '));

var cls = new ReconstructedClass(classPath);


fileSigs.Methods.ForEach(m =>
{
    using var sigParser = jvm.GetClass("sun.reflect.generics.parser.SignatureParser").CallStaticMethod<SignatureParser>("make");
    Console.WriteLine(m.Full);
    Console.WriteLine(m.Descriptor);
    var paramStart = m.Full.IndexOf('(');
    var methodName = "";
    for (var i = paramStart - 1; i >= 0; i--)
    {
        if (m.Full[i] == ' ')
        {
            methodName = m.Full.Substring(i + 1, paramStart - i - 1);
            break;
        } 
    }
    Console.WriteLine($"MET?H: _{methodName}_");
    
    
    using var parsedMethod = sigParser.Call<MethodTypeSignature>("parseMethodSig", m.Descriptor);
    using var returnTypeObj = parsedMethod.getReturnType();

    var paramTypeObjs = parsedMethod.getParameterTypes();
    var exceptionTypeObjs = parsedMethod.getExceptionTypes();
    var returnType = ExtractType(returnTypeObj);
    var paramTypes = paramTypeObjs.Select(ExtractType).ToArray();
    var exceptionTypes = exceptionTypeObjs.Select(ExtractType).ToArray();
    
    cls.Methods.Add(new()
    {
        Name = methodName,
        Throws = exceptionTypes.ToList(),
        ParamTypes = paramTypes.ToList()
    });

    Console.WriteLine($"Attempted Reconstruction: {JavaObject.GenMethodSignature(returnType, paramTypes)}\n___\n");
});

var gen = new CSharpCodeProvider();
var code = new CodeCompileUnit();
var comp = new CompilerParameters();


var codeDomNamespace = new CodeNamespace(cls.Package);
var codeDomType = new CodeTypeDeclaration();
codeDomType.Name = cls.Name;
codeDomType.IsClass = true;
codeDomNamespace.Types.Add(codeDomType);
cls.Methods.ForEach(m =>
{
    var method = new CodeMemberMethod();
    method.Name = m.Name;
    var index = 0;
    m.ParamTypes.ForEach(p =>
    {
        var name = $"param_{index++}";
        switch (p.Type)
        {
            case JavaType.Int:
                method.Parameters.Add(new(typeof(int), name));
                break;
            case JavaType.Bool:
                method.Parameters.Add(new(typeof(bool), name));
                break;
            case JavaType.Byte:
                method.Parameters.Add(new(typeof(byte), name));
                break;
            case JavaType.Char:
                method.Parameters.Add(new(typeof(char), name));
                break;
            case JavaType.Short:
                method.Parameters.Add(new(typeof(short), name));
                break;
            case JavaType.Long:
                method.Parameters.Add(new(typeof(long), name));
                break;
            case JavaType.Float:
                method.Parameters.Add(new(typeof(float), name));
                break;
            case JavaType.Double:
                method.Parameters.Add(new(typeof(double), name));
                break;
            case JavaType.Object:
                if (p.ArrayType != null)
                {
                    method.Parameters.Add(new($"Array", name));
                    return;
                }
                method.Parameters.Add(new(p.CustomType!, name));
                break;
            case JavaType.Void:
                method.Parameters.Add(new(typeof(void), name));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    });
    codeDomType.Members.Add(method);
});


code.Namespaces.Add(codeDomNamespace);

var tw1 = new IndentedTextWriter(new StreamWriter("test.cs", false), "    ");
gen.GenerateCodeFromCompileUnit(code, tw1, new CodeGeneratorOptions());
tw1.Close();