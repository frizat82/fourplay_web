using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToLeagueUserMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true (not EF's inferred `false` for bool) — every existing
            // LeagueUserMapping row is a currently-active member; defaulting to false would
            // silently soft-remove every real member in every league the moment this runs.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "LeagueUserMapping",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RemovedAt",
                table: "LeagueUserMapping",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "LeagueUserMapping");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "LeagueUserMapping");
        }
    }
}
