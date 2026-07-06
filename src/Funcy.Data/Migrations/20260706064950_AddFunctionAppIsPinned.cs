using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funcy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFunctionAppIsPinned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "FunctionApps",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "FunctionApps");
        }
    }
}
