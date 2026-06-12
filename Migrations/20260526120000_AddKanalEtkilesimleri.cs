using System;
using Loopin.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopin.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260526120000_AddKanalEtkilesimleri")]
    public partial class AddKanalEtkilesimleri : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KanalEtkilesimleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tur = table.Column<string>(type: "TEXT", nullable: false),
                    Tarih = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VideoId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KanalEtkilesimleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KanalEtkilesimleri_Videolar_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videolar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KanalEtkilesimleri_VideoId_Tarih_Tur",
                table: "KanalEtkilesimleri",
                columns: new[] { "VideoId", "Tarih", "Tur" });

            migrationBuilder.Sql(
                """
                INSERT INTO KanalEtkilesimleri (Tur, Tarih, VideoId)
                SELECT 'Goruntulenme', IzlenmeTarihi, VideoId
                FROM GecmisListesi;

                INSERT INTO KanalEtkilesimleri (Tur, Tarih, VideoId)
                SELECT 'Begeni', Tarih, VideoId
                FROM Begenmeler;

                INSERT INTO KanalEtkilesimleri (Tur, Tarih, VideoId)
                SELECT 'DahaSonraIzle', EklenmeTarihi, VideoId
                FROM DahaSonraIzleListesi;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "KanalEtkilesimleri");
        }
    }
}
