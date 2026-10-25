using System;
using System.IO;
using System.Threading.Tasks;
using ScopeCLI.Common;
using ScopeCLI.Models;
using ScopeCLI.Services;
using Xunit;

namespace ScopeCLI.Tests
{
    public class ProfileServiceTests
    {
        [Fact]
        public async Task SaveAndGetProfile_RoundTrip_WorksCorrectly()
        {
            var profileService = new ProfileService();
            string testId = profileService.GenerateUniqueShortId();

            var profile = new Profile
            {
                ShortId = testId,
                Name = "IntegrationTestProfile",
                Nickname = "TestPlayer",
                GameVersion = "1.20.1",
                ModLoader = "Forge",
                ServerAddress = "test.server:25565",
                IsIsolationEnabled = true,
                MaxRamMb = 4096
            };

            try
            {
                await profileService.SaveProfileAsync(profile);

                var loaded = await profileService.GetProfileByIdAsync(testId);
                Assert.NotNull(loaded);
                Assert.Equal("IntegrationTestProfile", loaded.Name);
                Assert.Equal("TestPlayer", loaded.Nickname);
                Assert.Equal("1.20.1", loaded.GameVersion);
                Assert.Equal("Forge", loaded.ModLoader);
                Assert.Equal("test.server:25565", loaded.ServerAddress);
                Assert.Equal(4096, loaded.MaxRamMb);
                Assert.True(loaded.IsIsolationEnabled);

                // Search by name
                var found = await profileService.FindProfileByNameOrIdAsync("IntegrationTestProfile");
                Assert.NotNull(found);
                Assert.Equal(testId, found.ShortId);

                // Delete profile
                bool deleted = await profileService.DeleteProfileAsync(testId);
                Assert.True(deleted);

                var deletedCheck = await profileService.GetProfileByIdAsync(testId);
                Assert.Null(deletedCheck);
            }
            finally
            {
                string path = AppPaths.GetProfilePath(testId);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
