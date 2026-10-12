using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Core.Entities;
using TecnmInventario.Api.Data;
using TecnmInventario.Api.Features.Auth.Login;

namespace TecnmInventario.Api.Tests;

public sealed class LoginHandlerTests
{
    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public (string Token, DateTime ExpiresAt) GenerateToken(AppUser user)
            => ("fake-token", DateTime.UtcNow.AddHours(1));
    }

    private sealed class CapturingLogger : ILogger<LoginHandler>
    {
        public LogLevel? LastLevel { get; private set; }
        public string? LastMessage { get; private set; }
        public int WarningCount { get; private set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            LastLevel = logLevel;
            LastMessage = formatter(state, exception);
            if (logLevel == LogLevel.Warning)
                WarningCount++;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }

    private static TecnmInventarioDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TecnmInventarioDbContext>()
            .UseInMemoryDatabase($"tecnm-login-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TecnmInventarioDbContext(options);
    }

    private static void SeedRoleAndUser(TecnmInventarioDbContext context, string passwordHash)
    {
        var now = DateTime.UtcNow;
        var role = new Role
        {
            RoleId = 1,
            Code = "ADMIN",
            Name = "Administrador",
            Description = "Rol de administrador",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
            IsDeleted = false
        };
        var user = new AppUser
        {
            UserId = 1,
            Username = "testuser",
            Email = "test@test.local",
            PasswordHash = passwordHash,
            FirstName = "Test",
            LastName = "User",
            RoleId = 1,
            Status = "activo",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
            IsDeleted = false
        };
        context.AddRange(role, user);
        context.SaveChanges();
    }

    [Fact]
    public async Task Handle_EmptyHash_ThrowsAndLogsWarning()
    {
        using var context = CreateContext();
        SeedRoleAndUser(context, string.Empty);

        var logger = new CapturingLogger();
        var handler = new LoginHandler(context, new FakeJwtTokenGenerator(), logger);
        var command = new LoginCommand("testuser", "secret123");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None).AsTask());

        Assert.Equal(1, logger.WarningCount);
        Assert.Equal(LogLevel.Warning, logger.LastLevel);
        Assert.Contains("1", logger.LastMessage); // UserId logged
        Assert.DoesNotContain("secret123", logger.LastMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_SystemAccountHash_ThrowsAndLogsWarning()
    {
        using var context = CreateContext();
        SeedRoleAndUser(context, "SYSTEM_ACCOUNT_NO_LOGIN");

        var logger = new CapturingLogger();
        var handler = new LoginHandler(context, new FakeJwtTokenGenerator(), logger);
        var command = new LoginCommand("testuser", "secret123");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None).AsTask());

        Assert.Equal(1, logger.WarningCount);
        Assert.Equal(LogLevel.Warning, logger.LastLevel);
        Assert.Contains("1", logger.LastMessage);
        Assert.DoesNotContain("SYSTEM_ACCOUNT_NO_LOGIN", logger.LastMessage);
        Assert.DoesNotContain("secret123", logger.LastMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_ValidHashWrongPassword_ThrowsNoWarning()
    {
        using var context = CreateContext();
        var validHash = BCrypt.Net.BCrypt.HashPassword("correct-password", 4);
        SeedRoleAndUser(context, validHash);

        var logger = new CapturingLogger();
        var handler = new LoginHandler(context, new FakeJwtTokenGenerator(), logger);
        var command = new LoginCommand("testuser", "wrong-password");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None).AsTask());

        Assert.Equal(0, logger.WarningCount);
    }

    [Fact]
    public async Task Handle_ValidHashCorrectPassword_ReturnsToken()
    {
        using var context = CreateContext();
        var validHash = BCrypt.Net.BCrypt.HashPassword("correct-password", 4);
        SeedRoleAndUser(context, validHash);

        var logger = new CapturingLogger();
        var handler = new LoginHandler(context, new FakeJwtTokenGenerator(), logger);
        var command = new LoginCommand("testuser", "correct-password");

        var response = await handler.Handle(command, CancellationToken.None).AsTask();

        Assert.NotNull(response);
        Assert.Equal("fake-token", response.Token);
        Assert.Equal("ADMIN", response.RoleCode);
        Assert.Equal(1, response.UserId);
        Assert.Equal("testuser", response.Username);
        Assert.Equal(0, logger.WarningCount);
    }
}