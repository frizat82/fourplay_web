using FourPlayWebApp.Shared.Helpers;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models;

public class EspnScores
{
    [JsonPropertyName("leagues")]
    public EspnLeague[]? Leagues { get; set; }
    [JsonPropertyName("season")]
    public Season? Season { get; set; }
    [JsonPropertyName("week")]
    public Week? Week { get; set; }
    [JsonPropertyName("events")]
    public Event[]? Events { get; set; }
}

public class Event
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("season")]
    public Season Season { get; set; }
    [JsonPropertyName("week")]
    public Week Week { get; set; }
    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; set; }
    [JsonPropertyName("competitions")]
    public Competition[] Competitions { get; set; }
    [JsonPropertyName("weather")]
    public EspnWeather? Weather { get; set; }
}

public class EspnWeather
{
    [JsonPropertyName("displayValue")]
    public string DisplayValue { get; set; }

    [JsonPropertyName("temperature")]
    public int Temperature { get; set; }

    [JsonPropertyName("highTemperature")]
    public int HighTemperature { get; set; }

    [JsonPropertyName("conditionId")]
    public string ConditionId { get; set; }
}
public class Competition
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("date")]
    public DateTimeOffset Date { get; set; }
    [JsonPropertyName("competitors")]
    public Competitor[] Competitors { get; set; }
    [JsonPropertyName("status")]
    public EspnStatus Status { get; set; }

    [JsonPropertyName("odds")]
    public Odd[] Odds { get; set; }

    [JsonPropertyName("situation")]
    public EspnSitutation Situation { get; set; }

    public override int GetHashCode() =>
        HashCode.Combine(Date.ToString("yyyyMMddHHmmss"), Competitors[0].Team.Abbreviation, Competitors[1].Team.Abbreviation);
}

public class EspnSitutation {
    [JsonPropertyName("down")]
    public int Down { get; set; }

    [JsonPropertyName("yardLine")]
    public int YardLine { get; set; }

    [JsonPropertyName("distance")]
    public int Distance { get; set; }

    [JsonPropertyName("downDistanceText")]
    public string DownDistanceText { get; set; }

    [JsonPropertyName("shortDownDistanceText")]
    public string ShortDownDistanceText { get; set; }

    [JsonPropertyName("possessionText")]
    public string PossessionText { get; set; }

    [JsonPropertyName("isRedZone")]
    public bool? IsRedZone { get; set; }

    [JsonPropertyName("homeTimeouts")]
    public int HomeTimeouts { get; set; }

    [JsonPropertyName("awayTimeouts")]
    public int AwayTimeouts { get; set; }

    [JsonPropertyName("possession")]
    public string Possession { get; set; }
}
public class Competitor
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("homeAway")]
    public HomeAway HomeAway { get; set; }
    [JsonPropertyName("team")]
    public EspnTeam Team { get; set; }
    [JsonPropertyName("score")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long Score { get; set; }
    [JsonPropertyName("records")]
    public EspnRecord[] Records { get; set; }
}
public class EspnRecord
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("type")]
    public EspnRecordType Type { get; set; }
    [JsonPropertyName("summary")]
    public string Summary { get; set; }
}


public class EspnTeam
{
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }
    [JsonPropertyName("logo")]
    public Uri Logo { get; set; }
}

public class Odd
{
    [JsonPropertyName("provider")]
    public OddsProvider Provider { get; set; }
    [JsonPropertyName("details")]
    public string Details { get; set; }
    [JsonPropertyName("overUnder")]
    public double OverUnder { get; set; }
}

public class OddsProvider
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("priority")]
    public long Priority { get; set; }
}

public class EspnStatus
{
    [JsonPropertyName("clock")]
    public double Clock { get; set; }
    [JsonPropertyName("displayClock")]
    public string DisplayClock { get; set; }
    [JsonPropertyName("period")]
    public long Period { get; set; }
    [JsonPropertyName("type")]
    public StatusType Type { get; set; }
}

public class StatusType
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long Id { get; set; }
    [JsonPropertyName("name")]
    public TypeName Name { get; set; }
    [JsonPropertyName("state")]
    public State State { get; set; }
    [JsonPropertyName("completed")]
    public bool Completed { get; set; }
    [JsonPropertyName("description")]
    public Description Description { get; set; }
    [JsonPropertyName("detail")]
    public string Detail { get; set; }
    [JsonPropertyName("shortDetail")]
    public string ShortDetail { get; set; }
}

public class EspnLeague
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long Id { get; set; }
    [JsonPropertyName("uid")]
    public string Uid { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }
    [JsonPropertyName("slug")]
    public string Slug { get; set; }
    [JsonPropertyName("season")]
    public LeagueSeason Season { get; set; }
    [JsonPropertyName("logos")]
    public Logo[] Logos { get; set; }
    [JsonPropertyName("calendarType")]
    public string CalendarType { get; set; }
    [JsonPropertyName("calendarIsWhitelist")]
    public bool CalendarIsWhitelist { get; set; }
    [JsonPropertyName("calendarStartDate")]
    public string CalendarStartDate { get; set; }
    [JsonPropertyName("calendarEndDate")]
    public string CalendarEndDate { get; set; }
    [JsonPropertyName("calendar")]
    public EspnCalendar[] Calendar { get; set; }
}

public class EspnCalendar
{
    [JsonPropertyName("label")]
    public string Label { get; set; }
    [JsonPropertyName("value")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long Value { get; set; }
    [JsonPropertyName("startDate")]
    public string StartDate { get; set; }
    [JsonPropertyName("endDate")]
    public string EndDate { get; set; }
    [JsonPropertyName("entries")]
    public CalendarEntry[] Entries { get; set; }
}

public class CalendarEntry {
    [JsonPropertyName("label")] public string Label { get; set; }

    [JsonPropertyName("alternateLabel")] public string AlternateLabel { get; set; }

    [JsonPropertyName("detail")] public string Detail { get; set; }

    [JsonPropertyName("value")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long Value { get; set; }
    [JsonPropertyName("startDate")] public DateTimeOffset StartDate { get; set; }

    [JsonPropertyName("endDate")] public DateTimeOffset EndDate { get; set; }
}

public class Logo
{
    [JsonPropertyName("href")]
    public Uri Href { get; set; }
    [JsonPropertyName("width")]
    public long Width { get; set; }
    [JsonPropertyName("height")]
    public long Height { get; set; }
    [JsonPropertyName("alt")]
    public string Alt { get; set; }
    [JsonPropertyName("rel")]
    public string[] Rel { get; set; }
    [JsonPropertyName("lastUpdated")]
    public string LastUpdated { get; set; }
}

public class LeagueSeason
{
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
    public SeasonType Type { get; set; }
}

public class SeasonType
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long Id { get; set; }
    [JsonPropertyName("type")]
    public long Type { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; }
}

public class Season
{
    [JsonPropertyName("type")]
    public long Type { get; set; }
    [JsonPropertyName("year")]
    public long Year { get; set; }
}

public class Week
{
    [JsonPropertyName("number")]
    public long Number { get; set; }
    [JsonPropertyName("teamsOnBye")]
    public EspnTeam[] TeamsOnBye { get; set; }
}
public enum TypeOfSeason { PostSeason = 3, RegularSeason = 2, PreSeason = 1 };
public enum EspnRecordType { Total, Road, Home }
public enum HomeAway { Away, Home }
public enum Description { Final, Halftime, InProgress, Scheduled, EndOfPeriod }
public enum TypeName { StatusFinal, StatusHalftime, StatusInProgress, StatusScheduled, StatusEndPeriod }
public enum State { In, Post, Pre }

public static class EspnApiServiceJsonConverter
{
    public static readonly JsonSerializerOptions Settings = new(JsonSerializerDefaults.General)
    {
        Converters =
        {
            HomeAwayConverter.Singleton,
            EspnRecordTypeConverter.Singleton,
            DescriptionConverter.Singleton,
            TypeNameConverter.Singleton,
            StateConverter.Singleton,
            new DateOnlyConverter(),
            new TimeOnlyConverter(),
            IsoDateTimeOffsetConverter.Singleton,
            new DateTimeConverterUsingDateTimeParse()
        },
    };
}
internal class EspnRecordTypeConverter : JsonConverter<EspnRecordType>
{
    public override EspnRecordType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "road" => EspnRecordType.Road,
            "home" => EspnRecordType.Home,
            "total" => EspnRecordType.Total,
            _ => throw new Exception("Cannot unmarshal type EspnRecordType")
        };

    public override void Write(Utf8JsonWriter writer, EspnRecordType value, JsonSerializerOptions options) {
        if (value == EspnRecordType.Road)
            writer.WriteStringValue("road");
        if (value == EspnRecordType.Home)
            writer.WriteStringValue("home");
        if (value == EspnRecordType.Total)
            writer.WriteStringValue("total");
    }

    public static readonly EspnRecordTypeConverter Singleton = new();
}
internal class HomeAwayConverter : JsonConverter<HomeAway>
{
    public override HomeAway Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "away" => HomeAway.Away,
            "home" => HomeAway.Home,
            _ => throw new Exception("Cannot unmarshal type HomeAway")
        };

    public override void Write(Utf8JsonWriter writer, HomeAway value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value == HomeAway.Away ? "away" : "home");

    public static readonly HomeAwayConverter Singleton = new();
}

internal class DescriptionConverter : JsonConverter<Description>
{
    public override Description Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "Final" => Description.Final,
            "Halftime" => Description.Halftime,
            "In Progress" => Description.InProgress,
            "Scheduled" => Description.Scheduled,
            "End of Period" => Description.EndOfPeriod,
            _ => throw new Exception("Cannot unmarshal type Description")
        };

    public override void Write(Utf8JsonWriter writer, Description value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            Description.Final => "Final",
            Description.Halftime => "Halftime",
            Description.InProgress => "In Progress",
            Description.Scheduled => "Scheduled",
            Description.EndOfPeriod => "End of Period",
            _ => throw new ArgumentOutOfRangeException()
        });

    public static readonly DescriptionConverter Singleton = new();
}

internal class TypeNameConverter : JsonConverter<TypeName>
{
    public override TypeName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "STATUS_FINAL" => TypeName.StatusFinal,
            "STATUS_HALFTIME" => TypeName.StatusHalftime,
            "STATUS_IN_PROGRESS" => TypeName.StatusInProgress,
            "STATUS_SCHEDULED" => TypeName.StatusScheduled,
            "STATUS_END_PERIOD" => TypeName.StatusEndPeriod,
            _ => throw new Exception("Cannot unmarshal type TypeName")
        };

    public override void Write(Utf8JsonWriter writer, TypeName value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            TypeName.StatusFinal => "STATUS_FINAL",
            TypeName.StatusHalftime => "STATUS_HALFTIME",
            TypeName.StatusInProgress => "STATUS_IN_PROGRESS",
            TypeName.StatusScheduled => "STATUS_SCHEDULED",
            TypeName.StatusEndPeriod => "STATUS_END_PERIOD",
            _ => throw new ArgumentOutOfRangeException()
        });

    public static readonly TypeNameConverter Singleton = new();
}

internal class StateConverter : JsonConverter<State>
{
    public override State Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "in" => State.In,
            "post" => State.Post,
            "pre" => State.Pre,
            _ => throw new Exception("Cannot unmarshal type State")
        };

    public override void Write(Utf8JsonWriter writer, State value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            State.In => "in",
            State.Post => "post",
            State.Pre => "pre",
            _ => throw new ArgumentOutOfRangeException()
        });

    public static readonly StateConverter Singleton = new();
}

public class DateOnlyConverter(string? format = null) : JsonConverter<DateOnly> {
    private readonly string _format = format ?? "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateOnly.Parse(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(_format));
}

public class TimeOnlyConverter(string? format = null) : JsonConverter<TimeOnly> {
    private readonly string _format = format ?? "HH:mm:ss.fff";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TimeOnly.Parse(reader.GetString()!);
    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(_format));
}

internal class IsoDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string _defaultFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.Parse(reader.GetString()!, CultureInfo.CurrentCulture, DateTimeStyles.RoundtripKind);
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(_defaultFormat, CultureInfo.CurrentCulture));
    public static readonly IsoDateTimeOffsetConverter Singleton = new();
}

internal class DateTimeConverterUsingDateTimeParse : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.Parse(reader.GetString() ?? string.Empty);
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
