// See https://aka.ms/new-console-template for more information


using Mud;
using Mud.Generator;
using Mud.Types;

// var jclass = new FileInfo(args[0]);
// Console.WriteLine("Hello, World!");

var jvm = new Jvm();
var sigParser = jvm.GetClass("sun.reflect.generics.parser.SignatureParser").CallStaticMethod<SignatureParser>("make");


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

Console.WriteLine(sigParser);
var parsedMethod = sigParser.Call<MethodTypeSignature>("parseMethodSig", "(Ljava/lang/String;)V");
using var returnTypeObj = parsedMethod.getReturnType();


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
        using var paramSigObj = pathList.CallNamedReturn("get", new(classTypeSigType), 0);
        var paramSig = paramSigObj.Call<string>("getName");
        Console.WriteLine($"ParamSig: {{{paramSig}}}");
        return new(paramSig);
    }

    throw new NotImplementedException($"Parsing of type signature class `{type.ClassPath}`");
}

var paramTypeObjs = parsedMethod.getParameterTypes();
var returnType = ExtractType(returnTypeObj);
var paramTypes = paramTypeObjs.Select(ExtractType).ToList();
