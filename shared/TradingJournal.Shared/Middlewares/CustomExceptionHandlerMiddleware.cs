using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradingJournal.Shared.Abstractions;
using TradingJournal.Shared.Exceptions;

namespace TradingJournal.Shared.Middlewares;

public static class CustomExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CustomExceptionHandlerMiddleware>();
    }
}

public class CustomExceptionHandlerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<CustomExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public CustomExceptionHandlerMiddleware(RequestDelegate next, ILogger<CustomExceptionHandlerMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (AccessDeniedException ex)
        {
            await HandleExceptionAsync(httpContext, ex, (int)HttpStatusCode.Forbidden);
        }
        catch (ValidationException ex)
        {
            _logger.LogError(ex, ex.Message);
            httpContext.Response.ContentType = "application/json";
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            List<Error> errors = ex.Errors.Select(x => Error.ValidationError(x.ErrorCode, x.ErrorMessage)).ToList();
            
            Result<IEnumerable<Error>> errorResult = Result<IEnumerable<Error>>.Failure(errors);
            
            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(errorResult, JsonOptions));
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogError(ex, ex.Message);
            httpContext.Response.ContentType = "application/json";
            httpContext.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
            Error error = new(ex.ErrorCode, ex.Message);
            
            await httpContext.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
        }
        catch (NotFoundException ex)
        {
            await HandleExceptionAsync(httpContext, ex, (int)HttpStatusCode.NotFound);
        }
        catch (IntegrationException ex)
        {
            await HandleExceptionAsync(httpContext, ex, (int)HttpStatusCode.InternalServerError);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(httpContext, ex, (int)HttpStatusCode.InternalServerError);
        }
    }

    // Need update to comply with the Mantu API guidline
    // https://dev.azure.com/Mantu/SoftwareArchitecture/_wiki/wikis/API%20Guidelines/6447/REST-API-Guidelines?anchor=7.10.2.-error-condition-responses
    private async Task HandleExceptionAsync(HttpContext context, Exception exception, int statusCode)
    {
        _logger.LogError(exception, exception.Message);
        
        context.Response.ContentType = "application/json";
        
        context.Response.StatusCode = statusCode;

        string errorCode = Enum.GetName(typeof(HttpStatusCode), statusCode) ?? nameof(HttpStatusCode.InternalServerError);

        bool isDevelopment = _environment.IsDevelopment();

        // Only expose real exception details in development to prevent leaking
        // internal infrastructure information (SQL errors, file paths, etc.).
        string message = isDevelopment
            ? exception.Message
            : IsBusinessException(exception)
                ? exception.Message
                : "An internal error occurred. Please try again later.";

        string? innerExceptionMessage = isDevelopment ? GetInnerExceptionMessages(exception) : null;

        string? stackTrace = isDevelopment ? exception.StackTrace : null;

        string? exceptionType = isDevelopment ? exception.GetType().FullName : null;

        var errorResponse = new
        {
            code = errorCode,
            description = message,
            innerErrors = innerExceptionMessage,
            exceptionType,
            stackTrace
        };
        
        await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse, JsonOptions));
    }

    private static string? GetInnerExceptionMessages(Exception exception)
    {
        if (exception.InnerException == null)
            return null;

        var messages = new List<string>();
        var inner = exception.InnerException;
        while (inner != null)
        {
            messages.Add(inner.Message);
            inner = inner.InnerException;
        }
        return string.Join(" → ", messages);
    }

    /// <summary>
    /// Known business/domain exceptions whose messages are safe to expose in production.
    /// </summary>
    private static bool IsBusinessException(Exception exception) =>
        exception is AccessDeniedException
            or NotFoundException
            or IntegrationException
            or BusinessRuleException;
}