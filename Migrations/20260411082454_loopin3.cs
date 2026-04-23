using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopin.Migrations
{
    /// <inheritdoc />
    public partial class loopin3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IzlenmeSayisi",
                table: "Videolar",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LikeSayisi",
                table: "Videolar",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "YuklenmeTarihi",
                table: "Videolar",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IzlenmeSayisi",
                table: "Videolar");

            migrationBuilder.DropColumn(
                name: "LikeSayisi",
                table: "Videolar");

            migrationBuilder.DropColumn(
                name: "YuklenmeTarihi",
                table: "Videolar");
        }
    }
}
