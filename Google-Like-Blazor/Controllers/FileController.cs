using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;

namespace Google_Like_Blazor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly ILogger<FileController> _logger;
        private readonly IFileRepo _fileRepo;

        public FileController(ILogger<FileController> logger, IFileRepo fileRepo)
        {
            _logger = logger;
            _fileRepo = fileRepo;
        }

        /// <summary>
        /// Download a file by ID.
        /// Returns PDF or DOC with ETag and Cache-Control headers for browser caching.
        /// </summary>
        [HttpGet]
        [Route("{fileId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status304NotModified)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOne(string fileId)
        {
            try
            {
                var db = await _fileRepo.GetAsync(fileId);
                if (db == null)
                    return NotFound($"File {fileId} not found");

                // ETag based on content hash (fast, avoids re-sending unchanged files)
                var etag = ComputeETag(db.Content);
                Response.Headers.ETag = etag;

                // Check If-None-Match for 304
                if (Request.Headers.IfNoneMatch == etag)
                    return StatusCode(StatusCodes.Status304NotModified);

                // Cache for 1 hour in browser, 1 day in CDN
                Response.Headers.CacheControl = "public, max-age=3600, s-maxage=86400";

                var ms = new MemoryStream(db.Content);
                var mime = db.Type == "application/pdf" ? "application/pdf" : "application/msword";
                return File(ms, mime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to serve file {FileId}", fileId);
                return Problem($"Something went wrong", statusCode: 500);
            }
        }

        private static string ComputeETag(byte[] content)
        {
            var hash = SHA256.HashData(content);
            return Convert.ToHexStringLower(hash)[..16]; // first 16 hex chars = 64 bits
        }
    }
}
