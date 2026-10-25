using ScopeCLI.Common;
using Xunit;

namespace ScopeCLI.Tests
{
    public class CommandLineArgsTests
    {
        [Fact]
        public void Parse_EmptyArgs_ReturnsInteractive()
        {
            var args = CommandLineArgs.Parse(Array.Empty<string>());
            Assert.Equal(CliCommandType.Interactive, args.Command);
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("help")]
        public void Parse_HelpFlags_ReturnsHelp(string flag)
        {
            var args = CommandLineArgs.Parse(new[] { flag });
            Assert.Equal(CliCommandType.Help, args.Command);
        }

        [Fact]
        public void Parse_LaunchWithFlags_ParsesAllOptions()
        {
            var args = CommandLineArgs.Parse(new[]
            {
                "launch",
                "-p", "MyCustomProfile",
                "-s", "mc.example.com:25565",
                "-r", "6144",
                "-o"
            });

            Assert.Equal(CliCommandType.Launch, args.Command);
            Assert.Equal("MyCustomProfile", args.ProfileQuery);
            Assert.Equal("mc.example.com:25565", args.ServerAddress);
            Assert.Equal(6144, args.MaxRamMb);
            Assert.True(args.Optimize);
        }

        [Fact]
        public void Parse_ProfilesList_ReturnsListProfiles()
        {
            var args = CommandLineArgs.Parse(new[] { "profiles", "list" });
            Assert.Equal(CliCommandType.ListProfiles, args.Command);
        }

        [Fact]
        public void Parse_ProfileShow_ReturnsShowProfile()
        {
            var args = CommandLineArgs.Parse(new[] { "profile", "show", "abc123" });
            Assert.Equal(CliCommandType.ShowProfile, args.Command);
            Assert.Equal("abc123", args.ProfileQuery);
        }

        [Fact]
        public void Parse_ProfileDelete_ReturnsDeleteProfile()
        {
            var args = CommandLineArgs.Parse(new[] { "profiles", "delete", "abc123" });
            Assert.Equal(CliCommandType.DeleteProfile, args.Command);
            Assert.Equal("abc123", args.ProfileQuery);
        }

        [Fact]
        public void Parse_OptimizeAggressive_ReturnsOptimizeWithKill()
        {
            var args = CommandLineArgs.Parse(new[] { "optimize", "--aggressive" });
            Assert.Equal(CliCommandType.Optimize, args.Command);
            Assert.True(args.AggressiveKill);
        }

        [Fact]
        public void Parse_AdminRelaunch_ReturnsAdminRelaunch()
        {
            string payload = "eyJBY2NvdW50Tmlja05hbWUiOiJUZXN0In0=";
            var args = CommandLineArgs.Parse(new[] { "--admin-relaunch", payload });
            Assert.Equal(CliCommandType.AdminRelaunch, args.Command);
            Assert.Equal(payload, args.AdminPayload);
        }
    }
}
