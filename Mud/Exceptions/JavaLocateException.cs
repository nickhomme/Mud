namespace Mud.Exceptions;

/// <summary>
/// JAVA_HOME was either not set or the directory does not exist
/// </summary>
public class JavaLocateException : Exception
{
    internal JavaLocateException(string msg) : base(msg) {}
}