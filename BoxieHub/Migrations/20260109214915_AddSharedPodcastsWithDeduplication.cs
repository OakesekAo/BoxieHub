using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BoxieHub.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedPodcastsWithDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedPodcasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FeedUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Author = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TotalEpisodes = table.Column<int>(type: "integer", nullable: false),
                    LastFetched = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Modified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedPodcasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PodcastEpisodeCache",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SavedPodcastId = table.Column<int>(type: "integer", nullable: false),
                    EpisodeGuid = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AudioUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    DurationSeconds = table.Column<float>(type: "real", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    PublishDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileUploadId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportCount = table.Column<int>(type: "integer", nullable: false),
                    CachedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PodcastEpisodeCache", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PodcastEpisodeCache_FileUploads_FileUploadId",
                        column: x => x.FileUploadId,
                        principalTable: "FileUploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PodcastEpisodeCache_SavedPodcasts_SavedPodcastId",
                        column: x => x.SavedPodcastId,
                        principalTable: "SavedPodcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PodcastSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    SavedPodcastId = table.Column<int>(type: "integer", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LastAccessed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubscribedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PodcastSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PodcastSubscriptions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PodcastSubscriptions_SavedPodcasts_SavedPodcastId",
                        column: x => x.SavedPodcastId,
                        principalTable: "SavedPodcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PodcastEpisodeCache_AudioUrl",
                table: "PodcastEpisodeCache",
                column: "AudioUrl");

            migrationBuilder.CreateIndex(
                name: "IX_PodcastEpisodeCache_FileUploadId",
                table: "PodcastEpisodeCache",
                column: "FileUploadId");

            migrationBuilder.CreateIndex(
                name: "IX_PodcastEpisodeCache_PodcastId",
                table: "PodcastEpisodeCache",
                column: "SavedPodcastId");

            migrationBuilder.CreateIndex(
                name: "IX_PodcastEpisodeCache_PodcastId_Guid",
                table: "PodcastEpisodeCache",
                columns: new[] { "SavedPodcastId", "EpisodeGuid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PodcastSubscriptions_IsFavorite",
                table: "PodcastSubscriptions",
                column: "IsFavorite");

            migrationBuilder.CreateIndex(
                name: "IX_PodcastSubscriptions_LastAccessed",
                table: "PodcastSubscriptions",
                column: "LastAccessed");

            migrationBuilder.CreateIndex(
                name: "IX_PodcastSubscriptions_SavedPodcastId",
                table: "PodcastSubscriptions",
                column: "SavedPodcastId");

            migrationBuilder.CreateIndex(
                name: "IX_PodcastSubscriptions_UserId",
                table: "PodcastSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PodcastSubscriptions_UserId_PodcastId",
                table: "PodcastSubscriptions",
                columns: new[] { "UserId", "SavedPodcastId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedPodcasts_FeedUrl",
                table: "SavedPodcasts",
                column: "FeedUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedPodcasts_LastFetched",
                table: "SavedPodcasts",
                column: "LastFetched");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PodcastEpisodeCache");

            migrationBuilder.DropTable(
                name: "PodcastSubscriptions");

            migrationBuilder.DropTable(
                name: "SavedPodcasts");
        }
    }
}
