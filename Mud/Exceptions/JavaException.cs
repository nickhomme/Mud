namespace Mud.Exceptions;

/// <summary>
/// Exception in the JVM
/// </summary>
public class JavaException : Exception
{
    /// <summary>
    /// Stack trace from the JVM
    /// </summary>
    public string JavaStackTrace { get; }
    public JavaException(string message, string stackTrace) : base(message)
    {
        JavaStackTrace = stackTrace;
    }
    // public override string? StackTrace { get; }
}