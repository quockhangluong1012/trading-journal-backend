using Asp.Versioning;
using Asp.Versioning.ApiExplorer;

namespace TradingJournal.ApiGateWay.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'V";
                options.SubstituteApiVersionInUrl = true;
            });
        
        // Register Swashbuckle services required by Swagger middleware
        services.AddSwaggerGen();
        
        return services;
    }
    
    /// <summary>
    /// Config Swagger UI
    /// </summary>
    /// <param name="app"></param>
    public static void UseSwaggerDoc(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            // Use the IApiVersionDescriptionProvider to dynamically generate Swagger UI URLs
            IApiVersionDescriptionProvider provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            foreach (ApiVersionDescription description in provider.ApiVersionDescriptions)
            {
                c.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                    description.GroupName.ToUpperInvariant());
            }
        });
    }
}