namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public record VersionDto(string Sha, string Env, DateTimeOffset Timestamp);
