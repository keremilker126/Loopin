using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopin.Migrations
{
    /// <inheritdoc />
    public partial class loopin14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "EmailOnayli",
                table: "Kullanicilar",
                type: "INTEGER",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailOnayExpire",
                table: "Kullanicilar",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailOnayToken",
                table: "Kullanicilar",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailOnayExpire",
                table: "Kullanicilar");

            migrationBuilder.DropColumn(
                name: "EmailOnayToken",
                table: "Kullanicilar");

            migrationBuilder.AlterColumn<bool>(
                name: "EmailOnayli",
                table: "Kullanicilar",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");
        }
    }
}
