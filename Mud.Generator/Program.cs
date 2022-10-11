// See https://aka.ms/new-console-template for more information

using System.CodeDom;
using System.CodeDom.Compiler;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CSharp;
using Mud;
using Mud.Generator;
using Mud.Types;
using TypeInfo = Mud.Generator.TypeInfo;

var baseDir = Path.Join(Directory.GetCurrentDirectory(), "Mud.J8");

// Jvm.Initialize("-Djava.class.path=/Users/nicholas/Documents/Dev/Playground/Jdk/Signatures/lib/guava-31.1-jre.jar");
Jvm.Initialize("-Djava.class.path=/Users/nicholas/Documents/Dev/Playground/Jdk/Mud/out/artifacts/Mud_jar/Mud.jar:/Library/Java/JavaVirtualMachines/adoptopenjdk-8.jdk/Contents/Home/jre/lib/rt.jar:/Users/nicholas/Downloads/jt400-9.7.jar");

var mudClsInfo = Jvm.GetClassInfo("com.nickhomme.mud.Mud");

var packages = new List<string>()
{
    // @"^java\.util\.Collection*"
    // @"^java\.util\.Collection*"
    // @"^java\.util\.*"
    @"^com\.ibm\.*"
};
var testingClasses = new List<string>()
{
    // "java.awt.dnd.DropTargetAdapter",
    // "java.lang.Comparable",
    // "java.lang.Object",
    // "java.lang.String",
    // "java.lang.Class",
    // "java.lang.Iterable",
    // "java.lang.reflect.Modifier",
    // "java.util.Dictionary",
    // "java.util.Map",
    // "java.util.Set",
    // "java.util.Hashtable",
    // "java.util.Collection",
    // "java.awt.color.ICC_ProfileGray",
    // "java.awt.color.ICC_Profile"
};


var classes = mudClsInfo.CallStaticMethod<string[]>("GetAllLoadedClassPaths").Where(c =>
{
    
    if (packages.Count > 0 && !packages.Any(p => Regex.Match(c, p).Success))
    {
        return false;
    }
    
    if (testingClasses.Count != 0) return testingClasses.Contains(c);
    // if (!c.StartsWith("java")) return false;
    var subClass = c.Split('$').ElementAtOrDefault(1);
    if (subClass == null) return true;
    return !char.IsDigit(subClass[0]);
} );
// Console.WriteLine(string.Join("\n", classes));

var completedClassPaths = new HashSet<string>();

void TryLoadClass(string classPath)
{
    if (Writer.Written.Contains(classPath)) return;
    
    if (completedClassPaths.Contains(classPath)) return;
    var cls = TypeInfo.Get(classPath);
    if (cls.IsPrivate)
    {
        return;
    }

    var clsDir = new DirectoryInfo(Path.Join(baseDir, cls.Package.Replace('.', '/')));
        
    if (!clsDir.Exists)
    {
        clsDir.Create();
    }

    completedClassPaths.Add(cls.ClassPath);
    var filePath = Path.Join(clsDir.FullName, $"{cls.Name}.cs");
    
    if (File.Exists(filePath))
    {
        // return;
    }
    
    cls.Load();
    var writer = new Writer(cls);
    writer.WriteTo(filePath);
    foreach (var usedClass in writer.UsedClassPaths)
    {
        // Console.WriteLine("Pasing used: " + usedClass);
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



