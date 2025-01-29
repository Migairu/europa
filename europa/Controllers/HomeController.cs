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

using europa.Data;
using europa.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace europa.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IFileService _fileService;
        private readonly UrlShortenerService _urlShortener;
        private readonly ApplicationDbContext _context;

        private const long MaxTotalFileSizeBasic = 2L * 1024 * 1024 * 1024;

        public HomeController(ILogger<HomeController> logger, IFileService fileService, UrlShortenerService urlShortener, ApplicationDbContext context)
        {
            _logger = logger;
            _fileService = fileService;
            _urlShortener = urlShortener;
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            ViewBag.MaxTotalFileSize = MaxTotalFileSizeBasic;
            return View();
        }

        [HttpGet("error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }

        [HttpGet("d/{id}")]
        [EnableRateLimiting("download")]
        public async Task<IActionResult> DownloadFile(string id)
        {
            try
            {
                var fileTransfer = await _urlShortener.GetOriginalUrl(id);
                if (fileTransfer == null)
                {
                    return NotFound();
                }

                var fileInfo = await _fileService.GetFileInfoAsync(fileTransfer.FileId);
                await _context.SaveChangesAsync();

                return View(fileInfo);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while preparing file download");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        [HttpGet("download-file/{fileId}")]
        [EnableRateLimiting("download")]
        public async Task<IActionResult> DownloadFileContent(string fileId)
        {
            try
            {
                var (stream, iv, salt, isMultiFile) = await _fileService.GetFileStreamAsync(fileId);
                Response.Headers.Add("X-IV", Convert.ToBase64String(iv));
                Response.Headers.Add("X-Salt", Convert.ToBase64String(salt));
                Response.Headers.Add("X-Is-Multi-File", isMultiFile.ToString());

                return new FileStreamResult(stream, "application/octet-stream")
                {
                    FileDownloadName = fileId,
                    EnableRangeProcessing = true
                };
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while downloading file");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }
    }
}