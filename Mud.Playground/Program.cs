// See https://aka.ms/new-console-template for more information

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Mud;
using Mud.Types;

[assembly: InternalsVisibleTo("Mud")]
namespace Mud.Playground;

class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        // Jvm.Initialize();
        Jvm.Initialize();
        
        var integerA = ClassInfo<IInteger>.Instance(10);
        var integerB = ClassInfo<IInteger>.Instance(8);
        ClassInfo<IInteger>.Static.ValueOf(10);
    }
}
