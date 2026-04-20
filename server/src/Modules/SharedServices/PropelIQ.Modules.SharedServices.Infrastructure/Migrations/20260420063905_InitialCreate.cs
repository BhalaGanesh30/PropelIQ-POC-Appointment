using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropelIQ.Modules.SharedServices.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            // pgvector is optional — gracefully skip when not installed (local dev without Docker).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    CREATE EXTENSION IF NOT EXISTS vector;
                EXCEPTION WHEN OTHERS THEN
                    RAISE NOTICE 'pgvector not available on this instance. Vector search will be disabled.';
                END $$;
                """);

            // Create embedding_samples only when pgvector is installed.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    CREATE TABLE IF NOT EXISTS app.embedding_samples (
                        "Id"         uuid                     NOT NULL DEFAULT uuid_generate_v4(),
                        "ContentRef" character varying(512)   NOT NULL,
                        "Embedding"  vector(1536),
                        "CreatedAt"  timestamp with time zone NOT NULL DEFAULT now(),
                        CONSTRAINT "PK_embedding_samples" PRIMARY KEY ("Id")
                    );
                    CREATE INDEX IF NOT EXISTS "IX_embedding_samples_Embedding"
                        ON app.embedding_samples USING ivfflat ("Embedding" vector_cosine_ops)
                        WITH (lists = 100);
                EXCEPTION WHEN OTHERS THEN
                    RAISE NOTICE 'Skipping embedding_samples table — pgvector not available.';
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "embedding_samples",
                schema: "app");
        }
    }
}
