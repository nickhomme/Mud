using Microsoft.VisualBasic.CompilerServices;

namespace Mud.Types;

public static class Java
{

    public static class Lang
    {
        public const string System = "java.lang.Sytem";
        public const string Object = "java.lang.Object";
        public const string ClassLoader = "java.lang.ClassLoader";
    }

    public static class Utils
    {
        public const string Set = "java.util.Set";
        public const string Date = "java.util.Date";
        public const string Vector = "java.util.Vector";
    }
    
    public static class Reflect
    {
        private const string Package = "java.lang.reflect";
        public const string Field = $"{Package}.Field";
    }
    
}