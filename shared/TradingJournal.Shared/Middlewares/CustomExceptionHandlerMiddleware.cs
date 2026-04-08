using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net;
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
    private static readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        },
        Formatting = Formatting.Indented
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<CustomExceptionHandlerMiddleware> _logger;

    public CustomExceptionHandlerMiddleware(RequestDelegate next, ILogger<CustomExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
            
            await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(errorResult, _jsonSerializerSettings));
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogError(ex, ex.Message);
            httpContext.Response.ContentType = "application/json";
            httpContext.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
            Error error = new(ex.ErrorCode, ex.Message);
            
            await httpContext.Response.WriteAsync(JsonConvert.SerializeObject(error, _jsonSerializerSettings));
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
        
        Error error = new(nameof(HttpStatusCode.InternalServerError), exception.Message);
        
        await context.Response.WriteAsync(JsonConvert.SerializeObject(error, _jsonSerializerSettings));
    }
}