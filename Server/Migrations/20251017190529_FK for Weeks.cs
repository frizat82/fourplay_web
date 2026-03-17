using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class FKforWeeks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NflWeekId",
                table: "NflPicks",
                type: "integer",
                nullable: false,
                defaultValue: 0);
            // 2️⃣ Populate using existing Season + NflWeek composite mapping
            migrationBuilder.Sql(@"
                UPDATE ""NflPicks"" p
                SET ""NflWeekId"" = w.""Id""
                FROM ""NflWeeks"" w
                WHERE p.""Season"" = w.""Season"" AND p.""NflWeek"" = w.""NflWeek"";");

            migrationBuilder.CreateIndex(
                name: "IX_NflPicks_NflWeekId",
                table: "NflPicks",
                column: "NflWeekId");

            migrationBuilder.AddForeignKey(
                name: "FK_NflPicks_NflWeeks_NflWeekId",
                table: "NflPicks",
                column: "NflWeekId",
                principalTable: "NflWeeks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NflPicks_NflWeeks_NflWeekId",
                table: "NflPicks");

            migrationBuilder.DropIndex(
                name: "IX_NflPicks_NflWeekId",
                table: "NflPicks");

            migrationBuilder.DropColumn(
                name: "NflWeekId",
                table: "NflPicks");
        }
    }
}
