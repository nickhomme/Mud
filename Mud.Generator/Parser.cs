using Mud.Types;

namespace Mud.Generator;

public class ReconstructedClass
{
    public string Package { get; }
    public string Name { get; }
    public List<ReconstructedMethod> Methods { get; } = new();
    public List<ReconstructedField> Fields { get; } = new();

    public ReconstructedClass(string classPath)
    {
        var parts = classPath.Split('.').ToList();
        Name = parts.Last();
        Package = string.Join('.', parts.Remove(parts.Last()));
    }
}

public class ReconstructedMethod
{
    public string Name { get; set; }
    public List<JavaFullType> Throws { get; init; } = new();
    public JavaFullType ReturnType { get; init;  }
    public List<JavaFullType> ParamTypes;
}

public class ReconstructedField
{
    public string Name { get; set; }
    public JavaFullType Type { get; }
}


public class ClassTypeSigLines
{
    public string Header { get; set; }
    public List<InfoLine> Fields { get; } = new();
    public List<InfoLine> Methods { get; } = new();

    public ClassTypeSigLines(FileInfo file)
    {
        var lines = File.ReadAllLines(file.FullName).Skip(1).ToList();
        Header = lines[0];
        for (var i = 1; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Contains("static {};")) break;

            var info = new InfoLine(line.Trim(), lines[++i].Trim().Replace("descriptor: ", ""));
            
            // is it method?
            if (line.EndsWith(");"))
            {
                Methods.Add(info);
            }
            else
            {
                Fields.Add(info);
            }
            
        }
    }
}

public class InfoLine
{
    public string Full { get; }
    public string Descriptor { get; }

    public InfoLine(string full, string descriptor)
    {
        Full = full;
        Descriptor = descriptor;
    }
}
