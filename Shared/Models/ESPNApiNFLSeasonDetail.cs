using FourPlayWebApp.Shared.Helpers;
using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models;
public class EspnApiNflSeasonDetail {
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("year")]
    public long Year { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("displayName")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long DisplayName { get; set; }

    [JsonPropertyName("type")]
    public SeasonDetailType Type { get; set; }

    [JsonPropertyName("types")]
    public Types Types { get; set; }

    [JsonPropertyName("rankings")]
    public Athletes Rankings { get; set; }

    [JsonPropertyName("coaches")]
    public Athletes Coaches { get; set; }

    [JsonPropertyName("athletes")]
    public Athletes Athletes { get; set; }

    [JsonPropertyName("futures")]
    public Athletes Futures { get; set; }

    [JsonPropertyName("leaders")]
    public Athletes Leaders { get; set; }
}

public class Athletes {
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }
}

public class SeasonDetailType {
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public long Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }

    [JsonPropertyName("year")]
    public long Year { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("hasGroups")]
    public bool HasGroups { get; set; }

    [JsonPropertyName("hasStandings")]
    public bool HasStandings { get; set; }

    [JsonPropertyName("hasLegs")]
    public bool HasLegs { get; set; }

    [JsonPropertyName("groups")]
    public Athletes Groups { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("week")]
    public WeekSeasonDetail Week { get; set; }

    [JsonPropertyName("weeks")]
    public Athletes Weeks { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("corrections")]
    public Athletes Corrections { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("leaders")]
    public Athletes Leaders { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; }
}

public class WeekSeasonDetail {
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("number")]
    public long Number { get; set; }

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("teamsOnBye")]
    public Athletes[] TeamsOnBye { get; set; }

    [JsonPropertyName("rankings")]
    public Athletes Rankings { get; set; }

    [JsonPropertyName("events")]
    public Athletes Events { get; set; }

    [JsonPropertyName("talentpicks")]
    public Athletes Talentpicks { get; set; }

    [JsonPropertyName("qbr")]
    public Athletes Qbr { get; set; }
}

public class Types {
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("count")]
    public long Count { get; set; }

    [JsonPropertyName("pageIndex")]
    public long PageIndex { get; set; }

    [JsonPropertyName("pageSize")]
    public long PageSize { get; set; }

    [JsonPropertyName("pageCount")]
    public long PageCount { get; set; }

    [JsonPropertyName("items")]
    public SeasonDetailType[] Items { get; set; }
}
