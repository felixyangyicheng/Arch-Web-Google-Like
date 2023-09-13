
using Microsoft.AspNetCore.Mvc;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Google_Like_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly ILogger<FileController> logger;
        private readonly IFileRepo _fileRepo;
        public FileController(ILogger<FileController> logger, IFileRepo fileRepo)
        {
            this.logger = logger;
            this._fileRepo = fileRepo;
        }
        /// <summary>
        /// Download file
        /// </summary>
        /// <param name="fileId"></param>
        /// <returns></returns>
        [HttpGet]

        [Route("{fileId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]

        public async Task<IActionResult> GetOne( string fileId)
        {
            logger.LogInformation($"File Attempt GET {fileId}");

            try
            {
                var db = _pdfRepo.GetByNameAsync(filename).Result;
                return File(db.Content, "application/pdf");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Something Went Wrong in the {nameof(GetOne)}");
                return Problem($"Something Went Wrong in the {nameof(GetOne)}", statusCode: 500);
            }
        }
    }
}
