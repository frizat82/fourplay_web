using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddWeekResultNotificationSent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeekResultNotificationSent",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Week = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeekResultNotificationSent", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeekResultNotificationSent_UserId_LeagueId_Season_Week",
                table: "WeekResultNotificationSent",
                columns: new[] { "UserId", "LeagueId", "Season", "Week" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeekResultNotificationSent");
        }
    }
}
