using System;
using System.Threading.Tasks;
using FourPlayWebApp.Server.Models.Identity;

namespace FourPlayWebApp.Server.Services.Interfaces
{
    public interface IRefreshTokenService
    {
        Task<RefreshToken> IssueTokenAsync(ApplicationUser user, TimeSpan lifetime);
        Task<RefreshToken?> ValidateTokenAsync(string token);
        Task<RefreshToken?> RotateTokenAsync(string oldToken, ApplicationUser user, TimeSpan lifetime);
        Task RevokeTokenAsync(string token);
        Task RevokeAllUserTokensAsync(string userId);
    }
}

