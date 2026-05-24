namespace SmmBot.Core.Interfaces.Settings.Models;

public class S3StorageSettings
{
    public required string Endpoint { get; set; }
    public required string AccessKey { get; set; }
    public required string SecretKey { get; set; }
    public required string BucketName { get; set; }
    public bool UseSsl { get; set; }
}
