using System.Text;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Middleware;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Common.Storage;
using TecnmInventario.Infrastructure;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Features.Documents;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Deletion;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<TecnmInventarioDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
});
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection(FileStorageOptions.SectionName));
builder.Services.AddSingleton<IFileStorageService, LocalDiskFileStorageService>();

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Secret en user-secrets.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Issuer en user-secrets.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Audience en user-secrets.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<IDocumentAccessAuthorizer, DocumentAccessAuthorizer>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISoftDeleteAuditor, SoftDeleteAuditor>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();





builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
