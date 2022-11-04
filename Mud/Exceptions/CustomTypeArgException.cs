namespace Mud.Exceptions;

public class CustomTypeArgException : Exception
{
    public CustomTypeArgException() : base("Attempt to create a CustomType record using just JavaType.Object. When creating a custom type that targets an object the class path must be proivded")
    {
    }
}