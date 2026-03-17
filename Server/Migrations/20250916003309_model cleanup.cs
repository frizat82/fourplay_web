using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class modelcleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            try {
            migrationBuilder.DropForeignKey(
                name: "FK_LeagueUserMapping_LeagueInfo_LeagueInfoId",
                table: "LeagueUserMapping");
            }
            catch (Exception ex) { }
            /*
            try {
            migrationBuilder.DropForeignKey(
                name: "FK_NflPicks_LeagueInfo_LeagueInfoId",
                table: "NflPicks");
            }
            catch (Exception ex) { }*/
            /*
            try {
            migrationBuilder.DropIndex(
                name: "IX_NflPicks_LeagueInfoId",
                table: "NflPicks");
            }
            catch (Exception ex) { }
            */
            try {
            migrationBuilder.DropIndex(
                name: "IX_LeagueUserMapping_LeagueInfoId",
                table: "LeagueUserMapping");
            }
            catch (Exception ex) { }
            /*
            try {
                migrationBuilder.DropColumn(
                    name: "LeagueInfoId",
                    table: "NflPicks");
            }
            catch (Exception ex) { }
            
*/
            migrationBuilder.DropColumn(
                name: "LeagueInfoId",
                table: "LeagueUserMapping");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeagueInfoId",
                table: "NflPicks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LeagueInfoId",
                table: "LeagueUserMapping",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NflPicks_LeagueInfoId",
                table: "NflPicks",
                column: "LeagueInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueUserMapping_LeagueInfoId",
                table: "LeagueUserMapping",
                column: "LeagueInfoId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeagueUserMapping_LeagueInfo_LeagueInfoId",
                table: "LeagueUserMapping",
                column: "LeagueInfoId",
                principalTable: "LeagueInfo",
                principalColumn: "Id");

            
            
            migrationBuilder.AddForeignKey(
                name: "FK_NflPicks_LeagueInfo_LeagueInfoId",
                table: "NflPicks",
                column: "LeagueInfoId",
                principalTable: "LeagueInfo",
                principalColumn: "Id");
        }
    }
}
