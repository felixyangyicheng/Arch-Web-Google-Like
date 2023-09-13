
using Microsoft.AspNetCore.Mvc;
using static System.Net.Mime.MediaTypeNames;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;

namespace Google_Like_Blazor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
       // private readonly ILogger<FileController> logger;
        private readonly IFileRepo _fileRepo;
        public FileController(ILogger<FileController> logger, IFileRepo fileRepo)
        {
            //this.logger = logger;
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
         //   logger.LogInformation($"File Attempt GET {fileId}");

            try
            {
                var db = _fileRepo.GetAsync(fileId).Result;
                MemoryStream ms = new MemoryStream(db.Content);

                if (db.Type== "application/pdf")
                {

                    return File(ms, "application/pdf");
                }
                else
                {
                    return File(ms, "application/msword");
                }
            }
            catch (Exception ex)
            {
               // logger.LogError(ex, $"Something Went Wrong in the {nameof(GetOne)}");
                return Problem($"Something Went Wrong in the {nameof(GetOne)}", statusCode: 500);
            }
        }
    }
}
