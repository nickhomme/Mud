namespace Mud.Exceptions;

/// <summary>
/// There was not a backing class found that implements provided interface
/// </summary>
public class NoClassMappingException : Exception
{
    public Type RequiredType { get; }
    public ClassInfo JavaType { get; }

    public NoClassMappingException(Type requiredType, ClassInfo javaType) : base($"A required mapping for Java class {javaType.ClassPath} was not found that implements {requiredType.FullName}")
    {
        RequiredType = requiredType;
        JavaType = javaType;
    }
}