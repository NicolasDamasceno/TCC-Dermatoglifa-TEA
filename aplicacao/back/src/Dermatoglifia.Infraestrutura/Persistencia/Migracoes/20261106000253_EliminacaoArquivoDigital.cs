using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dermatoglifia.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class EliminacaoArquivoDigital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_digital_arquivo_quando_coletavel",
                table: "digital");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "arquivo_eliminado_em",
                table: "digital",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_digital_arquivo_quando_coletavel",
                table: "digital",
                sql: "NOT \"coletavel\" OR \"arquivo_eliminado_em\" IS NOT NULL OR (\"caminho_arquivo\" IS NOT NULL AND \"hash_sha256\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_digital_arquivo_quando_coletavel",
                table: "digital");

            migrationBuilder.DropColumn(
                name: "arquivo_eliminado_em",
                table: "digital");

            migrationBuilder.AddCheckConstraint(
                name: "ck_digital_arquivo_quando_coletavel",
                table: "digital",
                sql: "NOT \"coletavel\" OR (\"caminho_arquivo\" IS NOT NULL AND \"hash_sha256\" IS NOT NULL)");
        }
    }
}
