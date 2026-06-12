using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopin.Migrations
{
    /// <inheritdoc />
    public partial class loopin6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Abonelikler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AboneOlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    AboneOlunanId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tarih = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Abonelikler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Abonelikler_Kullanicilar_AboneOlanId",
                        column: x => x.AboneOlanId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Abonelikler_Kullanicilar_AboneOlunanId",
                        column: x => x.AboneOlunanId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Abonelikler_AboneOlanId",
                table: "Abonelikler",
                column: "AboneOlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Abonelikler_AboneOlunanId",
                table: "Abonelikler",
                column: "AboneOlunanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Abonelikler");
        }
    }
}
