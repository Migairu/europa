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

using System;
using System.Linq;
using System.Security.Cryptography;
using europa.Data;
using europa.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace europa.Services
{
    public class UrlShortenerService
    {
        private readonly ApplicationDbContext _context;

        public UrlShortenerService(ApplicationDbContext context)
        {
            _context = context;
        }

        public virtual string ShortenUrl(FileTransfer fileTransfer)
        {
            string shortUrl;
            do
            {
                shortUrl = GenerateShortUrl();
            } while (_context.FileTransfers.Any(ft => ft.ShortUrl == shortUrl));

            fileTransfer.ShortUrl = shortUrl;
            return shortUrl;
        }

        public virtual async Task<FileTransfer> GetOriginalUrl(string shortUrl)
        {
            return await _context.FileTransfers
                .FirstOrDefaultAsync(ft => ft.ShortUrl == shortUrl);
        }

        private string GenerateShortUrl()
        {
            var bytes = new byte[6];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
    public class CachedUrlShortenerService : UrlShortenerService
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

        public CachedUrlShortenerService(ApplicationDbContext context, IMemoryCache cache) : base(context)
        {
            _cache = cache;
        }

        public override async Task<FileTransfer> GetOriginalUrl(string shortUrl)
        {
            string cacheKey = $"FileTransfer_{shortUrl}";

            if (!_cache.TryGetValue(cacheKey, out FileTransfer fileTransfer))
            {
                fileTransfer = await base.GetOriginalUrl(shortUrl);

                if (fileTransfer != null)
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(_cacheDuration);

                    _cache.Set(cacheKey, fileTransfer, cacheEntryOptions);
                }
            }

            return fileTransfer;
        }

        // Override the ShortenUrl method to update the cache when a new short URL is created
        public override string ShortenUrl(FileTransfer fileTransfer)
        {
            string shortUrl = base.ShortenUrl(fileTransfer);

            string cacheKey = $"FileTransfer_{shortUrl}";
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(_cacheDuration);

            _cache.Set(cacheKey, fileTransfer, cacheEntryOptions);

            return shortUrl;
        }
    }
    public class UrlMapping
    {
        public int Id { get; set; }
        public string FileIdentifier { get; set; }
        public string ShortUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}