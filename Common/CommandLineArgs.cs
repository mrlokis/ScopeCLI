using System;
using System.Collections.Generic;

namespace ScopeCLI.Common
{
    public enum CliCommandType
    {
        Interactive,
        Launch,
        ListProfiles,
        ShowProfile,
        DeleteProfile,
        Optimize,
        AdminRelaunch,
        Help
    }

    public class CommandLineArgs
    {
        public CliCommandType Command { get; set; } = CliCommandType.Interactive;
        public string? ProfileQuery { get; set; }
        public string? ServerAddress { get; set; }
        public int? MaxRamMb { get; set; }
        public bool Optimize { get; set; }
        public bool AggressiveKill { get; set; }
        public string? AdminPayload { get; set; }

        public static CommandLineArgs Parse(string[] args)
        {
            var result = new CommandLineArgs();

            if (args.Length == 0)
            {
                result.Command = CliCommandType.Interactive;
                return result;
            }

            var queue = new Queue<string>(args);
            string first = queue.Dequeue();

            switch (first.ToLowerInvariant())
            {
                case "--help":
                case "-h":
                case "help":
                    result.Command = CliCommandType.Help;
                    return result;

                case "--admin-relaunch":
                    result.Command = CliCommandType.AdminRelaunch;
                    if (queue.Count > 0) result.AdminPayload = queue.Dequeue();
                    return result;

                case "launch":
                    result.Command = CliCommandType.Launch;
                    break;

                case "profiles":
                case "profile":
                    string sub = queue.Count > 0 ? queue.Dequeue().ToLowerInvariant() : "list";
                    if (sub == "list" || sub == "ls")
                    {
                        result.Command = CliCommandType.ListProfiles;
                    }
                    else if (sub == "show" || sub == "get")
                    {
                        result.Command = CliCommandType.ShowProfile;
                        if (queue.Count > 0) result.ProfileQuery = queue.Dequeue();
                    }
                    else if (sub == "delete" || sub == "rm")
                    {
                        result.Command = CliCommandType.DeleteProfile;
                        if (queue.Count > 0) result.ProfileQuery = queue.Dequeue();
                    }
                    else
                    {
                        result.Command = CliCommandType.ListProfiles;
                    }
                    return result;

                case "optimize":
                    result.Command = CliCommandType.Optimize;
                    while (queue.Count > 0)
                    {
                        string opt = queue.Dequeue().ToLowerInvariant();
                        if (opt is "--aggressive" or "-a" or "--kill")
                            result.AggressiveKill = true;
                    }
                    return result;

                default:
                    // Check if it's a legacy single base64 payload
                    if (args.Length == 1 && IsLikelyBase64(first))
                    {
                        result.Command = CliCommandType.AdminRelaunch;
                        result.AdminPayload = first;
                        return result;
                    }

                    if (first.StartsWith('-'))
                    {
                        result.Command = CliCommandType.Help;
                        return result;
                    }

                    result.Command = CliCommandType.Launch;
                    result.ProfileQuery = first;
                    break;
            }

            while (queue.Count > 0)
            {
                string arg = queue.Dequeue();
                switch (arg.ToLowerInvariant())
                {
                    case "--profile":
                    case "-p":
                        if (queue.Count > 0) result.ProfileQuery = queue.Dequeue();
                        break;
                    case "--server":
                    case "-s":
                        if (queue.Count > 0) result.ServerAddress = queue.Dequeue();
                        break;
                    case "--ram":
                    case "-r":
                        if (queue.Count > 0 && int.TryParse(queue.Dequeue(), out int ram))
                            result.MaxRamMb = ram;
                        break;
                    case "--optimize":
                    case "-o":
                        result.Optimize = true;
                        break;
                }
            }

            return result;
        }

        private static bool IsLikelyBase64(string str)
        {
            if (string.IsNullOrWhiteSpace(str) || str.Length < 16 || str.Length % 4 != 0)
                return false;

            try
            {
                Span<byte> buffer = new byte[str.Length];
                return Convert.TryFromBase64String(str, buffer, out _);
            }
            catch
            {
                return false;
            }
        }
    }
}
