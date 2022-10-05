using Mud.Types;

namespace Mud.Generator;

[ClassPath("sun.reflect.generics.tree.MethodTypeSignature")]
public class MethodTypeSignature : JavaObject
{
    public JavaObject getReturnType() => CallNamedReturn("getReturnType", new("sun.reflect.generics.tree.ReturnType"));
    public JavaObject[] getParameterTypes() => CallNamedReturn<JavaObject[]>("getParameterTypes", new(JavaType.Object)
    {
        ArrayType = new(JavaType.Object)
        {
            CustomType = "sun.reflect.generics.tree.TypeSignature"
        }
    });
}