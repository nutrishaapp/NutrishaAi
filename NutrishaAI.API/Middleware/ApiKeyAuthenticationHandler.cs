using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using NutrishaAI.API.Services;

namespace NutrishaAI.API.Middleware
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthOptions>
    {
        private readonly IApiKeyService _apiKeyService;
        private readonly IConfiguration _configuration;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IApiKeyService apiKeyService,
            IConfiguration configuration)
            : base(options, logger, encoder, clock)
        {
            _apiKeyService = apiKeyService;
            _configuration = configuration;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Check for API Key in header
            var apiKeyHeaderName = _configuration["ApiKeySettings:HeaderName"] ?? "X-API-Key";
            var userIdHeaderName = _configuration["ApiKeySettings:UserIdHeaderName"] ?? "X-User-Id";

            if (!Request.Headers.TryGetValue(apiKeyHeaderName, out var apiKeyHeader))
            {
                return AuthenticateResult.NoResult();
            }

            if (!Request.Headers.TryGetValue(userIdHeaderName, out var userIdHeader))
            {
                return AuthenticateResult.Fail("User ID required with API Key");
            }

            var apiKey = apiKeyHeader.FirstOrDefault();
            var userId = userIdHeader.FirstOrDefault();

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(userId))
            {
                return AuthenticateResult.Fail("Invalid API Key or User ID");
            }

            try
            {
                // Validate API Key
                var validationResult = await _apiKeyService.ValidateApiKey(apiKey, userId);

                if (!validationResult.IsValid)
                {
                    return AuthenticateResult.Fail(validationResult.Error ?? "Invalid API Key");
                }

                // Create claims
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim("ApiKeyId", validationResult.ApiKeyId),
                    new Claim("AuthMethod", "ApiKey"),
                    new Claim(ClaimTypes.Role, validationResult.UserRole ?? "patient")
                };

                // Add permissions as claims if available
                if (validationResult.Permissions != null)
                {
                    foreach (var permission in validationResult.Permissions)
                    {
                        claims.Add(new Claim("Permission", permission));
                    }
                }

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                // Log usage
                await _apiKeyService.LogUsage(validationResult.ApiKeyId, Request.Path);

                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error validating API key");
                return AuthenticateResult.Fail("An error occurred during authentication");
            }
        }
    }

    public class ApiKeyAuthOptions : AuthenticationSchemeOptions
    {
    }
}