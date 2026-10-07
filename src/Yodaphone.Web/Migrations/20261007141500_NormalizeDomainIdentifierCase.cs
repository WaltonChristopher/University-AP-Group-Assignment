using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yodaphone.Web.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeDomainIdentifierCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Repair installations that already applied the lowercase backfill.
            // Only the text representation changes; GUID values and FKs remain intact.
            migrationBuilder.Sql("UPDATE \"Conversations\" SET \"Id\" = upper(\"Id\");");
            migrationBuilder.Sql("UPDATE \"Messages\" SET \"Id\" = upper(\"Id\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Case normalization preserves values and needs no reversal. Lowercasing
            // on rollback would make GUID lookups fail again without reverting schema.
        }
    }
}
