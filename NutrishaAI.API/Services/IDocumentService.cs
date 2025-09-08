using System;
using System.IO;
using System.Threading.Tasks;
using NutrishaAI.API.Models;
using NutrishaAI.API.Models.Requests;
using NutrishaAI.API.Models.Responses;
using Microsoft.AspNetCore.Http;

namespace NutrishaAI.API.Services
{
    public interface IDocumentService
    {
        Task<Document> UploadDocumentAsync(Guid userId, IFormFile file, DocumentUploadDto dto);
        Task<Document?> GetDocumentAsync(Guid documentId, Guid userId);
        Task<List<Document>> GetUserDocumentsAsync(Guid userId, DocumentFilterDto? filter = null);
        Task<bool> DeleteDocumentAsync(Guid documentId, Guid userId);
        Task<Document?> UpdateDocumentAsync(Guid documentId, Guid userId, DocumentUpdateDto dto);
        Task<(Stream? stream, string? contentType, string? fileName)> DownloadDocumentAsync(Guid documentId, Guid userId);
        Task<Document?> ProcessDocumentContentAsync(Guid documentId);
    }
}