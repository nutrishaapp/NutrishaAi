using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace NutrishaAI.API.Models.Requests
{
    public class DocumentUploadDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        
        [RegularExpression("^(workout_plan|diet_plan|other)$", ErrorMessage = "DocumentType must be 'workout_plan', 'diet_plan', or 'other'")]
        public string? DocumentType { get; set; }
        
        public List<string>? Tags { get; set; }
        public bool IsPublic { get; set; } = false;
    }

    public class DocumentUploadRequest
    {
        [Required(ErrorMessage = "File is required")]
        public IFormFile File { get; set; } = null!;
        
        public string? Name { get; set; }
        public string? Description { get; set; }
        
        [RegularExpression("^(workout_plan|diet_plan|other)$", ErrorMessage = "DocumentType must be 'workout_plan', 'diet_plan', or 'other'")]
        public string DocumentType { get; set; } = "other";
        
        public List<string>? Tags { get; set; }
        public bool IsPublic { get; set; } = false;
    }

    public class DocumentUpdateDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Content { get; set; }
        
        [RegularExpression("^(workout_plan|diet_plan|other)$", ErrorMessage = "DocumentType must be 'workout_plan', 'diet_plan', or 'other'")]
        public string? DocumentType { get; set; }
        
        public List<string>? Tags { get; set; }
        public bool? IsPublic { get; set; }
    }

    public class DocumentFilterDto
    {
        [RegularExpression("^(workout_plan|diet_plan|other)$", ErrorMessage = "DocumentType must be 'workout_plan', 'diet_plan', or 'other'")]
        public string? DocumentType { get; set; }
        
        public string? Status { get; set; }
        public bool? IsPublic { get; set; }
        public string? SearchTerm { get; set; }
        public List<string>? Tags { get; set; }
        public int? Limit { get; set; } = 50;
        public int? Offset { get; set; } = 0;
    }
}