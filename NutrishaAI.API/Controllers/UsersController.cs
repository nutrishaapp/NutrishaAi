using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.Core.Entities;
using Supabase;
using System.Security.Claims;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            Client supabaseClient,
            ILogger<UsersController> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetUsers([FromQuery] string? search = null, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            try
            {
                // Build query with search filter
                var users = !string.IsNullOrEmpty(search) 
                    ? await _supabaseClient
                        .From<User>()
                        .Filter("email", Supabase.Postgrest.Constants.Operator.ILike, $"%{search}%")
                        .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                        .Limit(limit)
                        .Offset(offset)
                        .Get()
                    : await _supabaseClient
                        .From<User>()
                        .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                        .Limit(limit)
                        .Offset(offset)
                        .Get();

                var response = users.Models.Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FullName,
                    u.Role,
                    u.PhoneNumber,
                    u.DateOfBirth,
                    u.Gender,
                    u.CreatedAt,
                    u.UpdatedAt
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return StatusCode(500, new { error = "Failed to get users" });
            }
        }

        [HttpGet("{userId}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetUser(Guid userId)
        {
            try
            {
                var userResult = await _supabaseClient
                    .From<User>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                    .Get();

                var user = userResult.Models.FirstOrDefault();
                if (user == null)
                    return NotFound(new { error = "User not found" });

                return Ok(new
                {
                    user.Id,
                    user.Email,
                    user.FullName,
                    user.Role,
                    user.PhoneNumber,
                    user.DateOfBirth,
                    user.Gender,
                    user.CreatedAt,
                    user.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user");
                return StatusCode(500, new { error = "Failed to get user" });
            }
        }

        [HttpPut("{userId}/role")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleRequest request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                // Prevent admin from removing their own admin role
                if (userId.ToString() == currentUserId && request.Role != "admin")
                {
                    return BadRequest(new { error = "Cannot remove your own admin privileges" });
                }

                // Validate role
                var validRoles = new[] { "patient", "nutritionist", "admin" };
                if (!validRoles.Contains(request.Role))
                {
                    return BadRequest(new { error = "Invalid role. Must be patient, nutritionist, or admin" });
                }

                // Get the user
                var userResult = await _supabaseClient
                    .From<User>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                    .Get();

                var user = userResult.Models.FirstOrDefault();
                if (user == null)
                    return NotFound(new { error = "User not found" });

                // Update the role
                user.Role = request.Role;
                user.UpdatedAt = DateTime.UtcNow;

                await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == userId)
                    .Update(user);

                _logger.LogInformation("User {UserId} role updated to {Role} by admin {AdminId}", 
                    userId, request.Role, currentUserId);

                return Ok(new { 
                    message = "User role updated successfully",
                    userId,
                    newRole = request.Role 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user role");
                return StatusCode(500, new { error = "Failed to update user role" });
            }
        }

        [HttpDelete("{userId}")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> DeleteUser(Guid userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                // Prevent admin from deleting themselves
                if (userId.ToString() == currentUserId)
                {
                    return BadRequest(new { error = "Cannot delete your own account" });
                }

                await _supabaseClient
                    .From<User>()
                    .Where(u => u.Id == userId)
                    .Delete();

                _logger.LogInformation("User {UserId} deleted by admin {AdminId}", userId, currentUserId);

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return StatusCode(500, new { error = "Failed to delete user" });
            }
        }

        [HttpGet("stats")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetUserStats()
        {
            try
            {
                var users = await _supabaseClient
                    .From<User>()
                    .Get();

                var stats = new
                {
                    TotalUsers = users.Models.Count,
                    PatientCount = users.Models.Count(u => u.Role == "patient"),
                    NutritionistCount = users.Models.Count(u => u.Role == "nutritionist"),
                    AdminCount = users.Models.Count(u => u.Role == "admin"),
                    RecentSignups = users.Models.Count(u => u.CreatedAt > DateTime.UtcNow.AddDays(-7))
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user stats");
                return StatusCode(500, new { error = "Failed to get user stats" });
            }
        }
    }

    public class UpdateUserRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }
}