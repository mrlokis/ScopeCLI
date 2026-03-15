using Newtonsoft.Json;
using System.Collections.Generic;

namespace ScopeCLI.Models
{
    public class Profile
    {
        [JsonProperty("shortId")]
        public string ShortId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("nickname")]
        public string Nickname { get; set; }

        [JsonProperty("gameVersion")]
        public string GameVersion { get; set; }

        [JsonProperty("modLoader")]
        public string ModLoader { get; set; }

        [JsonProperty("serverAddress")]
        public string ServerAddress { get; set; }

        [JsonProperty("mods")]
        public List<ModEntry> Mods { get; set; }

        [JsonProperty("isIsolationEnabled")]
        public bool IsIsolationEnabled { get; set; }
    }
}