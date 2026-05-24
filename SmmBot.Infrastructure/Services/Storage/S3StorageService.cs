using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using SmmBot.Core.Interfaces.Settings.Models;
using SmmBot.Core.Interfaces.Storage;

namespace SmmBot.Infrastructure.Services.Storage;

public class S3StorageService : IS3StorageService
{
    private readonly IMinioClient _minioClient;
    private readonly S3StorageSettings _settings;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IOptions<S3StorageSettings> settings, ILogger<S3StorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        
        _minioClient = new MinioClient()
            .WithEndpoint(_settings.Endpoint)
            .WithCredentials(_settings.AccessKey, _settings.SecretKey)
            .Build();
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(_settings.BucketName);
            bool found = await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken);
            if (!found)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(_settings.BucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
            }

            string objectName = $"{Guid.NewGuid()}_{fileName}";

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

            return $"{_settings.Endpoint}/{_settings.BucketName}/{objectName}";
        }
        catch (MinioException e)
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
            var memoryStream = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithCallbackStream((stream) =>
                {
                    stream.CopyTo(memoryStream);
                });

            await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (MinioException e)
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
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);
        }
        catch (MinioException e)
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
