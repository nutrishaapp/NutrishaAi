using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.API.Services;
using Supabase;
using Supabase.Gotrue;
using System.Security.Claims;
using NutrishaAI.Core.Entities;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly IApiKeyService _apiKeyService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            Supabase.Client supabaseClient,
            IApiKeyService apiKeyService,
            ILogger<AuthController> logger)
        {
            _supabaseClient = supabaseClient;
            _apiKeyService = apiKeyService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Register with Supabase Auth
                var session = await _supabaseClient.Auth.SignUp(request.Email, request.Password);

                if (session?.User == null)
                {
                    return BadRequest(new { error = "Registration failed" });
                }

                // Create user profile in our users table
                var user = new Core.Entities.User
                {
                    Id = Guid.Parse(session.User.Id),
                    Email = request.Email,
                    FullName = request.FullName,
                    Role = request.Role,
                    PhoneNumber = request.PhoneNumber,
                    DateOfBirth = request.DateOfBirth,
                    Gender = request.Gender,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<Core.Entities.User>()
                    .Insert(user);

                var response = new AuthResponse
                {
                    AccessToken = session.AccessToken,
                    RefreshToken = session.RefreshToken,
                    ExpiresIn = session.ExpiresIn == 0 ? 3600 : (int)session.ExpiresIn,
                    User = new UserResponse
                    {
                        Id = Guid.Parse(session.User.Id),
                        Email = request.Email,
                        FullName = request.FullName,
                        Role = request.Role
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration error");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var session = await _supabaseClient.Auth.SignIn(request.Email, request.Password);

                if (session?.User == null)
                {
                    return Unauthorized(new { error = "Invalid credentials" });
                }

                // Get user profile
                var userProfile = await _supabaseClient
                    .From<Core.Entities.User>()
                    .Where(x => x.Id == Guid.Parse(session.User.Id))
                    .Single();

                var response = new AuthResponse
                {
                    AccessToken = session.AccessToken,
                    RefreshToken = session.RefreshToken,
                    ExpiresIn = session.ExpiresIn == 0 ? 3600 : (int)session.ExpiresIn,
                    User = new UserResponse
                    {
                        Id = Guid.Parse(session.User.Id),
                        Email = session.User.Email,
                        FullName = userProfile?.FullName ?? "",
                        Role = userProfile?.Role ?? "patient"
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                return Unauthorized(new { error = "Invalid credentials" });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var session = await _supabaseClient.Auth.RefreshSession();

                if (session?.User == null)
                {
                    return Unauthorized(new { error = "Invalid refresh token" });
                }

                var response = new AuthResponse
                {
                    AccessToken = session.AccessToken,
                    RefreshToken = session.RefreshToken,
                    ExpiresIn = session.ExpiresIn == 0 ? 3600 : (int)session.ExpiresIn,
                    User = new UserResponse
                    {
                        Id = Guid.Parse(session.User.Id),
                        Email = session.User.Email,
                        FullName = "",
                        Role = "patient"
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh error");
                return Unauthorized(new { error = "Invalid refresh token" });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _supabaseClient.Auth.SignOut();
                return Ok(new { message = "Logged out successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error");
                return Ok(new { message = "Logged out" });
            }
        }
    }
}