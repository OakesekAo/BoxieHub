using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoxieHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTagsToImportJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "ImportJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "ImportJobs");
        }
    }
}
