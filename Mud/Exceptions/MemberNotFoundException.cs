namespace Mud.Exceptions;

/// <summary>
/// The request method was not found
/// </summary>
public class MemberNotFoundException : Exception
{
    public string Member { get; }
    public string Signature { get; }
    public string ClassPath { get; }

    public MemberNotFoundException(string classPath, string member, string signature, string message) : base(message)
    {
        ClassPath = classPath;
        Member = member;
        Signature = signature;
    }
}