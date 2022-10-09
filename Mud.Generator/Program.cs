// See https://aka.ms/new-console-template for more information

using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CSharp;
using Mud;
using Mud.Generator;
using Mud.Types;
using TypeInfo = Mud.Generator.TypeInfo;

var baseDir = Path.Join(Directory.GetCurrentDirectory(), "Mud.J8");

// Jvm.Initialize("-Djava.class.path=/Users/nicholas/Documents/Dev/Playground/Jdk/Signatures/lib/guava-31.1-jre.jar");
Jvm.Initialize("-Djava.class.path=/Users/nicholas/Documents/Dev/Playground/Jdk/Mud/out/artifacts/Mud_jar/Mud.jar:/Library/Java/JavaVirtualMachines/adoptopenjdk-8.jdk/Contents/Home/jre/lib/rt.jar");

var mudClsInfo = Jvm.GetClassInfo("com.nickhomme.mud.Mud");
var classes = mudClsInfo.CallStaticMethod<string[]>("GetAllLoadedClassPaths").Where(c =>
{
    return c.StartsWith("java") && c == "java.util.Hashtable";
    // if (!c.StartsWith("java")) return false;
    var subClass = c.Split('$').ElementAtOrDefault(1);
    if (subClass == null) return true;
    return !char.IsDigit(subClass[0]);
} );
// Console.WriteLine(string.Join("\n", classes));

var completedClassPaths = new HashSet<string>();

void TryLoadClass(string classPath)
{
    if (completedClassPaths.Contains(classPath)) return;
    var cls = TypeInfo.Get(classPath);

    var clsDir = new DirectoryInfo(Path.Join(baseDir, cls.Package.Replace('.', '/')));
        
    if (!clsDir.Exists)
    {
        clsDir.Create();
    }

    completedClassPaths.Add(cls.ClassPath);
    var filePath = Path.Join(clsDir.FullName, $"{cls.Name}.cs");
    
    // if (File.Exists(filePath))
    // {
    //     return;
    // }
    
    cls.Load();
    var writer = new Writer(cls);
    writer.WriteTo(filePath);
    foreach (var usedClass in writer.UsedClassPaths)
    {
        Console.WriteLine("Pasing used: " + usedClass);
        // TryLoadClass(usedClass);
    }
}



foreach (var cls in classes)
{
    TryLoadClass(cls);
}


// var cls = ReflectParser.Load("java.lang.AbstractStringBuilder");
// var clsDir = new DirectoryInfo(Path.Join(baseDir, cls.Package.Replace('.', '/')));
//         
// if (!clsDir.Exists)
// {
//     clsDir.Create();
// }
// var filePath = Path.Join(clsDir.FullName, $"{cls.Name}.cs");
// new Writer(cls).WriteTo(filePath);
return;



