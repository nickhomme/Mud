namespace Mud;

public class JavaException : Exception
{
    public string JavaStackTrace { get; }
    public JavaException(string message, string stackTrace) : base(message)
    {
        JavaStackTrace = stackTrace;
    }
    // public override string? StackTrace { get; }
}