// See https://aka.ms/new-console-template for more information


using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CSharp;
using Mud;
using Mud.Generator;
using Mud.Types;

Jvm.Initialize();
// var jclass = new FileInfo(args[0]);
// Console.WriteLine("Hello, World!");



var signatureDir = new DirectoryInfo("/Users/nicholas/tmp/java/sigs/jdk8/sigs/jdk8-exploded/java/lang");

void AddFiles(DirectoryInfo dir)
{
    Console.WriteLine($"Checking dir {dir.FullName}");
    foreach (var fileInfo in dir.GetFiles("*.txt"))
    {
        var fileSigs = new ClassTypeSigLines(fileInfo);
        var cls = new ReconstructedClass(fileSigs);
        if (char.IsDigit(cls.Name[0])) continue;
        ;
        var baseDir = Directory.GetCurrentDirectory();
        var clsDir = new DirectoryInfo(Path.Join(baseDir, "Mud.J8", cls.Package.Replace('.', '/')));
        
        if (!clsDir.Exists)
        {
            clsDir.Create();
        }
        var filePath = Path.Join(clsDir.FullName, $"{cls.Name}.cs");
        // if (File.Exists(filePath))
        // {
        //     continue;
        // }
        cls.Parse(fileSigs);

        new Writer(cls).WriteTo(filePath);
        // Console.WriteLine($"Saved to: {filePath}\n\t{fileInfo.FullName}");
    }

    foreach (var subDir in dir.GetDirectories())
    {
        AddFiles(subDir);
    }
}

AddFiles(signatureDir);





