using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopin.Migrations
{
    /// <inheritdoc />
    public partial class loopin9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResetCode",
                table: "Kullanicilar",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetCodeExpire",
                table: "Kullanicilar",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetCode",
                table: "Kullanicilar");

            migrationBuilder.DropColumn(
                name: "ResetCodeExpire",
                table: "Kullanicilar");
        }
    }
}
