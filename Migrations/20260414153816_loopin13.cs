using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loopin.Migrations
{
    /// <inheritdoc />
    public partial class loopin13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailOnayli",
                table: "Kullanicilar",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailOnayli",
                table: "Kullanicilar");
        }
    }
}
