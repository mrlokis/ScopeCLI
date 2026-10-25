using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace ScopeCLI.Models
{
    public class ModEntry
    {
        [JsonProperty("fileName")]
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonProperty("url")]
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        public ModEntry() { }

        public ModEntry(string fileName, string url)
        {
            FileName = fileName;
            Url = url;
        }

        public override string ToString() => $"{FileName} ({Url})";
    }
}
