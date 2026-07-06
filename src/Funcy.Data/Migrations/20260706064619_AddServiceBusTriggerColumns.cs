using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Funcy.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceBusTriggerColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConnectionSetting",
                table: "Functions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QueueName",
                table: "Functions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionName",
                table: "Functions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TopicName",
                table: "Functions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConnectionSetting",
                table: "Functions");

            migrationBuilder.DropColumn(
                name: "QueueName",
                table: "Functions");

            migrationBuilder.DropColumn(
                name: "SubscriptionName",
                table: "Functions");

            migrationBuilder.DropColumn(
                name: "TopicName",
                table: "Functions");
        }
    }
}
