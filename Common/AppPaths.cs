using System;
using System.IO;

namespace ScopeCLI.Common
{
    public static class AppPaths
    {
        private static readonly string RootDirectory = AppDomain.CurrentDomain.BaseDirectory;

        public static string BaseDir => RootDirectory;

        public static string ProfilesDir => Path.Combine(RootDirectory, "profiles");

        public static string ProfilesDataDir => Path.Combine(RootDirectory, "profiles_data");

        public static string MinecraftDir => Path.Combine(RootDirectory, "minecraft");

        public static string ModsDir => Path.Combine(MinecraftDir, "mods");

        public static string ConfigDir => Path.Combine(MinecraftDir, "config");

        public static string TempDir => Path.Combine(RootDirectory, "temp");

        public static void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(ProfilesDir);
            Directory.CreateDirectory(ProfilesDataDir);
            Directory.CreateDirectory(MinecraftDir);
            Directory.CreateDirectory(ModsDir);
        }

        public static string GetProfilePath(string shortId) =>
            Path.Combine(ProfilesDir, $"{shortId}.upf");

        public static string GetProfileDataDir(string shortId) =>
            Path.Combine(ProfilesDataDir, shortId);

        public static string GetProfileConfigArchive(string shortId) =>
            Path.Combine(GetProfileDataDir(shortId), "config.zip");

        public static string GetProfileModsArchive(string shortId) =>
            Path.Combine(GetProfileDataDir(shortId), "mods.zip");
    }
}
