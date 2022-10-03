using NUnit.Framework;

namespace Mud.Test;

public class JarBaseTest : BaseTest
{
    protected HttpClient Client { get; }
    private string JarUrl { get; }
    public FileInfo Jar { get; }
    
    protected JarBaseTest(string jarPath, string jarUrl)
    {
        Client = new();
        JarUrl = jarUrl;
        Jar = new FileInfo(jarPath);
    }

    [OneTimeSetUp]
    public override async Task Setup()
    {
        if (!Jar.Exists)
        {
            var fileBytes = await Client.GetByteArrayAsync(JarUrl);
            await File.WriteAllBytesAsync(Jar.FullName, fileBytes);
        }
        Args.Add($"-Djava.class.path={Jar.FullName}");
        await base.Setup();
    }


    public override void Dispose()
    {
        Client?.Dispose();
        base.Dispose();
    }
}