namespace Mud.Exceptions;

public class InstanceMemberOnStaticException : Exception
{
    public string Member;
    public ClassInfo ClassInfo;

    public InstanceMemberOnStaticException(string member, ClassInfo classInfo) : base($"Attempt to use an instance method on a static interface")
    {
        Member = member;
        ClassInfo = classInfo;
    }
}