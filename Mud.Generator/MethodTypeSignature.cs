using Mud.Types;

namespace Mud.Generator;

[ClassPath("sun.reflect.generics.tree.MethodTypeSignature")]
public class MethodTypeSignature : JavaObject
{
    public JavaObject getReturnType() => Call("getReturnType", new JavaFullType("sun.reflect.generics.tree.ReturnType"));
    public JavaObject[] getParameterTypes() => Call<JavaObject[]>("getParameterTypes", new JavaFullType(JavaType.Object)
    {
        ArrayType = new(JavaType.Object)
        {
            CustomType = "sun.reflect.generics.tree.TypeSignature"
        }
    });
    
    public JavaObject[] getExceptionTypes() => Call<JavaObject[]>("getExceptionTypes", new JavaFullType(JavaType.Object)
    {
        ArrayType = new(JavaType.Object)
        {
            CustomType = "sun.reflect.generics.tree.FieldTypeSignature"
        }
    });
}