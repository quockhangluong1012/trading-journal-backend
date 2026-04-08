using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace TradingJournal.ApiGateWay.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Add firebase configuration for authentication
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        _ = services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddCookie("Identity.Application")
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.RequireHttpsMetadata = false;
                options.Audience = configuration["Auth0:Audience"];
                options.Authority = configuration["Auth0:Authority"];
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = true, // Check if the issuer is valid
                    ValidateAudience = true, // Check if the audience is valid
                    ValidateLifetime = true, // Check if the token is not expired
                    ValidateIssuerSigningKey = true,
                    ValidAudience = configuration["Auth0:Audience"],
                    ValidIssuer = configuration["Auth0:Authority"],
                    IssuerSigningKey =
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Auth0:ClientSecret"] ?? "")),
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        return services;
    }
}