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
    [Route("api/[controller]")]
    [Authorize]
    public class DietPlansController : ControllerBase
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<DietPlansController> _logger;

        public DietPlansController(
            Client supabaseClient,
            ILogger<DietPlansController> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetDietPlans([FromQuery] string? status = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

                // TODO: Fix query builder type issue
                var dietPlans = await _supabaseClient
                    .From<DietPlan>()
                    .Get();

                var response = dietPlans.Models.Select(d => new DietPlanResponse
                {
                    Id = d.Id,
                    PatientId = d.PatientId,
                    NutritionistId = d.NutritionistId,
                    Title = d.Title,
                    Description = d.Description,
                    PlanData = d.PlanData,
                    Status = d.Status,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting diet plans");
                return StatusCode(500, new { error = "Failed to get diet plans" });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDietPlan(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var dietPlan = await _supabaseClient
                    .From<DietPlan>()
                    .Where(d => d.Id == id)
                    .Single();

                if (dietPlan == null)
                    return NotFound(new { error = "Diet plan not found" });

                // Check access permission
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole == "nutritionist" && dietPlan.NutritionistId != Guid.Parse(userId))
                    return Forbid();
                if (userRole == "patient" && dietPlan.PatientId != Guid.Parse(userId))
                    return Forbid();

                var response = new DietPlanResponse
                {
                    Id = dietPlan.Id,
                    PatientId = dietPlan.PatientId,
                    NutritionistId = dietPlan.NutritionistId,
                    Title = dietPlan.Title,
                    Description = dietPlan.Description,
                    PlanData = dietPlan.PlanData,
                    Status = dietPlan.Status,
                    CreatedAt = dietPlan.CreatedAt,
                    UpdatedAt = dietPlan.UpdatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting diet plan");
                return StatusCode(500, new { error = "Failed to get diet plan" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "nutritionist,admin")]
        public async Task<IActionResult> CreateDietPlan([FromBody] CreateDietPlanRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                var dietPlan = new DietPlan
                {
                    Id = Guid.NewGuid(),
                    PatientId = request.PatientId,
                    NutritionistId = Guid.Parse(userId),
                    Title = request.Title,
                    Description = request.Description,
                    PlanData = request.PlanData,
                    Status = request.Status,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<DietPlan>()
                    .Insert(dietPlan);

                var response = new DietPlanResponse
                {
                    Id = dietPlan.Id,
                    PatientId = dietPlan.PatientId,
                    NutritionistId = dietPlan.NutritionistId,
                    Title = dietPlan.Title,
                    Description = dietPlan.Description,
                    PlanData = dietPlan.PlanData,
                    Status = dietPlan.Status,
                    CreatedAt = dietPlan.CreatedAt,
                    UpdatedAt = dietPlan.UpdatedAt
                };

                return CreatedAtAction(nameof(GetDietPlan), new { id = dietPlan.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating diet plan");
                return StatusCode(500, new { error = "Failed to create diet plan" });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "nutritionist,admin")]
        public async Task<IActionResult> UpdateDietPlan(Guid id, [FromBody] UpdateDietPlanRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Get existing diet plan
                var existingPlan = await _supabaseClient
                    .From<DietPlan>()
                    .Where(d => d.Id == id)
                    .Where(d => d.NutritionistId == Guid.Parse(userId))
                    .Single();

                if (existingPlan == null)
                    return NotFound(new { error = "Diet plan not found or access denied" });

                // Update fields
                if (!string.IsNullOrEmpty(request.Title))
                    existingPlan.Title = request.Title;
                if (!string.IsNullOrEmpty(request.Description))
                    existingPlan.Description = request.Description;
                if (request.PlanData != null)
                    existingPlan.PlanData = request.PlanData;
                if (!string.IsNullOrEmpty(request.Status))
                    existingPlan.Status = request.Status;
                
                existingPlan.UpdatedAt = DateTime.UtcNow;

                await _supabaseClient
                    .From<DietPlan>()
                    .Where(d => d.Id == id)
                    .Set(d => d.Title, existingPlan.Title)
                    .Set(d => d.Description, existingPlan.Description)
                    .Set(d => d.PlanData, existingPlan.PlanData)
                    .Set(d => d.Status, existingPlan.Status)
                    .Set(d => d.UpdatedAt, existingPlan.UpdatedAt)
                    .Update();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating diet plan");
                return StatusCode(500, new { error = "Failed to update diet plan" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "nutritionist,admin")]
        public async Task<IActionResult> DeleteDietPlan(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _supabaseClient
                    .From<DietPlan>()
                    .Where(d => d.Id == id)
                    .Where(d => d.NutritionistId == Guid.Parse(userId))
                    .Delete();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting diet plan");
                return StatusCode(500, new { error = "Failed to delete diet plan" });
            }
        }
    }
}