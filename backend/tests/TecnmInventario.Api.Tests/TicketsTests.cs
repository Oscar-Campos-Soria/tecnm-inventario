using TecnmInventario.Api.Features.Assets.GetMyAssets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Documents;
using TecnmInventario.Api.Features.Tickets.AddTicketComment;
using TecnmInventario.Api.Features.Tickets.AssignTicket;
using TecnmInventario.Api.Features.Tickets.CreateTicket;
using TecnmInventario.Api.Features.Tickets.GetTicketById;
using TecnmInventario.Api.Features.Tickets.GetTickets;
using TecnmInventario.Api.Features.Tickets.ResolveTicket;
using TecnmInventario.Api.Features.Tickets.Shared;
using TecnmInventario.Api.Features.Tickets.UpdateTicketStatus;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Reglas de Tickets: visibilidad por dueño, notas internas, transiciones,
/// concurrencia, cierre automático y acceso a adjuntos. Proveedor en memoria
/// (transacciones ignoradas; no hay trigger touch_version, la versión no sube).
/// </summary>
public class TicketsTests
{
    private const long AdminId = 1;
    private const long TechnicianId = 2;
    private const long UserAId = 3;
    private const long UserBId = 4;
    private const long SystemId = 5;

    private static readonly Dictionary<string, string[]> RolePermissions = new()
    {
        ["ADMIN"] = new[] { "assets.view", "tickets.create", "tickets.manage", "incidents.create", "incidents.resolve", "incidents.view" },
        ["TECNICO"] = new[] { "assets.view", "tickets.create", "tickets.manage", "incidents.create", "incidents.resolve", "incidents.view" },
        // Rol hipotético que solo reporta incidentes (sin verlos ni resolverlos): prueba la regla del autorizador.
        ["SOLO_REPORTA"] = new[] { "tickets.create", "incidents.create" },
        // Igual que scripts/restrict-general-user.sql: sin assets.view ni incidents.create.
        ["USUARIO_GENERAL"] = new[] { "tickets.create" },
    };

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public long? UserId { get; set; }
        public string? Username => $"user{UserId}";
        public string? RoleCode { get; set; }
        public System.Net.IPAddress? IpAddress => null;
    }

    private sealed class FakeRolePermissions : IRolePermissionService
    {
        public Task<bool> HasPermissionAsync(string roleCode, string permissionCode, CancellationToken cancellationToken)
            => Task.FromResult(RolePermissions.TryGetValue(roleCode, out var p) && p.Contains(permissionCode));

        public void InvalidateCache(string roleCode) { }
    }

    private sealed class Harness
    {
        public TecnmInventarioDbContext Context { get; } = CreateContext();
        public FakeCurrentUser User { get; } = new();
        private readonly FakeRolePermissions _permissions = new();
        private readonly IOptions<TicketOptions> _options = Options.Create(new TicketOptions { AutoCloseDays = 3 });
        private IAuditLogService Audit => new AuditLogService(Context, User);

        public Harness As(long userId, string role)
        {
            User.UserId = userId;
            User.RoleCode = role;
            return this;
        }

        public Task<CreateTicketResponse> Create(string title = "No tengo internet", long? assetId = null)
            => new CreateTicketHandler(Context, User, Audit, _permissions)
                .Handle(new CreateTicketCommand(1, null, title, null, assetId, null), default).AsTask();

        public void AssignAsset(long assetId, long userId)
        {
            if (!Context.AssignmentTypes.Any())
                Context.AssignmentTypes.Add(new AssignmentType { AssignmentTypeId = 1, Code = "PERSONAL", Name = "Personal" });
            Context.AssetAssignments.Add(new AssetAssignment
            {
                AssetId = assetId, AssignedTo = userId, AssignmentTypeId = 1, AssignedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Version = 1,
            });
            Context.SaveChanges();
        }

        public Task<GetTicketByIdResponse> Get(long id)
            => new GetTicketByIdHandler(Context, User, _permissions, _options)
                .Handle(new GetTicketByIdQuery(id), default).AsTask();

        public Task<Common.Pagination.PagedResult<GetTicketsResponse>> List()
            => new GetTicketsHandler(Context, User, _permissions)
                .Handle(new GetTicketsQuery(1, 50, null, null, null, null, null, null, null), default).AsTask();

        public Task<AddTicketCommentResponse> Comment(long id, string body, bool isInternal = false)
            => new AddTicketCommentHandler(Context, User, Audit, _permissions)
                .Handle(new AddTicketCommentCommand(id, body, isInternal), default).AsTask();

        public Task<AssignTicketResponse> Assign(long id, int version, long technicianId)
            => new AssignTicketHandler(Context, User, Audit, _permissions)
                .Handle(new AssignTicketCommand(id, version, technicianId), default).AsTask();

        public Task<UpdateTicketStatusResponse> SetStatus(long id, int version, string status, string? reason = null)
            => new UpdateTicketStatusHandler(Context, User, Audit, _permissions)
                .Handle(new UpdateTicketStatusCommand(id, version, status, reason), default).AsTask();

        public Task<ResolveTicketResponse> Resolve(long id, int version)
            => new ResolveTicketHandler(Context, User, Audit, _options)
                .Handle(new ResolveTicketCommand(id, version, "Se reinició el router"), default).AsTask();

        public Task Authorize(string ownerType, long ownerId, DocumentOperation op, long? createdBy = null)
            => new DocumentAccessAuthorizer(_permissions, User, Context)
                .EnsureAuthorizedAsync(ownerType, ownerId, op, createdBy, default);

        public void ForceStatus(long id, string status, DateTime? resolvedAt = null)
        {
            var t = Context.Tickets.Single(x => x.TicketId == id);
            t.Status = status;
            t.ResolvedAt = resolvedAt;
            Context.SaveChanges();
            Context.ChangeTracker.Clear();
        }
    }

    private static TecnmInventarioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TecnmInventarioDbContext>()
            .UseInMemoryDatabase($"tecnm-tickets-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new TecnmInventarioDbContext(options);
        var now = DateTime.UtcNow;

        context.AddRange(
            new Role { RoleId = 1, Code = "ADMIN", Name = "Admin", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new Role { RoleId = 2, Code = "TECNICO", Name = "Técnico", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new Role { RoleId = 4, Code = "USUARIO_GENERAL", Name = "Usuario", CreatedAt = now, UpdatedAt = now, Version = 1 },
            User(AdminId, "admin", 1), User(TechnicianId, "tec", 2),
            User(UserAId, "ana", 4), User(UserBId, "beto", 4), User(SystemId, TicketAutoCloser.SystemUsername, 1),
            new TicketCategory { TicketCategoryId = 1, Code = "RED_INTERNET", Name = "Red", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentPriority { IncidentPriorityId = 1, Code = IncidentPriorityCodes.Baja, Name = "Baja", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentPriority { IncidentPriorityId = 2, Code = IncidentPriorityCodes.Media, Name = "Media", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentPriority { IncidentPriorityId = 4, Code = IncidentPriorityCodes.Critica, Name = "Crítica", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetStatus { AssetStatusId = 1, Code = AssetStatusCodes.Disponible, Name = "Disponible", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetType { AssetTypeId = 1, Code = "PC", Name = "PC", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new Asset { AssetId = 10, InventoryCode = "TEC-0010", AssetTypeId = 1, AssetStatusId = 1, CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentType { IncidentTypeId = 1, Code = "HARD", Name = "Hardware", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentStatus { IncidentStatusId = 1, Code = IncidentStatusCodes.Abierto, Name = "Abierto", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new Incident { IncidentId = 20, AssetId = 10, IncidentTypeId = 1, IncidentPriorityId = 1, IncidentStatusId = 1, ReportedBy = UserBId, Title = "No enciende", ReportedAt = now, CreatedAt = now, UpdatedAt = now, Version = 1 });
        context.SaveChanges();
        return context;

        AppUser User(long id, string username, long roleId) => new()
        {
            UserId = id, Username = username, Email = $"{username}@test.local", PasswordHash = "x",
            FirstName = username, LastName = "Test", RoleId = roleId, Status = "activo",
            CreatedAt = now, UpdatedAt = now, Version = 1
        };
    }

    [Fact]
    public async Task UsuarioGeneral_solo_liga_sus_equipos_y_el_tecnico_cualquiera()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() => h.Create(assetId: 10));
        Assert.Equal("Solo puedes reportar equipos que tienes asignados.", ex.Message);
        Assert.Empty(h.Context.Tickets);

        h.As(TechnicianId, "TECNICO");
        var created = await h.Create(assetId: 10);
        Assert.Equal(10, h.Context.Tickets.Single(t => t.TicketId == created.TicketId).AssetId);
    }

    [Fact]
    public async Task Mis_equipos_trae_solo_lo_asignado_al_usuario_con_sus_tickets()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");
        h.AssignAsset(10, UserAId);
        await h.Create("Mi PC no prende", assetId: 10);
        await h.Create("Sin equipo");

        var mine = await new GetMyAssetsHandler(h.Context, h.User).Handle(new GetMyAssetsQuery(), default);
        var other = await new GetMyAssetsHandler(h.Context, h.As(UserBId, "USUARIO_GENERAL").User).Handle(new GetMyAssetsQuery(), default);

        var asset = Assert.Single(mine);
        Assert.Equal("TEC-0010", asset.InventoryCode);
        Assert.Equal("Mi PC no prende", Assert.Single(asset.Tickets).Title);
        Assert.Empty(other);
    }

    [Fact]
    public async Task Create_DefaultsToMedia_AndDoesNotTouchAssetStatus()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");
        h.AssignAsset(10, UserAId);

        var created = await h.Create(assetId: 10);

        Assert.Equal(TicketStatusCodes.Abierto, created.Status);
        Assert.Equal(IncidentPriorityCodes.Media, created.PriorityCode);
        Assert.Equal(AssetStatusCodes.Disponible, h.Context.Assets.Include(a => a.AssetStatus).Single(a => a.AssetId == 10).AssetStatus.Code);
        Assert.Contains(h.Context.AuditLogs, a => a.TableName == "tickets" && a.Action == "INSERT" && a.RecordId == created.TicketId);
    }

    [Fact]
    public async Task Requester_OnlySeesOwnTickets_ForeignTicketIs404()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");
        var mine = await h.Create("De Ana");
        h.As(UserBId, "USUARIO_GENERAL");
        var other = await h.Create("De Beto");

        h.As(UserAId, "USUARIO_GENERAL");
        var list = await h.List();
        Assert.Single(list.Items);
        Assert.Equal(mine.TicketId, list.Items[0].TicketId);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Get(other.TicketId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Comment(other.TicketId, "hola"));

        h.As(TechnicianId, "TECNICO");
        Assert.Equal(2, (await h.List()).TotalCount);
    }

    [Fact]
    public async Task InternalNotes_HiddenFromRequester_AndOnlyStaffCanWriteThem()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");
        var t = await h.Create();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => h.Comment(t.TicketId, "nota", isInternal: true));

        h.As(TechnicianId, "TECNICO");
        await h.Comment(t.TicketId, "Pedir pieza a compras", isInternal: true);
        await h.Comment(t.TicketId, "Voy en camino");
        Assert.Equal(2, (await h.Get(t.TicketId)).Comments.Count);

        h.As(UserAId, "USUARIO_GENERAL");
        var seen = await h.Get(t.TicketId);
        Assert.Single(seen.Comments);
        Assert.DoesNotContain(seen.Comments, c => c.IsInternal);
        Assert.False(seen.CanManage);
    }

    [Fact]
    public async Task Flow_AssignWaitReplyResolveReopenClose()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");
        var t = await h.Create();

        h.As(TechnicianId, "TECNICO");
        var assigned = await h.Assign(t.TicketId, 1, TechnicianId);
        Assert.Equal(TicketStatusCodes.EnAtencion, assigned.Status);
        await h.SetStatus(t.TicketId, 1, TicketStatusCodes.EsperandoUsuario);

        // El solicitante contesta: vuelve solo a EN_ATENCION.
        h.As(UserAId, "USUARIO_GENERAL");
        var reply = await h.Comment(t.TicketId, "Ya lo reinicié y sigue igual");
        Assert.Equal(TicketStatusCodes.EnAtencion, reply.TicketStatus);

        h.As(TechnicianId, "TECNICO");
        var resolved = await h.Resolve(t.TicketId, 1);
        Assert.Equal(resolved.ResolvedAt.AddDays(3), resolved.AutoCloseAt);

        h.As(UserAId, "USUARIO_GENERAL");
        await h.SetStatus(t.TicketId, 1, TicketStatusCodes.EnAtencion, "Volvió a fallar");
        var reopened = await h.Get(t.TicketId);
        Assert.Equal(TicketStatusCodes.EnAtencion, reopened.Status);
        Assert.Null(reopened.ResolvedAt);
        Assert.Null(reopened.AutoCloseAt);
        Assert.Contains(reopened.Comments, c => c.Body == "Volvió a fallar");

        h.As(TechnicianId, "TECNICO");
        await h.Resolve(t.TicketId, 1);
        h.As(UserAId, "USUARIO_GENERAL");
        await h.SetStatus(t.TicketId, 1, TicketStatusCodes.Cerrado);
        Assert.NotNull((await h.Get(t.TicketId)).ClosedAt);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => h.Comment(t.TicketId, "¿sigue?"));
    }

    [Theory]
    [InlineData(TicketStatusCodes.Abierto, TicketStatusCodes.Cancelado, false, true)]
    [InlineData(TicketStatusCodes.Resuelto, TicketStatusCodes.Cerrado, false, true)]
    [InlineData(TicketStatusCodes.Resuelto, TicketStatusCodes.EnAtencion, false, true)]
    [InlineData(TicketStatusCodes.EnAtencion, TicketStatusCodes.EsperandoUsuario, false, false)]
    [InlineData(TicketStatusCodes.EsperandoUsuario, TicketStatusCodes.EnAtencion, false, false)]
    [InlineData(TicketStatusCodes.EnAtencion, TicketStatusCodes.EsperandoUsuario, true, true)]
    [InlineData(TicketStatusCodes.Abierto, TicketStatusCodes.EnAtencion, true, false)]
    [InlineData(TicketStatusCodes.Cerrado, TicketStatusCodes.EnAtencion, true, false)]
    [InlineData(TicketStatusCodes.Cancelado, TicketStatusCodes.Cerrado, true, false)]
    public void Transitions_RespectRole(string from, string to, bool isManager, bool expected)
        => Assert.Equal(expected, TicketStatusTransitions.IsAllowed(from, to, isManager));

    [Fact]
    public async Task Assign_WithStaleVersion_Conflicts()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");
        var t = await h.Create();
        h.As(TechnicianId, "TECNICO");

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => h.Assign(t.TicketId, 99, TechnicianId));
    }

    [Fact]
    public async Task Assign_ToNonStaff_IsRejected()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");
        var t = await h.Create();
        h.As(TechnicianId, "TECNICO");

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => h.Assign(t.TicketId, 1, UserBId));
    }

    [Fact]
    public async Task AutoClose_ClosesOnlyExpiredResolved_SignedBySystem()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");
        var old = await h.Create("Viejo");
        var recent = await h.Create("Reciente");
        var now = DateTime.UtcNow;
        h.ForceStatus(old.TicketId, TicketStatusCodes.Resuelto, now.AddDays(-4));
        h.ForceStatus(recent.TicketId, TicketStatusCodes.Resuelto, now.AddDays(-1));

        var closed = await TicketAutoCloser.CloseExpiredAsync(h.Context, 3, now, NullLogger.Instance, default);

        Assert.Equal(1, closed);
        Assert.Equal(TicketStatusCodes.Cerrado, h.Context.Tickets.Single(t => t.TicketId == old.TicketId).Status);
        Assert.Equal(TicketStatusCodes.Resuelto, h.Context.Tickets.Single(t => t.TicketId == recent.TicketId).Status);
        Assert.Contains(h.Context.TicketComments, c => c.TicketId == old.TicketId && c.AuthorId == SystemId);
        Assert.Contains(h.Context.AuditLogs, a => a.TableName == "tickets" && a.RecordId == old.TicketId
            && a.Action == "UPDATE" && a.PerformedBy == SystemId);
    }

    [Fact]
    public async Task TicketAttachments_OwnerOrStaffOnly()
    {
        var h = new Harness().As(UserAId, "USUARIO_GENERAL");
        var t = await h.Create();

        await h.Authorize(DocumentOwnerTypes.Ticket, t.TicketId, DocumentOperation.Upload);
        await h.Authorize(DocumentOwnerTypes.Ticket, t.TicketId, DocumentOperation.Read);
        // El dueño no borra lo que subió el técnico.
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => h.Authorize(DocumentOwnerTypes.Ticket, t.TicketId, DocumentOperation.Delete, createdBy: TechnicianId));

        h.As(UserBId, "USUARIO_GENERAL");
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => h.Authorize(DocumentOwnerTypes.Ticket, t.TicketId, DocumentOperation.Read));

        h.As(TechnicianId, "TECNICO");
        await h.Authorize(DocumentOwnerTypes.Ticket, t.TicketId, DocumentOperation.Delete, createdBy: UserAId);

        h.ForceStatus(t.TicketId, TicketStatusCodes.Cerrado);
        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => h.Authorize(DocumentOwnerTypes.Ticket, t.TicketId, DocumentOperation.Upload));
    }

    [Fact]
    public async Task IncidentEvidence_ReporterOnly_ForUsersWithoutStaffPermissions()
    {
        // Incidente 20 lo reportó Beto.
        var h = new Harness().As(UserAId, "SOLO_REPORTA");
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => h.Authorize(DocumentOwnerTypes.Incident, 20, DocumentOperation.Read));

        h.As(UserBId, "SOLO_REPORTA");
        await h.Authorize(DocumentOwnerTypes.Incident, 20, DocumentOperation.Read);

        // El usuario general ya no levanta incidentes (reporta por ticket): tampoco ve su evidencia.
        h.As(UserBId, "USUARIO_GENERAL");
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => h.Authorize(DocumentOwnerTypes.Incident, 20, DocumentOperation.Read));

        h.As(TechnicianId, "TECNICO");
        await h.Authorize(DocumentOwnerTypes.Incident, 20, DocumentOperation.Read);
    }
}
