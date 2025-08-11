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
    [Route("api/plans")]
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

        [HttpPost("pin")]
        public async Task<IActionResult> PinPlan([FromBody] PinPlanRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // First, deactivate any existing active plan of the same type
                await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.UserId == Guid.Parse(userId))
                    .Where(p => p.PlanType == request.PlanType)
                    .Where(p => p.IsActive == true)
                    .Set(p => p.IsActive, false)
                    .Update();

                var pinnedPlan = new PinnedPlan
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.Parse(userId),
                    PlanType = request.PlanType,
                    PlanId = request.PlanId,
                    PlanContent = request.PlanContent,
                    Goals = request.Goals,
                    StartDate = DateTime.UtcNow.Date,
                    EndDate = request.EndDate,
                    IsActive = true,
                    ReminderSettings = request.ReminderSettings,
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
                    Goals = pinnedPlan.Goals,
                    StartDate = pinnedPlan.StartDate,
                    EndDate = pinnedPlan.EndDate,
                    IsActive = pinnedPlan.IsActive,
                    ReminderSettings = pinnedPlan.ReminderSettings,
                    CreatedAt = pinnedPlan.CreatedAt,
                    UpdatedAt = pinnedPlan.UpdatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning plan");
                return StatusCode(500, new { error = "Failed to pin plan" });
            }
        }

        [HttpGet("/api/users/{userId}/pinned-plans")]
        public async Task<IActionResult> GetPinnedPlans(Guid userId)
        {
            try
            {
                var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(currentUserId))
                    return Unauthorized();

                // Users can only see their own pinned plans
                if (userId != Guid.Parse(currentUserId))
                    return Forbid();

                var pinnedPlans = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.UserId == userId)
                    .Where(p => p.IsActive == true)
                    .Order(p => p.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var response = pinnedPlans.Models.Select(p => new PinnedPlanResponse
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    PlanType = p.PlanType,
                    PlanId = p.PlanId,
                    PlanContent = p.PlanContent,
                    Goals = p.Goals,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive,
                    ReminderSettings = p.ReminderSettings,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pinned plans");
                return StatusCode(500, new { error = "Failed to get pinned plans" });
            }
        }

        [HttpDelete("{pinnedPlanId}/unpin")]
        public async Task<IActionResult> UnpinPlan(Guid pinnedPlanId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Verify the plan belongs to the user
                var pinnedPlan = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == pinnedPlanId)
                    .Where(p => p.UserId == Guid.Parse(userId))
                    .Single();

                if (pinnedPlan == null)
                    return NotFound(new { error = "Pinned plan not found" });

                await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.Id == pinnedPlanId)
                    .Set(p => p.IsActive, false)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow)
                    .Update();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unpinning plan");
                return StatusCode(500, new { error = "Failed to unpin plan" });
            }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActivePinnedPlans()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var activePlans = await _supabaseClient
                    .From<PinnedPlan>()
                    .Where(p => p.UserId == Guid.Parse(userId))
                    .Where(p => p.IsActive == true)
                    .Get();

                var response = activePlans.Models.Select(p => new PinnedPlanResponse
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    PlanType = p.PlanType,
                    PlanId = p.PlanId,
                    PlanContent = p.PlanContent,
                    Goals = p.Goals,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive,
                    ReminderSettings = p.ReminderSettings,
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
    }
}