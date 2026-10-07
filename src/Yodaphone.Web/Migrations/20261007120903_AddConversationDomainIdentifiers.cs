using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yodaphone.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationDomainIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "Messages",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "Conversations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing installations can contain multiple rows. Give every legacy
            // row a distinct public GUID before the unique indexes are added.
            // Microsoft.Data.Sqlite binds GUID parameters as uppercase TEXT.
            migrationBuilder.Sql(
                "UPDATE \"Messages\" SET \"Id\" = upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)));");
            migrationBuilder.Sql(
                "UPDATE \"Conversations\" SET \"Id\" = upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6)));");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_Id",
                table: "Messages",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Id",
                table: "Conversations",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_Id",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_Id",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Conversations");
        }
    }
}
