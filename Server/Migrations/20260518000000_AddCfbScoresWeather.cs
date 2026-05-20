using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddCfbScoresWeather : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WeatherDisplayValue",
                table: "CfbScores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeatherConditionId",
                table: "CfbScores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeatherTemperatureF",
                table: "CfbScores",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "WeatherDisplayValue", table: "CfbScores");
            migrationBuilder.DropColumn(name: "WeatherConditionId",  table: "CfbScores");
            migrationBuilder.DropColumn(name: "WeatherTemperatureF", table: "CfbScores");
        }
    }
}
