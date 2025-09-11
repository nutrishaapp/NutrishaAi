using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using NutrishaAI.Core.Entities;
using Supabase;
using System.Security.Claims;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/pinned-plans")]
    [Authorize]
    public class PinnedPlansController : ControllerBase
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<PinnedPlansController> _logger;

        public PinnedPlansController(
            Client supabaseClient,
            ILogger<PinnedPlansController> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        /// <summary>
        /// Get all pinned plans (Admin only)
        /// </summary>
        [HttpGet("admin")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> GetAllPinnedPlans([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var offset = (page - 1) * pageSize;
                
                var pinnedPlans = await _supabaseClient
                    .From<PinnedPlan>()
                    .Order(p => p.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                var response = pinnedPlans.Models.Select(p => new PinnedPlanResponse
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    PlanType = p.PlanType,
                    PlanId = p.PlanId,
                    PlanContent = p.PlanContent,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                });

                return Ok(new { data = response, page, pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all pinned plans");
                return StatusCode(500, new { error = "Failed to get pinned plans" });
            }
        }

        /// <summary>
        /// Get pinned plans by user ID
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetPinnedPlansByUserId(Guid userId, [FromQuery] bool? isActive = null)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                // Users can only see their own pinned plans unless they're admin
                var currentUserGuid = Guid.Parse(currentUserId);
                if (userId != currentUserGuid && userRole != "admin")
                    return Forbid();

                var query = _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.UserId == userId);

                if (isActive.HasValue)
                {
                    query = query.Where(p => p.IsActive == isActive.Value);
                }

                var pinnedPlans = await query
                    .Order(p => p.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var response = pinnedPlans.Models.Select(p => new PinnedPlanResponse
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    PlanType = p.PlanType,
                    PlanId = p.PlanId,
                    PlanContent = p.PlanContent,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pinned plans for user {UserId}", userId);
                return StatusCode(500, new { error = "Failed to get pinned plans" });
            }
        }

        /// <summary>
        /// Get active pinned plans for current user
        /// </summary>
        [HttpGet("my-active")]
        public async Task<IActionResult> GetMyActivePinnedPlans()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var userGuid = Guid.Parse(userId);
                var activePlans = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.UserId == userGuid)
                    .Where(p => p.IsActive == true)
                    .Order(p => p.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var response = activePlans.Models.Select(p => new PinnedPlanResponse
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    PlanType = p.PlanType,
                    PlanId = p.PlanId,
                    PlanContent = p.PlanContent,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active pinned plans");
                return StatusCode(500, new { error = "Failed to get active pinned plans" });
            }
        }

        /// <summary>
        /// Get pinned plan by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPinnedPlanById(Guid id)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                var pinnedPlan = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Single();

                if (pinnedPlan == null)
                    return NotFound(new { error = "Pinned plan not found" });

                // Users can only see their own pinned plans unless they're admin
                var currentUserGuid = Guid.Parse(currentUserId);
                if (pinnedPlan.UserId != currentUserGuid && userRole != "admin")
                    return Forbid();

                var response = new PinnedPlanResponse
                {
                    Id = pinnedPlan.Id,
                    UserId = pinnedPlan.UserId,
                    PlanType = pinnedPlan.PlanType,
                    PlanId = pinnedPlan.PlanId,
                    PlanContent = pinnedPlan.PlanContent,
                    StartDate = pinnedPlan.StartDate,
                    EndDate = pinnedPlan.EndDate,
                    IsActive = pinnedPlan.IsActive,
                    CreatedAt = pinnedPlan.CreatedAt,
                    UpdatedAt = pinnedPlan.UpdatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pinned plan {Id}", id);
                return StatusCode(500, new { error = "Failed to get pinned plan" });
            }
        }

        /// <summary>
        /// Create/Pin a new plan
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePinnedPlan([FromBody] PinPlanRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Check if there's already an active plan of the same type for this user
                var userGuid = Guid.Parse(userId);
                var existingActivePlan = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.UserId == userGuid)
                    .Where(p => p.PlanType == request.PlanType)
                    .Where(p => p.IsActive == true)
                    .Get();

                // If there's an existing active plan, deactivate it first
                if (existingActivePlan.Models.Any())
                {
                    await _supabaseClient
                        .From<PinnedPlan>()
                        .Where(p => p.UserId == userGuid)
                        .Where(p => p.PlanType == request.PlanType)
                        .Where(p => p.IsActive == true)
                        .Set(p => p.IsActive, false)
                        .Set(p => p.UpdatedAt, DateTime.UtcNow)
                        .Update();
                }

                var pinnedPlan = new PinnedPlan
                {
                    Id = Guid.NewGuid(),
                    UserId = userGuid,
                    PlanType = request.PlanType,
                    PlanId = request.PlanId,
                    PlanContent = request.PlanContent,
                    StartDate = request.StartDate ?? DateTime.UtcNow.Date,
                    EndDate = request.EndDate,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<PinnedPlan>()
                    .Insert(pinnedPlan);

                var response = new PinnedPlanResponse
                {
                    Id = pinnedPlan.Id,
                    UserId = pinnedPlan.UserId,
                    PlanType = pinnedPlan.PlanType,
                    PlanId = pinnedPlan.PlanId,
                    PlanContent = pinnedPlan.PlanContent,
                    StartDate = pinnedPlan.StartDate,
                    EndDate = pinnedPlan.EndDate,
                    IsActive = pinnedPlan.IsActive,
                    CreatedAt = pinnedPlan.CreatedAt,
                    UpdatedAt = pinnedPlan.UpdatedAt
                };

                return CreatedAtAction(nameof(GetPinnedPlanById), new { id = pinnedPlan.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating pinned plan");
                return StatusCode(500, new { error = "Failed to create pinned plan" });
            }
        }

        /// <summary>
        /// Update a pinned plan
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePinnedPlan(Guid id, [FromBody] UpdatePinnedPlanRequest request)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                // Get the existing plan
                var existingPlan = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Single();

                if (existingPlan == null)
                    return NotFound(new { error = "Pinned plan not found" });

                // Users can only update their own pinned plans unless they're admin
                var currentUserGuid = Guid.Parse(currentUserId);
                if (existingPlan.UserId != currentUserGuid && userRole != "admin")
                    return Forbid();

                // Update the plan
                var updateQuery = _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow);

                if (request.PlanContent != null)
                    updateQuery = updateQuery.Set(p => p.PlanContent, request.PlanContent);
                
                if (request.StartDate.HasValue)
                    updateQuery = updateQuery.Set(p => p.StartDate, request.StartDate);
                
                if (request.EndDate.HasValue)
                    updateQuery = updateQuery.Set(p => p.EndDate, request.EndDate);
                
                if (request.IsActive.HasValue)
                    updateQuery = updateQuery.Set(p => p.IsActive, request.IsActive.Value);

                await updateQuery.Update();

                // Get the updated plan
                var updatedPlan = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Single();

                var response = new PinnedPlanResponse
                {
                    Id = updatedPlan.Id,
                    UserId = updatedPlan.UserId,
                    PlanType = updatedPlan.PlanType,
                    PlanId = updatedPlan.PlanId,
                    PlanContent = updatedPlan.PlanContent,
                    StartDate = updatedPlan.StartDate,
                    EndDate = updatedPlan.EndDate,
                    IsActive = updatedPlan.IsActive,
                    CreatedAt = updatedPlan.CreatedAt,
                    UpdatedAt = updatedPlan.UpdatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating pinned plan {Id}", id);
                return StatusCode(500, new { error = "Failed to update pinned plan" });
            }
        }

        /// <summary>
        /// Unpin/deactivate a plan
        /// </summary>
        [HttpPatch("{id}/unpin")]
        public async Task<IActionResult> UnpinPlan(Guid id)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                // Verify the plan exists and user has permission
                var pinnedPlan = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Single();

                if (pinnedPlan == null)
                    return NotFound(new { error = "Pinned plan not found" });

                // Users can only unpin their own plans unless they're admin
                var currentUserGuid = Guid.Parse(currentUserId);
                if (pinnedPlan.UserId != currentUserGuid && userRole != "admin")
                    return Forbid();

                await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Set(p => p.IsActive, false)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow)
                    .Update();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning plan {Id}", id);
                return StatusCode(500, new { error = "Failed to unpin plan" });
            }
        }

        /// <summary>
        /// Delete a pinned plan (soft delete by setting IsActive = false)
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePinnedPlan(Guid id)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                // Verify the plan exists and user has permission
                var pinnedPlan = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Single();

                if (pinnedPlan == null)
                    return NotFound(new { error = "Pinned plan not found" });

                // Users can only delete their own plans unless they're admin
                var currentUserGuid = Guid.Parse(currentUserId);
                if (pinnedPlan.UserId != currentUserGuid && userRole != "admin")
                    return Forbid();

                // Soft delete by setting IsActive = false
                await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Set(p => p.IsActive, false)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow)
                    .Update();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting pinned plan {Id}", id);
                return StatusCode(500, new { error = "Failed to delete pinned plan" });
            }
        }

        /// <summary>
        /// Hard delete a pinned plan (Admin only)
        /// </summary>
        [HttpDelete("{id}/hard")]
        [Authorize(Roles = "admin")]
        public async Task<IActionResult> HardDeletePinnedPlan(Guid id)
        {
            try
            {
                var pinnedPlan = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Single();

                if (pinnedPlan == null)
                    return NotFound(new { error = "Pinned plan not found" });

                await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == id)
                    .Delete();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error hard deleting pinned plan {Id}", id);
                return StatusCode(500, new { error = "Failed to delete pinned plan" });
            }
        }
    }
}