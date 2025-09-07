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
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            Supabase.Client supabaseClient,
            IApiKeyService apiKeyService,
            IJwtService jwtService,
            ILogger<AuthController> logger)
        {
            _supabaseClient = supabaseClient;
            _apiKeyService = apiKeyService;
            _jwtService = jwtService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Register with Supabase Auth with user metadata
                var options = new Supabase.Gotrue.SignUpOptions
                {
                    Data = new Dictionary<string, object>
                    {
                        { "full_name", request.FullName ?? "" },
                        { "role", request.Role ?? "patient" },
                        { "phone_number", request.PhoneNumber ?? "" },
                        { "date_of_birth", request.DateOfBirth?.ToString("yyyy-MM-dd") ?? "" },
                        { "gender", request.Gender ?? "" }
                    }
                };

                var session = await _supabaseClient.Auth.SignUp(request.Email, request.Password, options);

                if (session?.User == null)
                {
                    return BadRequest(new { error = "Registration failed" });
                }

                // Create corresponding record in public.users for API integrations
                var userProfile = new Core.Entities.User
                {
                    Id = Guid.Parse(session.User.Id),
                    Email = request.Email,
                    FullName = request.FullName ?? "",
                    Role = request.Role ?? "patient",
                    PhoneNumber = request.PhoneNumber ?? "",
                    DateOfBirth = request.DateOfBirth,
                    Gender = request.Gender ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _supabaseClient.From<Core.Entities.User>().Insert(userProfile);

                // Generate our own JWT token
                var token = _jwtService.GenerateToken(
                    session.User.Id,
                    session.User.Email ?? request.Email,
                    userProfile.Role
                );

                var response = new AuthResponse
                {
                    AccessToken = token,
                    RefreshToken = session.RefreshToken,
                    ExpiresIn = 3600,
                    User = new UserResponse
                    {
                        Id = Guid.Parse(session.User.Id),
                        Email = request.Email,
                        FullName = request.FullName ?? session.User.Email,
                        Role = request.Role ?? "patient"
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

                // Get user metadata from auth.users
                var userMetadata = session.User.UserMetadata;

                // Ensure public.users record exists for API integrations
                var userId = Guid.Parse(session.User.Id);
                Core.Entities.User? userProfile = null;
                try {
                    userProfile = await _supabaseClient
                        .From<Core.Entities.User>()
                        .Where(x => x.Id == userId)
                        .Single();
                } catch {
                    // Create public.users record from auth.users metadata
                    userProfile = new Core.Entities.User
                    {
                        Id = userId,
                        Email = session.User.Email,
                        FullName = userMetadata?.ContainsKey("full_name") == true ? 
                            userMetadata["full_name"]?.ToString() ?? session.User.Email : 
                            session.User.Email,
                        Role = userMetadata?.ContainsKey("role") == true ? 
                            userMetadata["role"]?.ToString() ?? "patient" : 
                            "patient",
                        PhoneNumber = userMetadata?.ContainsKey("phone_number") == true ? 
                            userMetadata["phone_number"]?.ToString() ?? "" : "",
                        Gender = userMetadata?.ContainsKey("gender") == true ? 
                            userMetadata["gender"]?.ToString() ?? "" : "",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    if (userMetadata?.ContainsKey("date_of_birth") == true && 
                        DateTime.TryParse(userMetadata["date_of_birth"]?.ToString(), out var dob))
                    {
                        userProfile.DateOfBirth = dob;
                    }

                    await _supabaseClient.From<Core.Entities.User>().Insert(userProfile);
                }
                
                // Generate our own JWT token
                var token = _jwtService.GenerateToken(
                    session.User.Id,
                    session.User.Email ?? request.Email,
                    userProfile?.Role ?? (userMetadata?.ContainsKey("role") == true ? 
                        userMetadata["role"]?.ToString() ?? "patient" : "patient")
                );

                var response = new AuthResponse
                {
                    AccessToken = token,
                    RefreshToken = session.RefreshToken,
                    ExpiresIn = 3600,
                    User = new UserResponse
                    {
                        Id = Guid.Parse(session.User.Id),
                        Email = session.User.Email,
                        FullName = userMetadata?.ContainsKey("full_name") == true ? 
                            userMetadata["full_name"]?.ToString() ?? session.User.Email : 
                            session.User.Email,
                        Role = userMetadata?.ContainsKey("role") == true ? 
                            userMetadata["role"]?.ToString() ?? "patient" : 
                            "patient"
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

                // Get user metadata from auth.users
                var userMetadata = session.User.UserMetadata;

                // Generate our own JWT token
                var token = _jwtService.GenerateToken(
                    session.User.Id,
                    session.User.Email ?? "",
                    userMetadata?.ContainsKey("role") == true ? 
                        userMetadata["role"]?.ToString() ?? "patient" : "patient"
                );

                var response = new AuthResponse
                {
                    AccessToken = token,
                    RefreshToken = session.RefreshToken,
                    ExpiresIn = 3600,
                    User = new UserResponse
                    {
                        Id = Guid.Parse(session.User.Id),
                        Email = session.User.Email,
                        FullName = userMetadata?.ContainsKey("full_name") == true ? 
                            userMetadata["full_name"]?.ToString() ?? session.User.Email : 
                            session.User.Email,
                        Role = userMetadata?.ContainsKey("role") == true ? 
                            userMetadata["role"]?.ToString() ?? "patient" : 
                            "patient"
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