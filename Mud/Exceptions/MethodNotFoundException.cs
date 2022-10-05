namespace Mud.Exceptions;

public class MethodNotFoundException : Exception
{
    public string Method { get; }
    public string Signature { get; }
    public string ClassPath { get; }

    public MethodNotFoundException(string classPath, string method, string signature, string message) : base(message)
    {
        ClassPath = classPath;
        Method = method;
        Signature = signature;
    }
}