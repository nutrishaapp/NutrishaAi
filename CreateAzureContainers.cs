using Azure.Storage.Blobs;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string connectionString = "DefaultEndpointsProtocol=https;AccountName=dareurbodystorage;AccountKey=l7+ZyYWLiaVcCUQpz5Dk5BDB2u9gcrL9SIes6RBWD6p6R/BY1CorcjMaUhfmLVhDhGNfy8PUgOhtdVxVg8zNQw==;EndpointSuffix=core.windows.net";
        
        BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
        
        string[] containers = { "user-uploads", "diet-plans", "profile-pictures" };
        
        foreach (var containerName in containers)
        {
            try
            {
                var containerClient = await blobServiceClient.CreateBlobContainerAsync(containerName);
                Console.WriteLine($"✅ Container '{containerName}' created successfully!");
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("already exists"))
                {
                    Console.WriteLine($"ℹ️ Container '{containerName}' already exists.");
                }
                else
                {
                    Console.WriteLine($"❌ Error creating container '{containerName}': {ex.Message}");
                }
            }
        }
        
        Console.WriteLine("\nAll containers processed!");
    }
}