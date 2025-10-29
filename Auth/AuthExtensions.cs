using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace GraphGateway.Auth;

public static class AuthExtensions
{
    /// <summary>
    /// Registers JwtBearer authentication and the RequireApiScope authorization policy.
    /// Reads from config sections: AzureAd and Auth.
    /// </summary>
    public static IServiceCollection AddGatewayJwtAuth(
        this IServiceCollection services, IConfiguration configuration)
    {
        var azureAd = configuration.GetSection("AzureAd");
        var auth = configuration.GetSection("Auth");

        var tenantId = Require(azureAd, "TenantId");
        var audience = Require(azureAd, "Audience");
        var apiClientId = Require(azureAd, "ClientId");
        var requiredScope = Require(auth, "RequiredScope");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                options.MapInboundClaims = false;

                var validAudiences = new[] { audience, $"api://{apiClientId}", apiClientId };
                var v2Issuer = $"https://login.microsoftonline.com/{tenantId}/v2.0";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidAudiences = validAudiences,
                    ValidIssuers = new[] { v2Issuer }
                };
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy("RequireApiScope", policy =>
                policy.RequireAssertion(context =>
                {
                    var scp = context.User.FindFirst("scp")?.Value;
                    if (string.IsNullOrEmpty(scp)) return false;
                    return scp.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Contains(requiredScope, StringComparer.OrdinalIgnoreCase);
                }));
        });

        return services;
    }

    /// <summary>
    /// Adds UseAuthentication and UseAuthorization to the pipeline.
    /// </summary>
    public static IApplicationBuilder UseGatewayJwtAuth(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>
    /// Retrieves a required configuration value from a specific subsection of the configuration,
    /// throwing a detailed exception if the key is missing.
    /// </summary>
    /// <param name="section">The configuration section to read from.</param>
    /// <param name="key">The key within the section to retrieve.</param>
    /// <returns>The configuration value associated with the specified key.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configuration key is missing or its value is null.
    /// </exception>
    private static string Require(IConfigurationSection section, string key) =>
    section[key] ?? throw new InvalidOperationException(
        $"Missing configuration: {section.Path}:{key}.");
}