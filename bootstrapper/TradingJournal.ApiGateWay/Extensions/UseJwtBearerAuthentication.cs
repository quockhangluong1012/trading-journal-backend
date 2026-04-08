using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TradingJournal.ApiGateWay.Extensions;

/// <summary>
/// Scalar API Extensions
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>
    /// Config Jwt Bearer for Scalar API
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public static OpenApiOptions UseJwtBearerAuthentication(this OpenApiOptions options)
    {
        OpenApiSecuritySchemeReference schema = new(JwtBearerDefaults.AuthenticationScheme);

        options.AddDocumentTransformer((document, context, ct) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                { JwtBearerDefaults.AuthenticationScheme, schema }
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, ct) =>
        {
            if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
            {
                operation.Security = [new OpenApiSecurityRequirement() {
                    [schema] = []
                }];
            }

            return Task.CompletedTask;
        });
        return options;
    }
}