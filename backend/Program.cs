using System.Text;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using TecnmInventario.Api.Common.Behaviors;
using TecnmInventario.Api.Common.Middleware;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Common.Storage;
using TecnmInventario.Api.Data;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Authorization;
using TecnmInventario.Api.Features.Documents;
using TecnmInventario.Api.Common.Audit;
using TecnmInventario.Api.Common.Deletion;
using TecnmInventario.Api.Features.Auth.Shared;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

// QuestPDF Community: gratis para instituciones públicas/educativas y organizaciones con
// ingresos menores a 1 M USD (https://www.questpdf.com/license/). El TecNM califica.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

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
builder.Services.AddBusinessPipeline();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddScoped<IAuthCookieWriter, AuthCookieWriter>();
builder.Services.AddScoped<IAuthSessionIssuer, AuthSessionIssuer>();

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

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
var externalCookiePolicy = authOptions.CookieSecure ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

var authentication = builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        // La SPA manda el JWT en la cookie HttpOnly; el header Bearer se
        // conserva para curl y tests.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Request.Headers.Authorization) &&
                    context.Request.Cookies.TryGetValue(AuthCookies.AccessToken, out var token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
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
    })
    // Identidad externa de corta vida: OIDC la deja aquí y el callback la
    // consume para abrir la sesión propia.
    .AddCookie(AuthCookies.ExternalScheme, options =>
    {
        options.Cookie.Name = "tecnm_ext";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = externalCookiePolicy;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    });

// response_mode=query (no form_post) para que las cookies de correlación
// funcionen con SameSite=Lax también en desarrollo sobre HTTP.
void ConfigureOidc(OpenIdConnectOptions options, string authority, string clientId, string clientSecret, string callbackPath)
{
    options.SignInScheme = AuthCookies.ExternalScheme;
    options.Authority = authority;
    options.ClientId = clientId;
    options.ClientSecret = clientSecret;
    options.ResponseType = "code";
    options.ResponseMode = "query";
    options.UsePkce = true;
    options.CallbackPath = callbackPath;
    options.MapInboundClaims = false;
    options.SaveTokens = false;
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    options.CorrelationCookie.SecurePolicy = externalCookiePolicy;
    options.NonceCookie.SameSite = SameSiteMode.Lax;
    options.NonceCookie.SecurePolicy = externalCookiePolicy;
}

if (authOptions.Entra.IsConfigured)
{
    authentication.AddOpenIdConnect(AuthCookies.MicrosoftScheme, "Microsoft", options =>
        ConfigureOidc(options,
            $"https://login.microsoftonline.com/{authOptions.Entra.TenantId}/v2.0",
            authOptions.Entra.ClientId, authOptions.Entra.ClientSecret, "/api/auth/signin-microsoft"));
}

if (authOptions.Google.IsConfigured)
{
    authentication.AddOpenIdConnect(AuthCookies.GoogleScheme, "Google", options =>
    {
        ConfigureOidc(options, "https://accounts.google.com",
            authOptions.Google.ClientId, authOptions.Google.ClientSecret, "/api/auth/signin-google");

        // "hd" solo filtra la pantalla de Google; la validación real del
        // dominio está en ExternalLoginCallbackHandler.
        if (!string.IsNullOrEmpty(authOptions.Google.HostedDomain))
        {
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.SetParameter("hd", authOptions.Google.HostedDomain);
                return Task.CompletedTask;
            };
        }
    });
}

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IRolePermissionService, RolePermissionService>();
builder.Services.AddScoped<IDocumentAccessAuthorizer, DocumentAccessAuthorizer>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISoftDeleteAuditor, SoftDeleteAuditor>();
builder.Services.AddScoped<TecnmInventario.Api.Features.Assets.Shared.IAssetStatusChanger, TecnmInventario.Api.Features.Assets.Shared.AssetStatusChanger>();
builder.Services.AddScoped<TecnmInventario.Api.Features.Incidents.Shared.IncidentResolver>();
builder.Services.AddScoped<TecnmInventario.Api.Features.BulkImport.Shared.AssetImportChecker>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.Configure<TecnmInventario.Api.Features.Tickets.Shared.TicketOptions>(
    builder.Configuration.GetSection(TecnmInventario.Api.Features.Tickets.Shared.TicketOptions.SectionName));
builder.Services.AddHostedService<TecnmInventario.Api.Features.Tickets.Shared.TicketAutoCloseService>();
builder.Services.AddHostedService<RefreshTokenCleanupService>();

// Freno a la fuerza bruta en el login (rate limiter integrado de ASP.NET, sin librerías).
// shortcut: se limita por IP; detrás de un proxy o una red con NAT compartida habría que
// usar X-Forwarded-For (UseForwardedHeaders) y/o sumar un límite por usuario.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthRateLimits.LoginPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = AuthRateLimits.LoginAttemptsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Demasiados intentos",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Demasiados intentos de inicio de sesión. Espera un minuto e intenta de nuevo."
        }, ct);
    };
});





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
app.UseMiddleware<CsrfMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.Run();
