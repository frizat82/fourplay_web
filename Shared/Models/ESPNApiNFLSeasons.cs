using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models;

public class EspnApiNflSeasons {
    [JsonPropertyName("count")]
    public long Count { get; set; }

    [JsonPropertyName("pageIndex")]
    public long PageIndex { get; set; }

    [JsonPropertyName("pageSize")]
    public long PageSize { get; set; }

    [JsonPropertyName("pageCount")]
    public long PageCount { get; set; }

    [JsonPropertyName("items")]
    public Item[] Items { get; set; }
}

public class Item {
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }
}
