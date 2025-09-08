using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.IO;
using System.Threading.Tasks;

class TestAzureBlobUpload
{
    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("🚀 Testing Azure Blob Storage Upload...\n");

            // Load environment variable
            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") 
                ?? throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING environment variable is required");
            
            Console.WriteLine("✅ Connection string loaded successfully");
            
            // Create blob service client
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            Console.WriteLine("✅ Blob service client created");
            
            // Get container reference
            string containerName = "user-uploads";
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            
            // Check if container exists
            var containerExists = await containerClient.ExistsAsync();
            if (!containerExists.Value)
            {
                Console.WriteLine($"❌ Container '{containerName}' does not exist");
                return;
            }
            Console.WriteLine($"✅ Container '{containerName}' exists and accessible");
            
            // Test upload (simulating apple image upload)
            string fileName = $"test-apple-upload-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
            string testContent = "🍎 Test upload - Apple image simulation\nThis simulates uploading an apple image to the user-uploads container.\nTimestamp: " + DateTime.UtcNow.ToString();
            
            // Upload test file
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(testContent)))
            {
                BlobClient blobClient = containerClient.GetBlobClient(fileName);
                
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders 
                    { 
                        ContentType = "text/plain" 
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "uploadedBy", "azure-test" },
                        { "fileType", "image-simulation" }
                    }
                };

                await blobClient.UploadAsync(stream, uploadOptions);
                
                Console.WriteLine($"✅ File uploaded successfully: {fileName}");
                Console.WriteLine($"📍 Blob URL: {blobClient.Uri}");
                
                // Get blob properties to verify upload
                var properties = await blobClient.GetPropertiesAsync();
                Console.WriteLine($"📊 File size: {properties.Value.ContentLength} bytes");
                Console.WriteLine($"🕒 Last modified: {properties.Value.LastModified}");
            }
            
            // List recent files to verify
            Console.WriteLine($"\n📂 Recent files in '{containerName}' container:");
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync().Take(5))
            {
                Console.WriteLine($"  📄 {blobItem.Name} (Size: {blobItem.Properties.ContentLength} bytes, Modified: {blobItem.Properties.LastModified})");
            }
            
            Console.WriteLine("\n🎉 Azure Blob Storage upload test completed successfully!");
            Console.WriteLine("✅ Your Azure configuration is working correctly for file uploads!");
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"💥 Inner Exception: {ex.InnerException.Message}");
            }
            Console.WriteLine($"📝 Stack trace: {ex.StackTrace}");
        }
    }
}