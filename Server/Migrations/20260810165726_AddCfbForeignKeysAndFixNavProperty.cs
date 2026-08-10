using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCfbForeignKeysAndFixNavProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_CfbPicks_AspNetUsers_UserId",
                table: "CfbPicks",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CfbPicks_CfbSlates_CfbSlateId",
                table: "CfbPicks",
                column: "CfbSlateId",
                principalTable: "CfbSlates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CfbPicks_LeagueInfo_LeagueId",
                table: "CfbPicks",
                column: "LeagueId",
                principalTable: "LeagueInfo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CfbScores_CfbSlates_CfbSlateId",
                table: "CfbScores",
                column: "CfbSlateId",
                principalTable: "CfbSlates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CfbSpreads_CfbSlates_CfbSlateId",
                table: "CfbSpreads",
                column: "CfbSlateId",
                principalTable: "CfbSlates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CfbPicks_AspNetUsers_UserId",
                table: "CfbPicks");

            migrationBuilder.DropForeignKey(
                name: "FK_CfbPicks_CfbSlates_CfbSlateId",
                table: "CfbPicks");

            migrationBuilder.DropForeignKey(
                name: "FK_CfbPicks_LeagueInfo_LeagueId",
                table: "CfbPicks");

            migrationBuilder.DropForeignKey(
                name: "FK_CfbScores_CfbSlates_CfbSlateId",
                table: "CfbScores");

            migrationBuilder.DropForeignKey(
                name: "FK_CfbSpreads_CfbSlates_CfbSlateId",
                table: "CfbSpreads");
        }
    }
}
