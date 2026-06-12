using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopin.Migrations
{
    /// <inheritdoc />
    public partial class loopin7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DahaSonraIzleListesi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KullaniciId = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    EklenmeTarihi = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DahaSonraIzleListesi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DahaSonraIzleListesi_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DahaSonraIzleListesi_Videolar_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GecmisListesi",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KullaniciId = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoId = table.Column<int>(type: "INTEGER", nullable: false),
                    IzlenmeTarihi = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GecmisListesi", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GecmisListesi_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GecmisListesi_Videolar_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DahaSonraIzleListesi_KullaniciId",
                table: "DahaSonraIzleListesi",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_DahaSonraIzleListesi_VideoId",
                table: "DahaSonraIzleListesi",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_GecmisListesi_KullaniciId",
                table: "GecmisListesi",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_GecmisListesi_VideoId",
                table: "GecmisListesi",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DahaSonraIzleListesi");

            migrationBuilder.DropTable(
                name: "GecmisListesi");
        }
    }
}
