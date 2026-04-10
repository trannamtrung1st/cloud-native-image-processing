namespace CloudNativeImageProcessing.Infrastructure.Options;

public sealed class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>When set, uploads go to Azure Blob Storage.</summary>
    public string? ConnectionString { get; set; }

    public string ContainerName { get; set; } = "images";

    /// <summary>When not using Azure, files are stored under this directory (relative to app base or absolute).</summary>
    public string LocalRootPath { get; set; } = "blob-storage";
}
