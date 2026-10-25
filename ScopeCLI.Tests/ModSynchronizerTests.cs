using System.IO;
using System.Threading.Tasks;
using ScopeCLI.Services;
using Xunit;

namespace ScopeCLI.Tests
{
    public class ModSynchronizerTests
    {
        [Fact]
        public async Task ParseModListAsync_ValidFileWithCommentsAndSpaces_ParsesCorrectly()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                string content = @"
# Header comment
sodium-fabric.jar | https://cdn.example.com/sodium.jar
iris-fabric.jar|https://cdn.example.com/iris.jar

# Another comment
fabric-api.jar |https://cdn.example.com/fabric-api.jar
";
                await File.WriteAllTextAsync(tempFile, content);

                var synchronizer = new ModSynchronizer();
                var mods = await synchronizer.ParseModListAsync(tempFile);

                Assert.Equal(3, mods.Count);

                Assert.Equal("sodium-fabric.jar", mods[0].FileName);
                Assert.Equal("https://cdn.example.com/sodium.jar", mods[0].Url);

                Assert.Equal("iris-fabric.jar", mods[1].FileName);
                Assert.Equal("https://cdn.example.com/iris.jar", mods[1].Url);

                Assert.Equal("fabric-api.jar", mods[2].FileName);
                Assert.Equal("https://cdn.example.com/fabric-api.jar", mods[2].Url);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task ParseModListAsync_EmptyFile_ReturnsEmptyList()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempFile, "# Only comments\n   \n# More comments");

                var synchronizer = new ModSynchronizer();
                var mods = await synchronizer.ParseModListAsync(tempFile);

                Assert.Empty(mods);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }
}
