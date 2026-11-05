using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dermatoglifia.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class SituacaoDiagnosticaAvaliacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "diagnostico_avaliado_em",
                table: "paciente",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_paciente_diagnostico_completo",
                table: "paciente",
                sql: "(\"situacao_diagnostica\" = 'SUSPEITA' AND \"fonte_diagnostico\" IS NULL AND \"diagnostico_avaliado_em\" IS NULL) OR (\"situacao_diagnostica\" <> 'SUSPEITA' AND \"fonte_diagnostico\" IS NOT NULL AND \"diagnostico_avaliado_em\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_paciente_diagnostico_completo",
                table: "paciente");

            migrationBuilder.DropColumn(
                name: "diagnostico_avaliado_em",
                table: "paciente");
        }
    }
}
