using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopin.Migrations
{
    /// <inheritdoc />
    public partial class loopin16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Yorumlar_Yorumlar_ParentId",
                table: "Yorumlar");

            migrationBuilder.DropIndex(
                name: "IX_Yorumlar_ParentId",
                table: "Yorumlar");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Yorumlar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "Yorumlar",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Yorumlar_ParentId",
                table: "Yorumlar",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Yorumlar_Yorumlar_ParentId",
                table: "Yorumlar",
                column: "ParentId",
                principalTable: "Yorumlar",
                principalColumn: "Id");
        }
    }
}
