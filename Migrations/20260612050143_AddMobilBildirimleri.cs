using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopin.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilBildirimleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobilBildirimleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KullaniciId = table.Column<int>(type: "INTEGER", nullable: false),
                    VideoId = table.Column<int>(type: "INTEGER", nullable: true),
                    KanalKullaniciId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tip = table.Column<string>(type: "TEXT", nullable: false),
                    Baslik = table.Column<string>(type: "TEXT", nullable: false),
                    Mesaj = table.Column<string>(type: "TEXT", nullable: false),
                    Okundu = table.Column<bool>(type: "INTEGER", nullable: false),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OkunmaTarihi = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobilBildirimleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilBildirimleri_Kullanicilar_KanalKullaniciId",
                        column: x => x.KanalKullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MobilBildirimleri_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MobilBildirimleri_Videolar_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MobilCihazTokenlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    KullaniciId = table.Column<int>(type: "INTEGER", nullable: false),
                    Token = table.Column<string>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    CihazId = table.Column<string>(type: "TEXT", nullable: true),
                    Aktif = table.Column<bool>(type: "INTEGER", nullable: false),
                    KayitTarihi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GuncellenmeTarihi = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobilCihazTokenlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MobilCihazTokenlari_Kullanicilar_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "Kullanicilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobilBildirimleri_KanalKullaniciId",
                table: "MobilBildirimleri",
                column: "KanalKullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilBildirimleri_KullaniciId_Okundu_OlusturulmaTarihi",
                table: "MobilBildirimleri",
                columns: new[] { "KullaniciId", "Okundu", "OlusturulmaTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilBildirimleri_VideoId",
                table: "MobilBildirimleri",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "IX_MobilCihazTokenlari_KullaniciId_Aktif",
                table: "MobilCihazTokenlari",
                columns: new[] { "KullaniciId", "Aktif" });

            migrationBuilder.CreateIndex(
                name: "IX_MobilCihazTokenlari_Token",
                table: "MobilCihazTokenlari",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobilBildirimleri");

            migrationBuilder.DropTable(
                name: "MobilCihazTokenlari");
        }
    }
}
