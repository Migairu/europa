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
using europa.Data;
using europa.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Retry;

namespace europa.Services
{
    public class CleanupService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileService _fileService;
        private readonly ILogger<CleanupService> _logger;
        private readonly IMemoryCache _cache;
        private readonly AzureStorageConfig _azureStorageConfig;
        private const int BatchSize = 100;
        private readonly AsyncRetryPolicy _retryPolicy;

        public CleanupService(
            ApplicationDbContext context,
            IFileService fileService,
            ILogger<CleanupService> logger,
            IMemoryCache cache,
            AzureStorageConfig azureStorageConfig)
        {
            _context = context;
            _fileService = fileService;
            _logger = logger;
            _cache = cache;
            _azureStorageConfig = azureStorageConfig;

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }
        public Task CleanupExpiredTransfersHangfireAsync()
        {
            return CleanupExpiredTransfersAsync(CancellationToken.None);
        }

        public async Task<CleanupMetrics> CleanupExpiredTransfersAsync(CancellationToken cancellationToken = default)
        {
            var metrics = new CleanupMetrics();
            var startTime = DateTime.UtcNow;
            var cleanupLog = new CleanupLog
            {
                StartTime = startTime,
                Status = "Started"
            };

            try
            {
                _context.CleanupLogs.Add(cleanupLog);
                await _context.SaveChangesAsync(cancellationToken);

                var now = DateTime.UtcNow;
                var processedCount = 0;

                await using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
                {
                    try
                    {
                        var totalCount = await _context.FileTransfers
                            .Where(ft => ft.ExpirationDate < now)
                            .CountAsync(cancellationToken);

                        for (var skip = 0; skip < totalCount; skip += BatchSize)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _logger.LogWarning("Cleanup operation cancelled");
                                break;
                            }

                            var batch = await _context.FileTransfers
                                .Where(ft => ft.ExpirationDate < now)
                                .OrderBy(ft => ft.ExpirationDate)
                                .Skip(skip)
                                .Take(BatchSize)
                                .ToListAsync(cancellationToken);

                            foreach (var transfer in batch)
                            {
                                try
                                {
                                    await _retryPolicy.ExecuteAsync(async () =>
                                    {
                                        var fileSize = await GetFileSizeAsync(transfer.FileId);
                                        await _fileService.DeleteExpiredFileAsync(transfer.FileId);
                                        metrics.BytesFreed += fileSize;
                                        metrics.SuccessfulDeletes++;

                                        // Clear any cached file info
                                        _cache.Remove($"FileInfo_{transfer.FileId}");
                                        _cache.Remove($"FileTransfer_{transfer.ShortUrl}");
                                    });

                                    _context.FileTransfers.Remove(transfer);

                                    var urlMapping = await _context.UrlMappings
                                        .FirstOrDefaultAsync(um => um.FileIdentifier == transfer.FileId, cancellationToken);
                                    if (urlMapping != null)
                                    {
                                        _context.UrlMappings.Remove(urlMapping);
                                    }

                                    processedCount++;
                                    metrics.TotalFilesProcessed++;

                                    if (processedCount % 10 == 0)
                                    {
                                        await _context.SaveChangesAsync(cancellationToken);
                                    }

                                    _logger.LogInformation($"Cleaned up expired transfer: {transfer.FileId}");
                                }
                                catch (Exception ex)
                                {
                                    metrics.FailedDeletes++;
                                    _logger.LogError(ex, $"Error cleaning up expired transfer: {transfer.FileId}");
                                }
                            }

                            await _context.SaveChangesAsync(cancellationToken);
                        }

                        // Cleanup orphaned files
                        await CleanupOrphanedFilesAsync(cancellationToken);

                        await transaction.CommitAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                }

                metrics.Duration = DateTime.UtcNow - startTime;

                cleanupLog.EndTime = DateTime.UtcNow;
                cleanupLog.FilesProcessed = metrics.TotalFilesProcessed;
                cleanupLog.FilesDeleted = metrics.SuccessfulDeletes;
                cleanupLog.ErrorCount = metrics.FailedDeletes;
                cleanupLog.BytesFreed = metrics.BytesFreed;
                cleanupLog.Status = "Completed";

                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                cleanupLog.EndTime = DateTime.UtcNow;
                cleanupLog.Status = "Failed";
                cleanupLog.ErrorDetails = ex.ToString();
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogError(ex, "Failed to complete cleanup operation");
                throw;
            }

            return metrics;
        }

        private async Task<long> GetFileSizeAsync(string fileId)
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(_azureStorageConfig.ConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient("encryptedfiles");
                var blobClient = containerClient.GetBlobClient(fileId);
                var properties = await blobClient.GetPropertiesAsync();
                return properties.Value.ContentLength;
            }
            catch
            {
                return 0;
            }
        }

        private async Task CleanupOrphanedFilesAsync(CancellationToken cancellationToken)
        {
            var blobServiceClient = new BlobServiceClient(_azureStorageConfig.ConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient("encryptedfiles");

            await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                var fileExists = await _context.FileTransfers
                    .AnyAsync(ft => ft.FileId == blobItem.Name, cancellationToken);

                if (!fileExists)
                {
                    _logger.LogWarning($"Found orphaned file: {blobItem.Name}");
                    try
                    {
                        await containerClient.DeleteBlobAsync(blobItem.Name, cancellationToken: cancellationToken);
                        _logger.LogInformation($"Deleted orphaned file: {blobItem.Name}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to delete orphaned file: {blobItem.Name}");
                    }
                }
            }
        }
        public async Task<CleanupMetrics> GetCleanupMetricsAsync()
        {
            var lastCleanup = await _context.CleanupLogs
                .OrderByDescending(l => l.StartTime)
                .FirstOrDefaultAsync();

            if (lastCleanup == null)
                return new CleanupMetrics();

            return new CleanupMetrics
            {
                TotalFilesProcessed = lastCleanup.FilesProcessed,
                SuccessfulDeletes = lastCleanup.FilesDeleted,
                FailedDeletes = lastCleanup.ErrorCount,
                BytesFreed = lastCleanup.BytesFreed,
                Duration = lastCleanup.EndTime?.Subtract(lastCleanup.StartTime) ?? TimeSpan.Zero
            };
        }
    }
}