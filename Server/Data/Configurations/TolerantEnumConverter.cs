using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Serilog;

namespace FourPlayWebApp.Server.Data.Configurations;

// /code-review: a plain HasConversion<string>() throws on read for any stored value that doesn't
// exactly match an enum member name — one malformed row would fail the entire query instead of
// degrading gracefully. This is the tolerant version: an unparseable value logs a warning and
// falls back to the given default rather than throwing, keeping the rest of the result set intact.
public static class TolerantEnumConverter {
    public static TEnum Parse<TEnum>(string? raw, TEnum fallback) where TEnum : struct, System.Enum {
        if (!string.IsNullOrEmpty(raw) && System.Enum.TryParse<TEnum>(raw, out var parsed)) return parsed;
        Log.Warning("TolerantEnumConverter: unparseable {EnumType} value {Raw} — falling back to {Fallback}", typeof(TEnum).Name, raw, fallback);
        return fallback;
    }

    public static ValueConverter<TEnum, string> Create<TEnum>(TEnum fallback) where TEnum : struct, System.Enum =>
        new(v => v.ToString(), v => Parse(v, fallback));
}
