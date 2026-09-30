using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ValidationException = TecnmInventario.Api.Common.Errors.ValidationException;

namespace TecnmInventario.Api.Common.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Error de validación en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.BadRequest,
                "Error de validación",
                ex.Message,
                ex.Errors);
        }
        catch (Common.Errors.BusinessRuleViolationException ex)
        {
            // Regla de negocio dependiente del estado actual de los datos
            // (no un error de forma detectable por FluentValidation antes
            // de llegar al Handler) -- ej. borrar una ubicación con hijos.
            _logger.LogWarning(ex, "Regla de negocio violada en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.BadRequest,
                "Regla de negocio violada",
                ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Intento de acceso no autorizado en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.Unauthorized,
                "No autorizado",
                ex.Message);
        }
        catch (Common.Errors.ForbiddenAccessException ex)
        {
            _logger.LogWarning(ex, "Acceso prohibido en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.Forbidden,
                "Acceso prohibido",
                ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Recurso no encontrado en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.NotFound,
                "Recurso no encontrado",
                ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Operación inválida en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.Conflict,
                "Operación inválida",
                ex.Message);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // El registro fue modificado o borrado por otro usuario entre que
            // el cliente lo leyó y envió su cambio (WHERE version=X afectó 0 filas).
            _logger.LogWarning(ex, "Conflicto de concurrencia optimista en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.Conflict,
                "Conflicto de concurrencia",
                "El recurso fue modificado por otro usuario mientras tanto. Vuelve a cargarlo e intenta de nuevo.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "P0001" } pgEx)
        {
            // RAISE EXCEPTION sin código SQLSTATE explícito en triggers de negocio
            // (ej. validate_document_owner). EF Core envuelve el PostgresException
            // real dentro de DbUpdateException; el mensaje ya viene redactado para el usuario.
            _logger.LogWarning(ex, "Regla de negocio de base de datos violada en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.BadRequest,
                "Error de validación",
                pgEx.MessageText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);
            await WriteProblemDetailsAsync(
                context,
                HttpStatusCode.InternalServerError,
                "Error interno del servidor",
                "Ocurrió un error inesperado. Contacte al administrador si el problema persiste.");
        }
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string title,
        string detail,
        IDictionary<string, string[]>? errors = null)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
