using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCfbEspnEventIdUseNaturalKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CfbSpreads_CfbSlateId",
                table: "CfbSpreads");

            migrationBuilder.DropIndex(
                name: "IX_CfbSpreads_EspnEventId",
                table: "CfbSpreads");

            migrationBuilder.DropIndex(
                name: "IX_CfbScores_CfbSlateId",
                table: "CfbScores");

            migrationBuilder.DropIndex(
                name: "IX_CfbScores_EspnEventId",
                table: "CfbScores");

            migrationBuilder.DropColumn(
                name: "EspnEventId",
                table: "CfbSpreads");

            migrationBuilder.DropColumn(
                name: "EspnEventId",
                table: "CfbScores");

            migrationBuilder.DropColumn(
                name: "EspnEventId",
                table: "CfbPicks");

            migrationBuilder.CreateIndex(
                name: "IX_CfbSpreads_CfbSlateId_HomeTeam",
                table: "CfbSpreads",
                columns: new[] { "CfbSlateId", "HomeTeam" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CfbScores_CfbSlateId_HomeTeam",
                table: "CfbScores",
                columns: new[] { "CfbSlateId", "HomeTeam" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CfbSpreads_CfbSlateId_HomeTeam",
                table: "CfbSpreads");

            migrationBuilder.DropIndex(
                name: "IX_CfbScores_CfbSlateId_HomeTeam",
                table: "CfbScores");

            migrationBuilder.AddColumn<int>(
                name: "EspnEventId",
                table: "CfbSpreads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EspnEventId",
                table: "CfbScores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EspnEventId",
                table: "CfbPicks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CfbSpreads_CfbSlateId",
                table: "CfbSpreads",
                column: "CfbSlateId");

            migrationBuilder.CreateIndex(
                name: "IX_CfbSpreads_EspnEventId",
                table: "CfbSpreads",
                column: "EspnEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CfbScores_CfbSlateId",
                table: "CfbScores",
                column: "CfbSlateId");

            migrationBuilder.CreateIndex(
                name: "IX_CfbScores_EspnEventId",
                table: "CfbScores",
                column: "EspnEventId",
                unique: true);
        }
    }
}
