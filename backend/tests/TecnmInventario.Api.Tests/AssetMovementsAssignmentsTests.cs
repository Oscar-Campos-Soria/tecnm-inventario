using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Assets.Assignments.CreateAssetAssignment;
using TecnmInventario.Api.Features.Assets.Assignments.EndAssetAssignment;
using TecnmInventario.Api.Features.Assets.Movements.CreateAssetMovement;
using TecnmInventario.Api.Features.Assets.Shared;

namespace TecnmInventario.Api.Tests;

/// <summary>
/// Reglas de negocio de movimientos y asignaciones (máquina de estados,
/// transacción + audit_log). Usa el proveedor en memoria: las transacciones
/// explícitas se ignoran (TransactionIgnoredWarning suprimido), pero las
/// reglas, los saves y las filas de audit_log se ejecutan de verdad.
/// </summary>
public class AssetMovementsAssignmentsTests
{
    private sealed class Harness
    {
        public TecnmInventarioDbContext Context { get; }
        private readonly FakeCurrentUser _currentUser = new();
        private readonly IAuditLogService _auditLog;
        private readonly IAssetStatusChanger _statusChanger;

        public CreateAssetMovementHandler Movement { get; }
        public CreateAssetAssignmentHandler Assign { get; }
        public EndAssetAssignmentHandler Return { get; }

        public Harness()
        {
            Context = CreateContext();
            _auditLog = new AuditLogService(Context, _currentUser);
            _statusChanger = new AssetStatusChanger(Context, _currentUser, _auditLog);
            Movement = new CreateAssetMovementHandler(Context, _currentUser, _auditLog);
            Assign = new CreateAssetAssignmentHandler(Context, _currentUser, _auditLog, _statusChanger);
            Return = new EndAssetAssignmentHandler(Context, _currentUser, _auditLog, _statusChanger);
        }
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public long? UserId => 1;
        public string? Username => "admin";
        public string? RoleCode => "ADMIN";
        public System.Net.IPAddress? IpAddress => null;
    }

    private const long LocationA = 1;
    private const long LocationB = 2;
    private const long UserId = 1;
    private const long TechnicianId = 2;

    private static TecnmInventarioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TecnmInventarioDbContext>()
            .UseInMemoryDatabase($"tecnm-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var context = new TecnmInventarioDbContext(options);
        Seed(context);
        context.SaveChanges();
        return context;
    }

    private static void Seed(TecnmInventarioDbContext context)
    {
        var now = DateTime.UtcNow;
        context.AddRange(
            new AssetStatus { AssetStatusId = 1, Code = AssetStatusCodes.Disponible, Name = "Disponible", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetStatus { AssetStatusId = 2, Code = AssetStatusCodes.Asignado, Name = "Asignado", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetStatus { AssetStatusId = 3, Code = AssetStatusCodes.EnMantenimiento, Name = "En mantenimiento", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetStatus { AssetStatusId = 4, Code = AssetStatusCodes.ConFalla, Name = "Con falla", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetStatus { AssetStatusId = 5, Code = AssetStatusCodes.DadoDeBaja, Name = "Dado de baja", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssetType { AssetTypeId = 1, Code = "PC", Name = "Equipo de cómputo", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new Location { LocationId = LocationA, Code = "ALM", Name = "Almacén", LocationTypeId = 1, CreatedAt = now, UpdatedAt = now, Version = 1 },
            new Location { LocationId = LocationB, Code = "LAB", Name = "Laboratorio", LocationTypeId = 1, CreatedAt = now, UpdatedAt = now, Version = 1 },
            new MovementReason { MovementReasonId = 1, Code = "REUB", Name = "Reubicación", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AssignmentType { AssignmentTypeId = 1, Code = "RES", Name = "Resguardo", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AppUser { UserId = UserId, Username = "admin", Email = "admin@test.local", PasswordHash = "x", FirstName = "Admin", LastName = "Sis", RoleId = 1, Status = "activo", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new AppUser { UserId = TechnicianId, Username = "tec", Email = "tec@test.local", PasswordHash = "x", FirstName = "Técnico", LastName = "Uno", RoleId = 2, Status = "activo", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentType { IncidentTypeId = 1, Code = "HARD", Name = "Hardware", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentPriority { IncidentPriorityId = 1, Code = "ALTA", Name = "Alta", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentStatus { IncidentStatusId = 1, Code = IncidentStatusCodes.Abierto, Name = "Abierto", CreatedAt = now, UpdatedAt = now, Version = 1 },
            new IncidentStatus { IncidentStatusId = 2, Code = IncidentStatusCodes.Resuelto, Name = "Resuelto", CreatedAt = now, UpdatedAt = now, Version = 1 });
    }

    private static Asset AddAsset(Harness h, string statusCode)
    {
        var status = h.Context.AssetStatuses.Single(s => s.Code == statusCode);
        var asset = new Asset
        {
            InventoryCode = $"TEC-TEST-{Guid.NewGuid():N}"[..18],
            AssetTypeId = 1,
            AssetStatusId = status.AssetStatusId,
            CurrentLocationId = LocationA,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };
        h.Context.Assets.Add(asset);
        h.Context.SaveChanges();
        return asset;
    }

    private static AssetAssignment AddAssignment(Harness h, long assetId)
    {
        var assignment = new AssetAssignment
        {
            AssetId = assetId,
            AssignedTo = TechnicianId,
            AssignmentTypeId = 1,
            LocationId = LocationA,
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };
        h.Context.AssetAssignments.Add(assignment);
        h.Context.SaveChanges();
        return assignment;
    }

    private static void AddIncident(Harness h, long assetId, string statusCode)
    {
        var status = h.Context.IncidentStatuses.Single(s => s.Code == statusCode);
        h.Context.Incidents.Add(new Incident
        {
            AssetId = assetId,
            IncidentTypeId = 1,
            IncidentPriorityId = 1,
            IncidentStatusId = status.IncidentStatusId,
            ReportedBy = UserId,
            Title = "Falla de prueba",
            ReportedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        });
        h.Context.SaveChanges();
    }

    private static Asset StatusOf(Harness h, long assetId)
        => h.Context.Assets
            .AsNoTracking()
            .Include(a => a.AssetStatus)
            .Single(a => a.AssetId == assetId);

    // ---------- Movimientos ----------

    [Fact]
    public async Task Mover_EnMantenimiento_Rechazado()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.EnMantenimiento);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, LocationB, 1, null, null), default).AsTask());

        Assert.Contains(AssetStatusCodes.EnMantenimiento, ex.Message);
        Assert.Empty(h.Context.AssetMovements);
    }

    [Fact]
    public async Task Mover_ConFalla_Rechazado()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.ConFalla);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, LocationB, 1, null, null), default).AsTask());

        Assert.Contains(AssetStatusCodes.ConFalla, ex.Message);
        Assert.Empty(h.Context.AssetMovements);
    }

    [Fact]
    public async Task Mover_DadoDeBaja_Rechazado()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.DadoDeBaja);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, LocationB, 1, null, null), default).AsTask());

        Assert.Contains(AssetStatusCodes.DadoDeBaja, ex.Message);
        Assert.Empty(h.Context.AssetMovements);
    }

    [Fact]
    public async Task Mover_Disponible_Permitido_RegistraMovimientoYAuditoria()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.Disponible);

        var result = await h.Movement.Handle(
            new CreateAssetMovementCommand(asset.AssetId, LocationB, 1, null, null), default);

        Assert.Equal(LocationA, result.FromLocationId);
        Assert.Equal(LocationB, result.ToLocationId);

        var movement = h.Context.AssetMovements.Single();
        Assert.Equal(asset.AssetId, movement.AssetId);
        Assert.Equal(LocationA, movement.FromLocationId);
        Assert.Equal(LocationB, movement.ToLocationId);

        var fresh = StatusOf(h, asset.AssetId);
        Assert.Equal(LocationB, fresh.CurrentLocationId);
        Assert.Equal(AssetStatusCodes.Disponible, fresh.AssetStatus.Code);

        var audits = h.Context.AuditLogs.ToList();
        Assert.Contains(audits, a => a.TableName == "asset_movements" && a.Action == "INSERT");
        Assert.Contains(audits, a => a.TableName == "assets" && a.Action == "UPDATE");
    }

    [Fact]
    public async Task Mover_Asignado_Permitido()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.Asignado);

        await h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, LocationB, 1, null, null), default);

        Assert.Equal(LocationB, StatusOf(h, asset.AssetId).CurrentLocationId);
    }

    [Fact]
    public async Task Mover_MismoDestino_Rechazado()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.Disponible);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Movement.Handle(new CreateAssetMovementCommand(asset.AssetId, LocationA, 1, null, null), default).AsTask());
    }

    // ---------- Asignaciones ----------

    [Fact]
    public async Task Asignar_NoDisponible_Rechazado()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.EnMantenimiento);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Assign.Handle(new CreateAssetAssignmentCommand(asset.AssetId, TechnicianId, 1, LocationA, null, null), default).AsTask());

        Assert.Contains(AssetStatusCodes.Disponible, ex.Message);
        Assert.Empty(h.Context.AssetAssignments);
    }

    [Fact(Skip = "Requiere transacciones reales de PostgreSQL; cubierto por prueba manual en bloque 1. Pendiente: Testcontainers.PostgreSql en Fase 4")]
    public async Task Asignar_DejaAsignado_RegistraAuditoria()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.Disponible);

        await h.Assign.Handle(new CreateAssetAssignmentCommand(asset.AssetId, TechnicianId, 1, LocationA, null, null), default);

        var fresh = StatusOf(h, asset.AssetId);
        Assert.Equal(AssetStatusCodes.Asignado, fresh.AssetStatus.Code);

        var assignment = h.Context.AssetAssignments.Single();
        Assert.Null(assignment.ReturnedAt);
        Assert.Equal(TechnicianId, assignment.AssignedTo);

        var audits = h.Context.AuditLogs.ToList();
        Assert.Contains(audits, a => a.TableName == "asset_assignments" && a.Action == "INSERT");
        Assert.Contains(audits, a => a.TableName == "assets" && a.Action == "UPDATE"
            && a.NewData != null && a.NewData.Contains("Asignación de resguardo"));
    }

    [Fact]
    public async Task Asignar_ConAsignacionActiva_Rechazado()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.Disponible);
        AddAssignment(h, asset.AssetId);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Assign.Handle(new CreateAssetAssignmentCommand(asset.AssetId, TechnicianId, 1, LocationA, null, null), default).AsTask());

        Assert.Contains("asignación activa", ex.Message);
        Assert.Single(h.Context.AssetAssignments);
    }

    [Fact(Skip = "Requiere transacciones reales de PostgreSQL; cubierto por prueba manual en bloque 1. Pendiente: Testcontainers.PostgreSql en Fase 4")]
    public async Task Devolver_DejaDisponible_RegistraAuditoria()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.Asignado);
        var assignment = AddAssignment(h, asset.AssetId);

        await h.Return.Handle(new EndAssetAssignmentCommand(asset.AssetId, assignment.AssetAssignmentId), default);

        Assert.Equal(AssetStatusCodes.Disponible, StatusOf(h, asset.AssetId).AssetStatus.Code);

        var returned = h.Context.AssetAssignments.AsNoTracking().Single(a => a.AssetAssignmentId == assignment.AssetAssignmentId);
        Assert.NotNull(returned.ReturnedAt);

        var audits = h.Context.AuditLogs.ToList();
        Assert.Contains(audits, a => a.TableName == "asset_assignments" && a.Action == "UPDATE");
        Assert.Contains(audits, a => a.TableName == "assets" && a.Action == "UPDATE"
            && a.NewData != null && a.NewData.Contains(AssetStatusCodes.Disponible));
    }

    [Fact]
    public async Task Devolver_ConIncidenteAbierto_ConservaEstado()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.Asignado);
        var assignment = AddAssignment(h, asset.AssetId);
        AddIncident(h, asset.AssetId, IncidentStatusCodes.Abierto);

        await h.Return.Handle(new EndAssetAssignmentCommand(asset.AssetId, assignment.AssetAssignmentId), default);

        Assert.Equal(AssetStatusCodes.Asignado, StatusOf(h, asset.AssetId).AssetStatus.Code);

        var returned = h.Context.AssetAssignments.AsNoTracking().Single(a => a.AssetAssignmentId == assignment.AssetAssignmentId);
        Assert.NotNull(returned.ReturnedAt);
    }

    [Fact]
    public async Task Devolver_EnOtroEstado_ConservaEstado()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.ConFalla);
        var assignment = AddAssignment(h, asset.AssetId);

        await h.Return.Handle(new EndAssetAssignmentCommand(asset.AssetId, assignment.AssetAssignmentId), default);

        Assert.Equal(AssetStatusCodes.ConFalla, StatusOf(h, asset.AssetId).AssetStatus.Code);
    }

    [Fact(Skip = "Requiere transacciones reales de PostgreSQL; cubierto por prueba manual en bloque 1. Pendiente: Testcontainers.PostgreSql en Fase 4")]
    public async Task Devolver_SegundaVez_Rechazado()
    {
        var h = new Harness();
        var asset = AddAsset(h, AssetStatusCodes.Asignado);
        var assignment = AddAssignment(h, asset.AssetId);

        await h.Return.Handle(new EndAssetAssignmentCommand(asset.AssetId, assignment.AssetAssignmentId), default);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            h.Return.Handle(new EndAssetAssignmentCommand(asset.AssetId, assignment.AssetAssignmentId), default).AsTask());

        Assert.Contains("ya fue devuelta", ex.Message);
    }
}
