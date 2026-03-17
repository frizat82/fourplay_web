using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Models;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests
{
    public class InvitationServiceTests
    {
        [Fact]
        public async Task CreateInvitationAsync_ValidData_ReturnsInvitation()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "InvitationServiceTestDb")
                .Options;
            var dbContext = new ApplicationDbContext(options);
            var dbContextFactory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
            dbContextFactory.CreateDbContextAsync().Returns(dbContext);
            var service = new InvitationService(dbContextFactory);

            // Act
            var result = await service.CreateInvitationAsync("test@example.com", "user123");

            // Assert
            Assert.Equal("test@example.com", result.Email);
            Assert.Equal("user123", result.InvitedByUserId);
        }
    }
}
