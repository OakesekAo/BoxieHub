using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxieHub.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaLibraryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileUploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    DurationSeconds = table.Column<float>(type: "real", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaLibraryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaLibraryItems_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaLibraryItems_FileUploads_FileUploadId",
                        column: x => x.FileUploadId,
                        principalTable: "FileUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaLibraryUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MediaLibraryItemId = table.Column<int>(type: "integer", nullable: false),
                    HouseholdId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TonieId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TonieName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ChapterId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChapterTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaLibraryUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaLibraryUsages_MediaLibraryItems_MediaLibraryItemId",
                        column: x => x.MediaLibraryItemId,
                        principalTable: "MediaLibraryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaLibraryItems_FileUploadId",
                table: "MediaLibraryItems",
                column: "FileUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLibraryItems_UserId",
                table: "MediaLibraryItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaLibraryUsages_MediaLibraryItemId",
                table: "MediaLibraryUsages",
                column: "MediaLibraryItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaLibraryUsages");

            migrationBuilder.DropTable(
                name: "MediaLibraryItems");
        }
    }
}
