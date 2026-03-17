using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FourPlayWebApp.Server.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "quartz");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    EnableEmails = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeagueUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NflScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    NflWeek = table.Column<int>(type: "integer", nullable: false),
                    HomeTeam = table.Column<string>(type: "text", nullable: false),
                    AwayTeam = table.Column<string>(type: "text", nullable: false),
                    HomeTeamScore = table.Column<int>(type: "integer", nullable: false),
                    AwayTeamScore = table.Column<int>(type: "integer", nullable: false),
                    GameTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NflScores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NflSpreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    NflWeek = table.Column<int>(type: "integer", nullable: false),
                    HomeTeam = table.Column<string>(type: "text", nullable: false),
                    AwayTeam = table.Column<string>(type: "text", nullable: false),
                    HomeTeamSpread = table.Column<double>(type: "double precision", nullable: false),
                    AwayTeamSpread = table.Column<double>(type: "double precision", nullable: false),
                    OverUnder = table.Column<double>(type: "double precision", nullable: false),
                    GameTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NflSpreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "qrtz_calendars",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    calendar_name = table.Column<string>(type: "text", nullable: false),
                    calendar = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_calendars", x => new { x.sched_name, x.calendar_name });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_fired_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    entry_id = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    instance_name = table.Column<string>(type: "text", nullable: false),
                    fired_time = table.Column<long>(type: "bigint", nullable: false),
                    sched_time = table.Column<long>(type: "bigint", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    job_name = table.Column<string>(type: "text", nullable: true),
                    job_group = table.Column<string>(type: "text", nullable: true),
                    is_nonconcurrent = table.Column<bool>(type: "bool", nullable: false),
                    requests_recovery = table.Column<bool>(type: "bool", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_fired_triggers", x => new { x.sched_name, x.entry_id });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_job_details",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    job_name = table.Column<string>(type: "text", nullable: false),
                    job_group = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    job_class_name = table.Column<string>(type: "text", nullable: false),
                    is_durable = table.Column<bool>(type: "bool", nullable: false),
                    is_nonconcurrent = table.Column<bool>(type: "bool", nullable: false),
                    is_update_data = table.Column<bool>(type: "bool", nullable: false),
                    requests_recovery = table.Column<bool>(type: "bool", nullable: false),
                    job_data = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_job_details", x => new { x.sched_name, x.job_name, x.job_group });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_locks",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    lock_name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_locks", x => new { x.sched_name, x.lock_name });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_paused_trigger_grps",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_paused_trigger_grps", x => new { x.sched_name, x.trigger_group });
                });

            migrationBuilder.CreateTable(
                name: "qrtz_scheduler_state",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    instance_name = table.Column<string>(type: "text", nullable: false),
                    last_checkin_time = table.Column<long>(type: "bigint", nullable: false),
                    checkin_interval = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_scheduler_state", x => new { x.sched_name, x.instance_name });
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvitationCode = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    InvitedByUserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegisteredUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Invitations_AspNetUsers_RegisteredUserId",
                        column: x => x.RegisteredUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LeagueInfo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueName = table.Column<string>(type: "text", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    OwnerUserId = table.Column<string>(type: "text", nullable: false),
                    LeagueType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueInfo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueInfo_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qrtz_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    job_name = table.Column<string>(type: "text", nullable: false),
                    job_group = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    next_fire_time = table.Column<long>(type: "bigint", nullable: true),
                    prev_fire_time = table.Column<long>(type: "bigint", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: true),
                    trigger_state = table.Column<string>(type: "text", nullable: false),
                    trigger_type = table.Column<string>(type: "text", nullable: false),
                    start_time = table.Column<long>(type: "bigint", nullable: false),
                    end_time = table.Column<long>(type: "bigint", nullable: true),
                    calendar_name = table.Column<string>(type: "text", nullable: true),
                    misfire_instr = table.Column<short>(type: "smallint", nullable: true),
                    job_data = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_triggers_qrtz_job_details_sched_name_job_name_job_group",
                        columns: x => new { x.sched_name, x.job_name, x.job_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_job_details",
                        principalColumns: new[] { "sched_name", "job_name", "job_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueJuiceMapping",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Juice = table.Column<int>(type: "integer", nullable: false),
                    JuiceDivisional = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    JuiceConference = table.Column<int>(type: "integer", nullable: false, defaultValue: 6),
                    WeeklyCost = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    DateCreated = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueJuiceMapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueJuiceMapping_LeagueInfo_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "LeagueInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeagueUserMapping",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LeagueInfoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeagueUserMapping", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeagueUserMapping_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueUserMapping_LeagueInfo_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "LeagueInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeagueUserMapping_LeagueInfo_LeagueInfoId",
                        column: x => x.LeagueInfoId,
                        principalTable: "LeagueInfo",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NflPicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Team = table.Column<string>(type: "text", nullable: false),
                    NflWeek = table.Column<int>(type: "integer", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LeagueInfoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NflPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NflPicks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NflPicks_LeagueInfo_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "LeagueInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NflPicks_LeagueInfo_LeagueInfoId",
                        column: x => x.LeagueInfoId,
                        principalTable: "LeagueInfo",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NflPostSeasonPicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeagueId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Team = table.Column<string>(type: "text", nullable: false),
                    Pick = table.Column<int>(type: "integer", nullable: false),
                    NflWeek = table.Column<int>(type: "integer", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LeagueInfoId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NflPostSeasonPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NflPostSeasonPicks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NflPostSeasonPicks_LeagueInfo_LeagueId",
                        column: x => x.LeagueId,
                        principalTable: "LeagueInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NflPostSeasonPicks_LeagueInfo_LeagueInfoId",
                        column: x => x.LeagueInfoId,
                        principalTable: "LeagueInfo",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "qrtz_blob_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    blob_data = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_blob_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_blob_triggers_qrtz_triggers_sched_name_trigger_name_tr~",
                        columns: x => new { x.sched_name, x.trigger_name, x.trigger_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_triggers",
                        principalColumns: new[] { "sched_name", "trigger_name", "trigger_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qrtz_cron_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    cron_expression = table.Column<string>(type: "text", nullable: false),
                    time_zone_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_cron_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_cron_triggers_qrtz_triggers_sched_name_trigger_name_tr~",
                        columns: x => new { x.sched_name, x.trigger_name, x.trigger_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_triggers",
                        principalColumns: new[] { "sched_name", "trigger_name", "trigger_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qrtz_simple_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    repeat_count = table.Column<long>(type: "bigint", nullable: false),
                    repeat_interval = table.Column<long>(type: "bigint", nullable: false),
                    times_triggered = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_simple_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_simple_triggers_qrtz_triggers_sched_name_trigger_name_~",
                        columns: x => new { x.sched_name, x.trigger_name, x.trigger_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_triggers",
                        principalColumns: new[] { "sched_name", "trigger_name", "trigger_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "qrtz_simprop_triggers",
                schema: "quartz",
                columns: table => new
                {
                    sched_name = table.Column<string>(type: "text", nullable: false),
                    trigger_name = table.Column<string>(type: "text", nullable: false),
                    trigger_group = table.Column<string>(type: "text", nullable: false),
                    str_prop_1 = table.Column<string>(type: "text", nullable: true),
                    str_prop_2 = table.Column<string>(type: "text", nullable: true),
                    str_prop_3 = table.Column<string>(type: "text", nullable: true),
                    int_prop_1 = table.Column<int>(type: "integer", nullable: true),
                    int_prop_2 = table.Column<int>(type: "integer", nullable: true),
                    long_prop_1 = table.Column<long>(type: "bigint", nullable: true),
                    long_prop_2 = table.Column<long>(type: "bigint", nullable: true),
                    dec_prop_1 = table.Column<decimal>(type: "numeric", nullable: true),
                    dec_prop_2 = table.Column<decimal>(type: "numeric", nullable: true),
                    bool_prop_1 = table.Column<bool>(type: "bool", nullable: true),
                    bool_prop_2 = table.Column<bool>(type: "bool", nullable: true),
                    time_zone_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_qrtz_simprop_triggers", x => new { x.sched_name, x.trigger_name, x.trigger_group });
                    table.ForeignKey(
                        name: "FK_qrtz_simprop_triggers_qrtz_triggers_sched_name_trigger_name~",
                        columns: x => new { x.sched_name, x.trigger_name, x.trigger_group },
                        principalSchema: "quartz",
                        principalTable: "qrtz_triggers",
                        principalColumns: new[] { "sched_name", "trigger_name", "trigger_group" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email",
                table: "Invitations",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InvitedByUserId",
                table: "Invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_RegisteredUserId",
                table: "Invitations",
                column: "RegisteredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInfo_LeagueName",
                table: "LeagueInfo",
                column: "LeagueName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueInfo_OwnerUserId",
                table: "LeagueInfo",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueJuiceMapping_LeagueId",
                table: "LeagueJuiceMapping",
                column: "LeagueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueJuiceMapping_LeagueId_Season",
                table: "LeagueJuiceMapping",
                columns: new[] { "LeagueId", "Season" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueUserMapping_LeagueId_UserId",
                table: "LeagueUserMapping",
                columns: new[] { "LeagueId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeagueUserMapping_LeagueInfoId",
                table: "LeagueUserMapping",
                column: "LeagueInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueUserMapping_UserId",
                table: "LeagueUserMapping",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeagueUsers_Email",
                table: "LeagueUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NflPicks_LeagueId",
                table: "NflPicks",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_NflPicks_LeagueInfoId",
                table: "NflPicks",
                column: "LeagueInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_NflPicks_UserId_LeagueId_NflWeek_Season_Team",
                table: "NflPicks",
                columns: new[] { "UserId", "LeagueId", "NflWeek", "Season", "Team" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NflPostSeasonPicks_LeagueId",
                table: "NflPostSeasonPicks",
                column: "LeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_NflPostSeasonPicks_LeagueInfoId",
                table: "NflPostSeasonPicks",
                column: "LeagueInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_NflPostSeasonPicks_UserId_LeagueId_NflWeek_Season_Team_Pick",
                table: "NflPostSeasonPicks",
                columns: new[] { "UserId", "LeagueId", "NflWeek", "Season", "Team", "Pick" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NflScores_Season_NflWeek_HomeTeam",
                table: "NflScores",
                columns: new[] { "Season", "NflWeek", "HomeTeam" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NflSpreads_Season_NflWeek_HomeTeam",
                table: "NflSpreads",
                columns: new[] { "Season", "NflWeek", "HomeTeam" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_job_group",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "job_group");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_job_name",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "job_name");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_job_req_recovery",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "requests_recovery");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_trig_group",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "trigger_group");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_trig_inst_name",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "instance_name");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_trig_name",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                column: "trigger_name");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_ft_trig_nm_gp",
                schema: "quartz",
                table: "qrtz_fired_triggers",
                columns: new[] { "sched_name", "trigger_name", "trigger_group" });

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_j_req_recovery",
                schema: "quartz",
                table: "qrtz_job_details",
                column: "requests_recovery");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_t_next_fire_time",
                schema: "quartz",
                table: "qrtz_triggers",
                column: "next_fire_time");

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_t_nft_st",
                schema: "quartz",
                table: "qrtz_triggers",
                columns: new[] { "next_fire_time", "trigger_state" });

            migrationBuilder.CreateIndex(
                name: "idx_qrtz_t_state",
                schema: "quartz",
                table: "qrtz_triggers",
                column: "trigger_state");

            migrationBuilder.CreateIndex(
                name: "IX_qrtz_triggers_sched_name_job_name_job_group",
                schema: "quartz",
                table: "qrtz_triggers",
                columns: new[] { "sched_name", "job_name", "job_group" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "LeagueJuiceMapping");

            migrationBuilder.DropTable(
                name: "LeagueUserMapping");

            migrationBuilder.DropTable(
                name: "LeagueUsers");

            migrationBuilder.DropTable(
                name: "NflPicks");

            migrationBuilder.DropTable(
                name: "NflPostSeasonPicks");

            migrationBuilder.DropTable(
                name: "NflScores");

            migrationBuilder.DropTable(
                name: "NflSpreads");

            migrationBuilder.DropTable(
                name: "qrtz_blob_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_calendars",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_cron_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_fired_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_locks",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_paused_trigger_grps",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_scheduler_state",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_simple_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "qrtz_simprop_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "LeagueInfo");

            migrationBuilder.DropTable(
                name: "qrtz_triggers",
                schema: "quartz");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "qrtz_job_details",
                schema: "quartz");
        }
    }
}
