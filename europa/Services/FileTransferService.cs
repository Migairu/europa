/*
* Copyright (C) Migairu Corp.
* Written by Juan Miguel Giraldo.
* 
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
* 
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
* 
* You should have received a copy of the GNU General Public License
* along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.IO.Compression;
using System.Text.Json;
using Azure.Storage.Blobs;
using europa.Services;
using Microsoft.Extensions.Caching.Memory;

public interface IFileService
{
    Task<FileInfo> GetFileInfoAsync(string fileId);
    Task<(Stream FileStream, byte[] IV, byte[] Salt, bool IsMultiFile)> GetFileStreamAsync(string fileId);
    Task DeleteExpiredFileAsync(string fileId);
}

public class FileService : IFileService
{
    private readonly IMemoryCache _cache;
    private readonly AzureStorageConfig _azureStorageConfig;
    private readonly string _containerName = "encryptedfiles";
    private readonly string _tempContainerName = "tempuploads";

    public FileService(IMemoryCache cache, AzureStorageConfig azureStorageConfig)
    {
        _cache = cache;
        _azureStorageConfig = azureStorageConfig;
    }
    public async Task<(Stream FileStream, byte[] IV, byte[] Salt, bool IsMultiFile)> GetFileStreamAsync(string fileId)
    {
        var blobServiceClient = new BlobServiceClient(_azureStorageConfig.ConnectionString);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = blobContainerClient.GetBlobClient(fileId);

        if (!await blobClient.ExistsAsync())
        {
            throw new FileNotFoundException();
        }

        var properties = await blobClient.GetPropertiesAsync();
        var iv = Convert.FromBase64String(properties.Value.Metadata["iv"]);
        var salt = Convert.FromBase64String(properties.Value.Metadata["salt"]);
        var isMultiFile = bool.Parse(properties.Value.Metadata["isMultiFile"]);

        var stream = await blobClient.OpenReadAsync();
        return (stream, iv, salt, isMultiFile);
    }
    public async Task<string> SaveZippedFilesAsync(List<IFormFile> files, List<string> ivs, List<string> salts, int expirationDays)
    {
        var blobServiceClient = new BlobServiceClient(_azureStorageConfig.ConnectionString);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        await blobContainerClient.CreateIfNotExistsAsync();
        var blobName = Guid.NewGuid().ToString();
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        using (var memoryStream = new MemoryStream())
        {
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    var entry = archive.CreateEntry(file.FileName, CompressionLevel.Fastest);
                    using (var entryStream = entry.Open())
                    using (var fileStream = file.OpenReadStream())
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }
            }

            memoryStream.Position = 0;
            await blobClient.UploadAsync(memoryStream);
        }

        var expirationDate = DateTime.UtcNow.AddDays(expirationDays);

        var metadata = new Dictionary<string, string>
        {
            { "expirationDate", expirationDate.ToString("o") },
            { "ivs", JsonSerializer.Serialize(ivs) },
            { "salts", JsonSerializer.Serialize(salts) }
        };

        await blobClient.SetMetadataAsync(metadata);

        return blobName;
    }
    public async Task<FileInfo> GetFileInfoAsync(string fileId)
    {
        string cacheKey = $"FileInfo_{fileId}";
        if (!_cache.TryGetValue(cacheKey, out FileInfo fileInfo))
        {
            var blobServiceClient = new BlobServiceClient(_azureStorageConfig.ConnectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = blobContainerClient.GetBlobClient(fileId);

            if (!await blobClient.ExistsAsync())
            {
                throw new FileNotFoundException();
            }

            var properties = await blobClient.GetPropertiesAsync();

            fileInfo = new FileInfo
            {
                FileId = fileId,
                ExpirationDate = DateTime.Parse(properties.Value.Metadata["expirationDate"]),
                IsMultiFile = bool.Parse(properties.Value.Metadata["isMultiFile"]),
            };

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(cacheKey, fileInfo, cacheEntryOptions);
        }

        return fileInfo;
    }
    public async Task DeleteExpiredFileAsync(string fileId)
    {
        var blobServiceClient = new BlobServiceClient(_azureStorageConfig.ConnectionString);
        var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = blobContainerClient.GetBlobClient(fileId);

        if (await blobClient.ExistsAsync())
        {
            await blobClient.DeleteAsync();
        }
    }
}

public class FileInfo
{
    public string FileId { get; set; }
    public DateTime ExpirationDate { get; set; }
    public bool IsMultiFile { get; set; }
}