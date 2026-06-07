using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmmBot.Core.Interfaces.Settings.Models;
using SmmBot.Core.Interfaces.Storage;

namespace SmmBot.Infrastructure.Services.Storage;

public class S3StorageService : IS3StorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3StorageSettings _settings;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IOptions<S3StorageSettings> settings, ILogger<S3StorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1" // SeaweedFS often expects a region
        };

        if (string.IsNullOrEmpty(_settings.AccessKey) || _settings.AccessKey == "some_access_key")
        {
            // Use anonymous credentials if no access key is provided, 
            // since SeaweedFS without explicit authentication configured will reject signed requests.
            _s3Client = new AmazonS3Client(new AnonymousAWSCredentials(), config);
        }
        else
        {
            _s3Client = new AmazonS3Client(new BasicAWSCredentials(_settings.AccessKey, _settings.SecretKey), config);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            bool bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _settings.BucketName);
            if (!bucketExists)
            {
                var putBucketRequest = new PutBucketRequest
                {
                    BucketName = _settings.BucketName,
                    UseClientRegion = true
                };
                await _s3Client.PutBucketAsync(putBucketRequest, cancellationToken);
            }

            string objectName = $"{Guid.NewGuid()}_{fileName}";

            var putRequest = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectName,
                InputStream = fileStream,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);

            return $"{_settings.Endpoint}/{_settings.BucketName}/{objectName}";
        }
        catch (AmazonS3Exception e)
        {
            _logger.LogError(e, "Error occurred during file upload to S3.");
            throw;
        }
    }

    public async Task<Stream?> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            string objectName = ExtractObjectNameFromUrl(fileUrl);

            var getRequest = new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectName
            };

            using var response = await _s3Client.GetObjectAsync(getRequest, cancellationToken);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (AmazonS3Exception e)
        {
            _logger.LogError(e, $"Error occurred during file download from S3: {fileUrl}");
            return null;
        }
    }

    public async Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            string objectName = ExtractObjectNameFromUrl(fileUrl);
            
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = objectName
            };

            await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
        }
        catch (AmazonS3Exception e)
        {
            _logger.LogError(e, $"Error occurred during file deletion from S3: {fileUrl}");
        }
    }

    private string ExtractObjectNameFromUrl(string url)
    {
        var bucketPrefix = $"{_settings.Endpoint}/{_settings.BucketName}/";
        if (url.StartsWith(bucketPrefix))
        {
            return url.Substring(bucketPrefix.Length);
        }
        
        return url;
    }
}
