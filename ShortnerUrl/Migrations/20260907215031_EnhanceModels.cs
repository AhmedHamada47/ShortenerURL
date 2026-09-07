using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShortnerUrl.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "UrlClicks");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "UrlShorteners",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "IpHash",
                table: "UrlClicks",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                table: "ApiKeys",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UrlShorteners_LongUrl",
                table: "UrlShorteners",
                column: "LongUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UrlShorteners_LongUrl",
                table: "UrlShorteners");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "UrlShorteners");

            migrationBuilder.DropColumn(
                name: "IpHash",
                table: "UrlClicks");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "ApiKeys");

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "UrlClicks",
                type: "TEXT",
                nullable: true);
        }
    }
}
