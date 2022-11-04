namespace Mud.Exceptions;

/// <summary>
/// JVM was not initilized
/// </summary>
public class JvmNotInitializedException : Exception
{
    public JvmNotInitializedException() : base("Attempt to work with the JVM before it has been initialized")
    {
        
    }
}