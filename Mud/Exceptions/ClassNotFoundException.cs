namespace Mud.Exceptions;

/// <summary>
/// Class was not found within the JVM
/// </summary>
public class ClassNotFoundException : Exception
{
    public string ClassPath { get; }

    public ClassNotFoundException(string classPath) : base($"Class {classPath} not found.")
    {
        ClassPath = classPath;
    }
}