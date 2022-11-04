namespace Mud.Exceptions;

/// <summary>
/// Objects interfacing with the JVM must inherit Mud.Types.BoundObject
/// </summary>
public class InvalidBoundObjectException : Exception
{
    public Type Type { get; }
    public object? Value { get; }

    public InvalidBoundObjectException(Type type, object? value) : base($"Attempt to bind an object that does not inherit BoundObject and is not an interface")
    {
        Type = type;
        Value = value;
    }
}