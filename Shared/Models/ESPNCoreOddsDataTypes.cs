using System.Text.Json.Serialization;

namespace FourPlayWebApp.Shared.Models;

public class EspnCoreOddsApiResponse
{
    public int Count { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int PageCount { get; set; }
    public List<EspnCoreOddsItem> Items { get; set; } = new();
}

public class EspnCoreOddsItem
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;

    public EspnCoreOddsProvider Provider { get; set; } = new();
    public string Details { get; set; } = string.Empty;
    public double OverUnder { get; set; }
    public double Spread { get; set; }
    public double OverOdds { get; set; }
    public double UnderOdds { get; set; }

    public EspnCoreTeamOdds AwayTeamOdds { get; set; }
    public EspnCoreTeamOdds HomeTeamOdds { get; set; }

    public List<EspnCoreOddsLink> Links { get; set; } = new();

    public bool MoneylineWinner { get; set; }
    public bool SpreadWinner { get; set; }

    public EspnCoreOddsBreakdown Open { get; set; }
    public EspnCoreOddsBreakdown Close { get; set; }
    public EspnCoreOddsBreakdown Current { get; set; }
}

public class EspnCoreOddsProvider
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
}

public class EspnCoreTeamOdds
{
    public bool Favorite { get; set; }
    public bool Underdog { get; set; }
    public int MoneyLine { get; set; }
    public double SpreadOdds { get; set; }

    public EspnCoreTeamOddsDetail Open { get; set; }
    public EspnCoreTeamOddsDetail Close { get; set; }
    public EspnCoreTeamOddsDetail Current { get; set; }
    public EspnCoreTeamReference Team { get; set; }
}

public class EspnCoreTeamOddsDetail
{
    public EspnCorePointSpread PointSpread { get; set; }
    public EspnCoreSpread Spread { get; set; }
    public EspnCoreMoneyLine MoneyLine { get; set; }
}

public class EspnCorePointSpread
{
    public string AlternateDisplayValue { get; set; }
    public string American { get; set; }
}

public class EspnCoreSpread
{
    public double Value { get; set; }
    public string DisplayValue { get; set; }
    public string AlternateDisplayValue { get; set; }
    public double Decimal { get; set; }
    public string Fraction { get; set; }
    public string American { get; set; }
    public EspnCoreOutcome? Outcome { get; set; }
}

public class EspnCoreMoneyLine
{
    public double Value { get; set; }
    public string DisplayValue { get; set; }
    public string AlternateDisplayValue { get; set; }
    public double Decimal { get; set; }
    public string Fraction { get; set; }
    public string American { get; set; }
    public EspnCoreOutcome? Outcome { get; set; }
}

public class EspnCoreOutcome
{
    public string Type { get; set; }
}

public class EspnCoreTeamReference
{
    [JsonPropertyName("$ref")]
    public string Ref { get; set; }
}

public class EspnCoreOddsLink
{
    public string Language { get; set; }
    public List<string> Rel { get; set; }
    public string Href { get; set; }
    public string Text { get; set; }
    public string ShortText { get; set; }
    public bool IsExternal { get; set; }
    public bool IsPremium { get; set; }
}

public class EspnCoreOddsBreakdown
{
    public EspnCoreSpreadComponent Over { get; set; }
    public EspnCoreSpreadComponent Under { get; set; }
    public EspnCoreTotalComponent Total { get; set; }
}

public class EspnCoreSpreadComponent
{
    public double Value { get; set; }
    public string DisplayValue { get; set; }
    public string AlternateDisplayValue { get; set; }
    public double Decimal { get; set; }
    public string Fraction { get; set; }
    public string American { get; set; }
    public EspnCoreOutcome? Outcome { get; set; }
}

public class EspnCoreTotalComponent
{
    public string AlternateDisplayValue { get; set; }
    public string American { get; set; }
}
