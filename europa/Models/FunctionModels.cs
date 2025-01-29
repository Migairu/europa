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

namespace europa.Models
{
    public class FileTransfer
    {
        public int Id { get; set; }
        public string FileId { get; set; }
        public string FileName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string ShortUrl { get; set; }
    }
    public class UploadStatus
    {
        public string FileId { get; set; }
        public int TotalChunks { get; set; }
        public HashSet<int> ReceivedChunks { get; set; }
        public long TotalSize { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string Iv { get; set; }
        public string Salt { get; set; }
        public bool IsMultiFile { get; set; }
        public string ExpirationOption { get; set; }
    }
    public class CleanupLog
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int FilesProcessed { get; set; }
        public int FilesDeleted { get; set; }
        public int ErrorCount { get; set; }
        public long BytesFreed { get; set; }
        public string Status { get; set; }
        public string? ErrorDetails { get; set; }
    }
    public class CleanupMetrics
    {
        public int TotalFilesProcessed { get; set; }
        public int SuccessfulDeletes { get; set; }
        public int FailedDeletes { get; set; }
        public long BytesFreed { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
