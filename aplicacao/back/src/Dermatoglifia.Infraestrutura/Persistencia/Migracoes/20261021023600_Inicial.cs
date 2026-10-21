using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dermatoglifia.Infraestrutura.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identificacao");

            migrationBuilder.CreateTable(
                name: "modelo_ia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    arquitetura = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    origem_dados = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    escore_validado = table.Column<bool>(type: "boolean", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    metricas_teste = table.Column<string>(type: "jsonb", nullable: false),
                    treinado_em = table.Column<DateOnly>(type: "date", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    promovido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modelo_ia", x => x.id);
                    table.CheckConstraint("ck_modelo_ia_origem_dados", "\"origem_dados\" IN ('SINTETICO', 'REAL')");
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    papel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_acesso_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email_normalizado = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    senha_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id);
                    table.CheckConstraint("ck_usuario_papel", "\"papel\" IN ('MEDICO', 'ADMIN')");
                    table.CheckConstraint("ck_usuario_status", "\"status\" IN ('PENDENTE', 'ATIVO', 'RECUSADO', 'INATIVO')");
                });

            migrationBuilder.CreateTable(
                name: "log_auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    detalhes = table.Column<string>(type: "jsonb", nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_log_auditoria", x => x.id);
                    table.ForeignKey(
                        name: "fk_log_auditoria_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "medico",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crm = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    uf_crm = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    especialidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    vinculo_protocolo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    aprovado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    aprovado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_recusa = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medico", x => x.usuario_id);
                    table.ForeignKey(
                        name: "fk_medico_usuario_aprovado_por",
                        column: x => x.aprovado_por,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medico_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "usuario_claim",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_claim", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuario_claim_usuario_user_id",
                        column: x => x.user_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_login",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_login", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_usuario_login_usuario_user_id",
                        column: x => x.user_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_token",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_token", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_usuario_token_usuario_user_id",
                        column: x => x.user_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "paciente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    medico_responsavel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sexo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    situacao_diagnostica = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fonte_diagnostico = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    situacao_atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    anonimizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_paciente", x => x.id);
                    table.CheckConstraint("ck_paciente_sexo", "\"sexo\" IN ('MASCULINO', 'FEMININO')");
                    table.CheckConstraint("ck_paciente_situacao_diagnostica", "\"situacao_diagnostica\" IN ('SUSPEITA', 'TEA_CONFIRMADO', 'TEA_DESCARTADO', 'CONTROLE')");
                    table.ForeignKey(
                        name: "fk_paciente_medico_medico_responsavel_id",
                        column: x => x.medico_responsavel_id,
                        principalTable: "medico",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consentimento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    versao_termo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    autoriza_analise = table.Column<bool>(type: "boolean", nullable: false),
                    ciente_uso_ia = table.Column<bool>(type: "boolean", nullable: false),
                    autoriza_uso_treino = table.Column<bool>(type: "boolean", nullable: false),
                    assinado_em = table.Column<DateOnly>(type: "date", nullable: false),
                    registrado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consentimento", x => x.id);
                    table.CheckConstraint("ck_consentimento_tipo", "\"tipo\" IN ('TCLE', 'TALE')");
                    table.ForeignKey(
                        name: "fk_consentimento_paciente_paciente_id",
                        column: x => x.paciente_id,
                        principalTable: "paciente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consentimento_usuario_registrado_por",
                        column: x => x.registrado_por,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "identificacao_paciente",
                schema: "identificacao",
                columns: table => new
                {
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    responsavel_nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    responsavel_parentesco = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    responsavel_contato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identificacao_paciente", x => x.paciente_id);
                    table.ForeignKey(
                        name: "fk_identificacao_paciente_paciente_paciente_id",
                        column: x => x.paciente_id,
                        principalTable: "paciente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sessao_coleta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    paciente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medico_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metodo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo_impressao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    dispositivo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    idade_meses = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concluida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sessao_coleta", x => x.id);
                    table.CheckConstraint("ck_sessao_coleta_idade_meses", "\"idade_meses\" >= 0");
                    table.CheckConstraint("ck_sessao_coleta_metodo", "\"metodo\" IN ('LEITOR_OPTICO', 'TINTA_DIGITALIZADA')");
                    table.CheckConstraint("ck_sessao_coleta_status", "\"status\" IN ('EM_ANDAMENTO', 'COMPLETA', 'INCOMPLETA', 'CANCELADA')");
                    table.CheckConstraint("ck_sessao_coleta_tipo_impressao", "\"tipo_impressao\" IN ('ROLADA', 'PLANA')");
                    table.ForeignKey(
                        name: "fk_sessao_coleta_medico_medico_id",
                        column: x => x.medico_id,
                        principalTable: "medico",
                        principalColumn: "usuario_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sessao_coleta_paciente_paciente_id",
                        column: x => x.paciente_id,
                        principalTable: "paciente",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "analise",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_coleta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    modelo_ia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    escore_risco = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    versao_aviso = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao_medico = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    registrado_prontuario_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    mensagem_erro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    solicitada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concluida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    discordancia_bilateral = table.Column<int>(type: "integer", nullable: true),
                    discordancia_bilateral_com_polegar = table.Column<int>(type: "integer", nullable: true),
                    qtd_alcas_radiais = table.Column<int>(type: "integer", nullable: true),
                    qtd_alcas_ulnares = table.Column<int>(type: "integer", nullable: true),
                    qtd_arcos = table.Column<int>(type: "integer", nullable: true),
                    qtd_arcos_tendidos = table.Column<int>(type: "integer", nullable: true),
                    qtd_verticilos = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analise", x => x.id);
                    table.CheckConstraint("ck_analise_discordancia", "\"discordancia_bilateral\" IS NULL OR (\"discordancia_bilateral\" BETWEEN 0 AND 4 AND \"discordancia_bilateral_com_polegar\" BETWEEN 0 AND 5)");
                    table.CheckConstraint("ck_analise_escore_risco", "\"escore_risco\" IS NULL OR \"escore_risco\" BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_analise_status", "\"status\" IN ('PENDENTE', 'PROCESSANDO', 'CONCLUIDA', 'ERRO')");
                    table.ForeignKey(
                        name: "fk_analise_modelo_ia_modelo_ia_id",
                        column: x => x.modelo_ia_id,
                        principalTable: "modelo_ia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_analise_sessao_coleta_sessao_coleta_id",
                        column: x => x.sessao_coleta_id,
                        principalTable: "sessao_coleta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "digital",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sessao_coleta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    dedo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    coletavel = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_nao_coletavel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    caminho_arquivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    hash_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    formato = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    resolucao_dpi = table.Column<int>(type: "integer", nullable: true),
                    largura_px = table.Column<int>(type: "integer", nullable: true),
                    altura_px = table.Column<int>(type: "integer", nullable: true),
                    nfiq2 = table.Column<int>(type: "integer", nullable: true),
                    capturada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_digital", x => x.id);
                    table.CheckConstraint("ck_digital_arquivo_quando_coletavel", "NOT \"coletavel\" OR (\"caminho_arquivo\" IS NOT NULL AND \"hash_sha256\" IS NOT NULL)");
                    table.CheckConstraint("ck_digital_dedo", "\"dedo\" IN ('POLEGAR', 'INDICADOR', 'MEDIO', 'ANELAR', 'MINIMO')");
                    table.CheckConstraint("ck_digital_mao", "\"mao\" IN ('DIREITA', 'ESQUERDA')");
                    table.CheckConstraint("ck_digital_motivo_nao_coletavel", "\"coletavel\" OR \"motivo_nao_coletavel\" IS NOT NULL");
                    table.CheckConstraint("ck_digital_nfiq2", "\"nfiq2\" IS NULL OR \"nfiq2\" BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_digital_resolucao_minima", "\"resolucao_dpi\" IS NULL OR \"resolucao_dpi\" >= 500");
                    table.ForeignKey(
                        name: "fk_digital_sessao_coleta_sessao_coleta_id",
                        column: x => x.sessao_coleta_id,
                        principalTable: "sessao_coleta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "resultado_digital",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    digital_id = table.Column<Guid>(type: "uuid", nullable: false),
                    classe_imagem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    padrao_previsto = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    confianca = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    probabilidades = table.Column<string>(type: "jsonb", nullable: false),
                    alerta_qualidade = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resultado_digital", x => x.id);
                    table.CheckConstraint("ck_resultado_digital_classe_imagem", "\"classe_imagem\" IN ('ARCO', 'ARCO_TENDIDO', 'ALCA_ESQUERDA', 'ALCA_DIREITA', 'VERTICILO')");
                    table.CheckConstraint("ck_resultado_digital_confianca", "\"confianca\" BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_resultado_digital_padrao_previsto", "\"padrao_previsto\" IN ('ARCO', 'ARCO_TENDIDO', 'ALCA_ULNAR', 'ALCA_RADIAL', 'VERTICILO')");
                    table.ForeignKey(
                        name: "fk_resultado_digital_analise_analise_id",
                        column: x => x.analise_id,
                        principalTable: "analise",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_resultado_digital_digital_digital_id",
                        column: x => x.digital_id,
                        principalTable: "digital",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_analise_modelo_ia_id",
                table: "analise",
                column: "modelo_ia_id");

            migrationBuilder.CreateIndex(
                name: "ix_analise_sessao_coleta_id",
                table: "analise",
                column: "sessao_coleta_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_consentimento_paciente_id",
                table: "consentimento",
                column: "paciente_id");

            migrationBuilder.CreateIndex(
                name: "ix_consentimento_registrado_por",
                table: "consentimento",
                column: "registrado_por");

            migrationBuilder.CreateIndex(
                name: "ix_digital_sessao_coleta_id_mao_dedo",
                table: "digital",
                columns: new[] { "sessao_coleta_id", "mao", "dedo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_log_auditoria_entidade_entidade_id",
                table: "log_auditoria",
                columns: new[] { "entidade", "entidade_id" });

            migrationBuilder.CreateIndex(
                name: "ix_log_auditoria_ocorrido_em",
                table: "log_auditoria",
                column: "ocorrido_em");

            migrationBuilder.CreateIndex(
                name: "ix_log_auditoria_usuario_id",
                table: "log_auditoria",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_medico_aprovado_por",
                table: "medico",
                column: "aprovado_por");

            migrationBuilder.CreateIndex(
                name: "ix_medico_crm_uf_crm",
                table: "medico",
                columns: new[] { "crm", "uf_crm" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modelo_ia_versao",
                table: "modelo_ia",
                column: "versao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_modelo_ia_um_ativo",
                table: "modelo_ia",
                column: "ativo",
                unique: true,
                filter: "\"ativo\"");

            migrationBuilder.CreateIndex(
                name: "ix_paciente_medico_responsavel_id",
                table: "paciente",
                column: "medico_responsavel_id");

            migrationBuilder.CreateIndex(
                name: "ix_resultado_digital_analise_id",
                table: "resultado_digital",
                column: "analise_id");

            migrationBuilder.CreateIndex(
                name: "ix_resultado_digital_digital_id",
                table: "resultado_digital",
                column: "digital_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sessao_coleta_medico_id",
                table: "sessao_coleta",
                column: "medico_id");

            migrationBuilder.CreateIndex(
                name: "ux_sessao_coleta_em_andamento_por_paciente",
                table: "sessao_coleta",
                column: "paciente_id",
                unique: true,
                filter: "\"status\" = 'EM_ANDAMENTO'");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_email_normalizado",
                table: "usuario",
                column: "email_normalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_normalized_user_name",
                table: "usuario",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_claim_user_id",
                table: "usuario_claim",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_login_user_id",
                table: "usuario_login",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consentimento");

            migrationBuilder.DropTable(
                name: "identificacao_paciente",
                schema: "identificacao");

            migrationBuilder.DropTable(
                name: "log_auditoria");

            migrationBuilder.DropTable(
                name: "resultado_digital");

            migrationBuilder.DropTable(
                name: "usuario_claim");

            migrationBuilder.DropTable(
                name: "usuario_login");

            migrationBuilder.DropTable(
                name: "usuario_token");

            migrationBuilder.DropTable(
                name: "analise");

            migrationBuilder.DropTable(
                name: "digital");

            migrationBuilder.DropTable(
                name: "modelo_ia");

            migrationBuilder.DropTable(
                name: "sessao_coleta");

            migrationBuilder.DropTable(
                name: "paciente");

            migrationBuilder.DropTable(
                name: "medico");

            migrationBuilder.DropTable(
                name: "usuario");
        }
    }
}
