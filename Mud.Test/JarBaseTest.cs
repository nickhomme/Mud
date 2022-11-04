
using Xunit;

namespace Mud.Test;

[Collection("Serial")]
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

    public override async Task InitializeAsync()
    {
        if (!Jar.Exists)
        {
            var fileBytes = await Client.GetByteArrayAsync(JarUrl);
            await File.WriteAllBytesAsync(Jar.FullName, fileBytes);
        }   
        Jvm.AddClassPath(Jar.FullName);
        await base.InitializeAsync();
    }

    public override Task DisposeAsync()
    {
        Client.Dispose();
        return base.DisposeAsync();
    }
}