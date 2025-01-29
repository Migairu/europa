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

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using europa.Models;
using Microsoft.Extensions.Caching.Memory;

namespace europa.Services
{
    public class ChunkService
    {
        private readonly IMemoryCache _cache;
        private readonly AzureStorageConfig _azureStorageConfig;
        private readonly string _containerName = "tempuploads";
        private const int MINUTES_UNTIL_CHUNK_CLEANUP = 30;

        public ChunkService(IMemoryCache cache, AzureStorageConfig azureStorageConfig)
        {
            _cache = cache;
            _azureStorageConfig = azureStorageConfig;
        }

        public async Task<string> InitializeUpload(string fileId, int totalChunks, long totalSize, string iv, string salt, bool isMultiFile, string expirationOption)
        {
            var blobServiceClient = new BlobServiceClient(_azureStorageConfig.ConnectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await blobContainerClient.CreateIfNotExistsAsync();

            var uploadId = Guid.NewGuid().ToString();

            _cache.Set($"upload_{uploadId}", new UploadStatus
            {
                FileId = fileId,
                TotalChunks = totalChunks,
                ReceivedChunks = new HashSet<int>(),
                TotalSize = totalSize,
                ExpiresAt = DateTime.UtcNow.AddMinutes(MINUTES_UNTIL_CHUNK_CLEANUP),
                Iv = iv,
                Salt = salt,
                IsMultiFile = isMultiFile,
                ExpirationOption = expirationOption
            }, TimeSpan.FromMinutes(MINUTES_UNTIL_CHUNK_CLEANUP));

            return uploadId;
        }

        public async Task<bool> UploadChunk(string uploadId, int chunkNumber, byte[] chunkData)
        {
            var uploadStatus = _cache.Get<UploadStatus>($"upload_{uploadId}");
            if (uploadStatus == null) return false;

            var blobServiceClient = new BlobServiceClient(_azureStorageConfig.ConnectionString);
            var blobContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = blobContainerClient.GetBlobClient($"{uploadId}/{chunkNumber}");

            using (var ms = new MemoryStream(chunkData))
            {
                await blobClient.UploadAsync(ms, overwrite: true);
            }

            uploadStatus.ReceivedChunks.Add(chunkNumber);
            _cache.Set($"upload_{uploadId}", uploadStatus, TimeSpan.FromMinutes(MINUTES_UNTIL_CHUNK_CLEANUP));

            return true;
        }

        public async Task<bool> FinalizeUpload(string uploadId, string fileId, int expirationDays)
        {
            var uploadStatus = _cache.Get<UploadStatus>($"upload_{uploadId}");
            if (uploadStatus == null) return false;

            if (uploadStatus.ReceivedChunks.Count != uploadStatus.TotalChunks)
                return false;

            var blobServiceClient = new BlobServiceClient(_azureStorageConfig.ConnectionString);
            var tempContainerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            var finalContainerClient = blobServiceClient.GetBlobContainerClient("encryptedfiles");
            var finalBlob = finalContainerClient.GetBlobClient(fileId);

            using (var finalStream = new MemoryStream((int)uploadStatus.TotalSize))
            {
                for (int i = 0; i < uploadStatus.TotalChunks; i++)
                {
                    var chunkBlob = tempContainerClient.GetBlobClient($"{uploadId}/{i}");

                    if (!await chunkBlob.ExistsAsync())
                    {
                        return false;
                    }

                    var chunk = await chunkBlob.DownloadAsync();
                    await chunk.Value.Content.CopyToAsync(finalStream);
                    await chunkBlob.DeleteAsync();
                }

                finalStream.Position = 0;
                await finalBlob.UploadAsync(finalStream, new BlobUploadOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        { "expirationDate", DateTime.UtcNow.AddDays(expirationDays).ToString("o") },
                        { "iv", uploadStatus.Iv },
                        { "salt", uploadStatus.Salt },
                        { "isMultiFile", uploadStatus.IsMultiFile.ToString() }
                    }
                });
            }

            _cache.Remove($"upload_{uploadId}");
            return true;
        }
    }
}