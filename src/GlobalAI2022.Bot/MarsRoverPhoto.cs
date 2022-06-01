using System.Text.Json.Serialization;

namespace GlobalAI2022.Bot;

[Serializable]
internal class MarsRoverPhotos
{
    [JsonPropertyName(@"photos")]
    public List<MarsRoverPhoto> Photos { get; set; }
}

[Serializable]
internal class MarsRoverPhoto
{
    [JsonPropertyName(@"id")]
    public int Id { get; set; }

    [JsonPropertyName(@"sol")]
    public int Sol { get; set; }

    [JsonPropertyName(@"img_src")]
    public string ImageSource { get; set; }

    [JsonPropertyName(@"earth_date")]
    public DateTime EarthDate { get; set; }
}
