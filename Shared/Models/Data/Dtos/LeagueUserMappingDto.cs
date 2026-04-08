using System;

namespace FourPlayWebApp.Shared.Models.Data.Dtos
{
    public class LeagueUserMappingDto : IEquatable<LeagueUserMappingDto>
    {
        public int Id { get; set; }
        public int LeagueId { get; set; }
        public string? LeagueOwnerUserId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; } = string.Empty;
        public string? LeagueName { get; set; } = string.Empty;
        public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;

        // IEquatable implementation
        public bool Equals(LeagueUserMappingDto? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return Id == other.Id &&
                   LeagueId == other.LeagueId &&
                   string.Equals(LeagueOwnerUserId, other.LeagueOwnerUserId, StringComparison.Ordinal) &&
                   string.Equals(UserId, other.UserId, StringComparison.Ordinal) &&
                   string.Equals(UserName, other.UserName, StringComparison.Ordinal) &&
                   string.Equals(LeagueName, other.LeagueName, StringComparison.Ordinal); 
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as LeagueUserMappingDto);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                Id,
                LeagueId,
                LeagueOwnerUserId,
                UserId,
                UserName,
                LeagueName
            );
        }

        public static bool operator ==(LeagueUserMappingDto? left, LeagueUserMappingDto? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(LeagueUserMappingDto? left, LeagueUserMappingDto? right)
        {
            return !(left == right);
        }
    }
}
