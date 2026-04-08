using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services
{
    public class RefreshTokenService(IDbContextFactory<ApplicationDbContext> dbFactory) : IRefreshTokenService {
        public async Task<RefreshToken> IssueTokenAsync(ApplicationUser user, TimeSpan lifetime)
        {
            var token = GenerateSecureToken();
            var refreshToken = new RefreshToken
            {
                UserId = user.Id,
                Token = token,
                Expires = DateTimeOffset.UtcNow.Add(lifetime),
                Created = DateTimeOffset.UtcNow
            };
            await using var db = await dbFactory.CreateDbContextAsync();
            db.RefreshTokens.Add(refreshToken);
            await db.SaveChangesAsync();
            return refreshToken;
        }

        public async Task<RefreshToken?> ValidateTokenAsync(string token)
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var refreshToken = await db.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token);
            if (refreshToken == null || refreshToken.Expires < DateTimeOffset.UtcNow || refreshToken.Revoked != null)
                return null;
            return refreshToken;
        }

        public async Task<RefreshToken?> RotateTokenAsync(string oldToken, ApplicationUser user, TimeSpan lifetime)
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var existing = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == oldToken);
            if (existing == null || existing.Revoked != null || existing.Expires < DateTimeOffset.UtcNow)
                return null;
            // Revoke old token
            existing.Revoked = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            // Issue new token in its own context
            return await IssueTokenAsync(user, lifetime);
        }

        public async Task RevokeTokenAsync(string token)
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var refreshToken = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == token);
            if (refreshToken != null && refreshToken.Revoked == null)
            {
                refreshToken.Revoked = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        public async Task RevokeAllUserTokensAsync(string userId)
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var tokens = await db.RefreshTokens.Where(rt => rt.UserId == userId && rt.Revoked == null).ToListAsync();
            foreach (var token in tokens)
                token.Revoked = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        private static string GenerateSecureToken()
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}

