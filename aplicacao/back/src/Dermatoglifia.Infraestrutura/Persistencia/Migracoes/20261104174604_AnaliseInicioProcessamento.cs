using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dermatoglifia.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class AnaliseInicioProcessamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "processamento_iniciado_em",
                table: "analise",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "processamento_iniciado_em",
                table: "analise");
        }
    }
}
