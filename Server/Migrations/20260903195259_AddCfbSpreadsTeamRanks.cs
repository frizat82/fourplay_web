using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCfbSpreadsTeamRanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AwayTeamRank",
                table: "CfbSpreads",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HomeTeamRank",
                table: "CfbSpreads",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwayTeamRank",
                table: "CfbSpreads");

            migrationBuilder.DropColumn(
                name: "HomeTeamRank",
                table: "CfbSpreads");
        }
    }
}
