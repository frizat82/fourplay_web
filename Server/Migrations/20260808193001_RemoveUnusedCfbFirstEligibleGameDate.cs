using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedCfbFirstEligibleGameDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstEligibleGameDate",
                table: "CfbSeasonWeekConfigs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstEligibleGameDate",
                table: "CfbSeasonWeekConfigs",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
