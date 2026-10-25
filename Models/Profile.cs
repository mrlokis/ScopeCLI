using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ScopeCLI.Models
{
    public class Profile
    {
        [JsonProperty("shortId")]
        [JsonPropertyName("shortId")]
        public string ShortId { get; set; } = string.Empty;

        [JsonProperty("name")]
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("nickname")]
        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonProperty("gameVersion")]
        [JsonPropertyName("gameVersion")]
        public string GameVersion { get; set; } = string.Empty;

        [JsonProperty("modLoader")]
        [JsonPropertyName("modLoader")]
        public string ModLoader { get; set; } = "Vanilla";

        [JsonProperty("serverAddress")]
        [JsonPropertyName("serverAddress")]
        public string? ServerAddress { get; set; }

        [JsonProperty("mods")]
        [JsonPropertyName("mods")]
        public List<ModEntry> Mods { get; set; } = new();

        [JsonProperty("isIsolationEnabled")]
        [JsonPropertyName("isIsolationEnabled")]
        public bool IsIsolationEnabled { get; set; }

        [JsonProperty("maxRamMb")]
        [JsonPropertyName("maxRamMb")]
        public int? MaxRamMb { get; set; }

        [JsonProperty("javaPath")]
        [JsonPropertyName("javaPath")]
        public string? JavaPath { get; set; }

        public string GetBaseGameVersion()
        {
            if (string.IsNullOrEmpty(GameVersion))
                return string.Empty;

            int dashIndex = GameVersion.IndexOf('-');
            return dashIndex > 0 ? GameVersion.Substring(0, dashIndex) : GameVersion;
        }
    }
}
