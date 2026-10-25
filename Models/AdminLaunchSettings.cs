using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ScopeCLI.Models
{
    public class AdminLaunchSettings
    {
        [JsonProperty("profileId")]
        [JsonPropertyName("profileId")]
        public string? ProfileId { get; set; }

        [JsonProperty("accountNickName")]
        [JsonPropertyName("accountNickName")]
        public string AccountNickName { get; set; } = string.Empty;

        [JsonProperty("gameVersion")]
        [JsonPropertyName("gameVersion")]
        public string GameVersion { get; set; } = string.Empty;

        [JsonProperty("modLoader")]
        [JsonPropertyName("modLoader")]
        public string ModLoader { get; set; } = string.Empty;

        [JsonProperty("serverAddress")]
        [JsonPropertyName("serverAddress")]
        public string? ServerAddress { get; set; }

        [JsonProperty("maxRamMb")]
        [JsonPropertyName("maxRamMb")]
        public int? MaxRamMb { get; set; }

        public AdminLaunchSettings() { }

        public AdminLaunchSettings(Profile profile, int? maxRamMb = null)
        {
            ProfileId = profile.ShortId;
            AccountNickName = profile.Nickname;
            GameVersion = profile.GameVersion;
            ModLoader = profile.ModLoader;
            ServerAddress = profile.ServerAddress;
            MaxRamMb = maxRamMb ?? profile.MaxRamMb;
        }
    }
}
