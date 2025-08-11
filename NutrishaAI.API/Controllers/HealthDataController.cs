using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutrishaAI.Core.Entities;
using Supabase;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;
using System.Security.Claims;

namespace NutrishaAI.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class HealthDataController : ControllerBase
    {
        private readonly Client _supabaseClient;
        private readonly ILogger<HealthDataController> _logger;

        public HealthDataController(
            Client supabaseClient,
            ILogger<HealthDataController> logger)
        {
            _supabaseClient = supabaseClient;
            _logger = logger;
        }

        [HttpGet("patients/{patientId}/health-data")]
        public async Task<IActionResult> GetPatientHealthData(Guid patientId, [FromQuery] string? dataType = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Check if user has access (patient can see their own data, nutritionist can see assigned patients)
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole == "patient" && patientId != Guid.Parse(userId))
                    return Forbid();

                var query = _supabaseClient
                    .From<PatientHealthData>()
                    .Where(h => h.PatientId == patientId);

                if (!string.IsNullOrEmpty(dataType))
                {
                    query = query.Where(h => h.DataType == dataType);
                }

                var healthData = await query
                    .Order(h => h.ExtractedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var response = healthData.Models.Select(h => new
                {
                    h.Id,
                    h.PatientId,
                    h.DataType,
                    h.Value,
                    h.ExtractedAt,
                    h.ConfidenceScore,
                    h.VerifiedBy,
                    h.CreatedAt
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient health data");
                return StatusCode(500, new { error = "Failed to get health data" });
            }
        }

        [HttpGet("patients/{patientId}/timeline")]
        public async Task<IActionResult> GetPatientTimeline(Guid patientId, [FromQuery] int days = 30)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Check access permissions
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole == "patient" && patientId != Guid.Parse(userId))
                    return Forbid();

                var fromDate = DateTime.UtcNow.AddDays(-days);

                // Get health data
                var healthData = await _supabaseClient
                    .From<PatientHealthData>()
                    .Where(h => h.PatientId == patientId)
                    .Where(h => h.ExtractedAt >= fromDate)
                    .Order(h => h.ExtractedAt, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                // TODO: Also get conversation messages, meal logs, etc.
                // This would create a comprehensive timeline view

                var response = new
                {
                    PatientId = patientId,
                    TimelineData = healthData.Models.Select(h => new
                    {
                        Type = "health_data",
                        Timestamp = h.ExtractedAt,
                        Data = new
                        {
                            h.DataType,
                            h.Value,
                            h.ConfidenceScore
                        }
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient timeline");
                return StatusCode(500, new { error = "Failed to get timeline" });
            }
        }

        [HttpGet("patients/{patientId}/metrics")]
        public async Task<IActionResult> GetPatientMetrics(
            Guid patientId, 
            [FromQuery] string? type = null,
            [FromQuery] int period = 30)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                // Check access permissions
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole == "patient" && patientId != Guid.Parse(userId))
                    return Forbid();

                var fromDate = DateTime.UtcNow.AddDays(-period);

                var query = _supabaseClient
                    .From<HealthMetric>()
                    .Where(m => m.PatientId == patientId)
                    .Where(m => m.RecordedDate >= fromDate);

                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(m => m.MetricType == type);
                }

                var metrics = await query
                    .Order(m => m.RecordedDate, Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var response = metrics.Models.Select(m => new
                {
                    m.Id,
                    m.PatientId,
                    m.MetricType,
                    m.Value,
                    m.Unit,
                    m.RecordedDate,
                    m.Source
                });

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting patient metrics");
                return StatusCode(500, new { error = "Failed to get metrics" });
            }
        }

        [HttpPost("health-data/verify")]
        [Authorize(Roles = "nutritionist,admin")]
        public async Task<IActionResult> VerifyHealthData([FromBody] VerifyHealthDataRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized();

                await _supabaseClient
                    .From<PatientHealthData>()
                    .Where(h => h.Id == request.HealthDataId)
                    .Set(h => h.VerifiedBy, Guid.Parse(userId))
                    .Update();

                return Ok(new { message = "Health data verified successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying health data");
                return StatusCode(500, new { error = "Failed to verify health data" });
            }
        }
    }

    public class VerifyHealthDataRequest
    {
        public Guid HealthDataId { get; set; }
    }

    // Missing entity classes - let's add them
    [Table("patient_health_data")]
    public class PatientHealthData : BaseModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public Guid? ConversationId { get; set; }
        public Guid? MessageId { get; set; }
        public string DataType { get; set; } = string.Empty;
        public Dictionary<string, object> Value { get; set; } = new();
        public DateTime ExtractedAt { get; set; }
        public float? ConfidenceScore { get; set; }
        public Guid? VerifiedBy { get; set; }
        public string? QdrantPointId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Table("health_metrics")]
    public class HealthMetric : BaseModel
    {
        public Guid Id { get; set; }
        public Guid PatientId { get; set; }
        public string MetricType { get; set; } = string.Empty;
        public float Value { get; set; }
        public string? Unit { get; set; }
        public DateTime RecordedDate { get; set; }
        public string Source { get; set; } = "manual";
        public Guid? SourceMessageId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}