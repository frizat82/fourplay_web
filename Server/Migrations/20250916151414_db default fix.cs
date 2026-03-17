using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class dbdefaultfix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NflPicks_UserId_LeagueId_NflWeek_Season_Team",
                table: "NflPicks");

            migrationBuilder.AlterColumn<int>(
                name: "Pick",
                table: "NflPicks",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 2);

            migrationBuilder.CreateIndex(
                name: "IX_NflPicks_UserId_LeagueId_NflWeek_Season_Team_Pick",
                table: "NflPicks",
                columns: new[] { "UserId", "LeagueId", "NflWeek", "Season", "Team", "Pick" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NflPicks_UserId_LeagueId_NflWeek_Season_Team_Pick",
                table: "NflPicks");

            migrationBuilder.AlterColumn<int>(
                name: "Pick",
                table: "NflPicks",
                type: "integer",
                nullable: false,
                defaultValue: 2,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_NflPicks_UserId_LeagueId_NflWeek_Season_Team",
                table: "NflPicks",
                columns: new[] { "UserId", "LeagueId", "NflWeek", "Season", "Team" },
                unique: true);
        }
    }
}
