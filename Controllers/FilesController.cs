using Microsoft.AspNetCore.Mvc;

namespace TestProject.Controllers {
    /// <summary>
    /// Controller for browsing and manipulating the file system.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase {
        private readonly string _homeDirectory;
        private readonly ILogger<FilesController> _logger;

        /// <summary>
        /// Constructor for FilesController.
        /// </summary>
        public FilesController(IConfiguration configuration, ILogger<FilesController> logger) {
            _homeDirectory = configuration["FileBrowser:HomeDirectory"] ?? throw new InvalidOperationException("HomeDirectory is not configured.");
            if (!_homeDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                _homeDirectory += Path.DirectorySeparatorChar;
            }
            _homeDirectory = Path.GetFullPath(_homeDirectory);
            _logger = logger;
        }

        /// <summary>
        /// Helper to validate a path against the HomeDirectory to prevent path traversal.
        /// </summary>
        private string? GetValidatedPath(string? relativePath, bool allowTraversal = false) {
            if (relativePath == null) {
                relativePath = "";
            }
            
            // Trim leading slashes so Path.Combine doesn't treat it as an absolute root path
            relativePath = relativePath.TrimStart('/', '\\');
            
            var targetPath = Path.Combine(_homeDirectory, relativePath);
            var normalizedTargetPath = Path.GetFullPath(targetPath);

            if (!allowTraversal && !normalizedTargetPath.StartsWith(_homeDirectory, StringComparison.OrdinalIgnoreCase)) {
                _logger.LogWarning("Path traversal attempt detected. Target path: {TargetPath}", targetPath);
                return null;
            }
            return normalizedTargetPath;
        }

        /// <summary>
        /// Browses files and directories at the specified path relative to the HomeDirectory.
        /// </summary>
        /// <param name="path">The relative path to browse. Must be a child of the configured HomeDirectory to prevent path traversal.</param>
        /// <returns>A list of files and directories within the specified path.</returns>
        /// <response code="200">Returns the list of files and directories.</response>
        /// <response code="400">If the path is invalid or points outside the HomeDirectory.</response>
        /// <response code="404">If the requested directory does not exist.</response>
        /// <response code="500">If an internal error occurs.</response>
        [HttpGet("browse")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Browse([FromQuery] string path = "", [FromQuery] bool hideSystem = true, [FromQuery] bool allowTraversal = false) {
            try {
                var validatedPath = GetValidatedPath(path, allowTraversal);
                if (validatedPath == null) return BadRequest("Invalid path. Path traversal is not allowed.");

                if (!Directory.Exists(validatedPath)) {
                    return NotFound("The specified directory does not exist.");
                }

                var options = new EnumerationOptions {
                    ReturnSpecialDirectories = false,
                    IgnoreInaccessible = true
                };
                if (hideSystem) {
                    options.AttributesToSkip = FileAttributes.System | FileAttributes.Hidden;
                } else {
                    options.AttributesToSkip = 0;
                }

                var entries = new List<object>();
                foreach (var entry in Directory.EnumerateFileSystemEntries(validatedPath, "*", options)) {
                    var isDirectory = Directory.Exists(entry);
                    var fileInfo = isDirectory ? null : new FileInfo(entry);
                    var dirInfo = isDirectory ? new DirectoryInfo(entry) : null;

                    entries.Add(new {
                        name = Path.GetFileName(entry),
                        path = Path.GetRelativePath(_homeDirectory, entry).Replace('\\', '/'),
                        size = isDirectory ? 0 : fileInfo?.Length ?? 0,
                        isDirectory = isDirectory,
                        lastModified = isDirectory ? dirInfo?.LastWriteTime : fileInfo?.LastWriteTime
                    });
                }

                return Ok(entries);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error occurred while browsing directory.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        /// <summary>
        /// Recursively searches for files or directories matching the query string.
        /// </summary>
        /// <param name="query">The string to search for in file and directory names.</param>
        /// <returns>A list of matching files and directories.</returns>
        /// <response code="200">Returns the list of matching entries.</response>
        /// <response code="400">If the search query is empty.</response>
        /// <response code="500">If an internal error occurs.</response>
        [HttpGet("search")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Search([FromQuery] string query, [FromQuery] bool hideSystem = true) {
            if (string.IsNullOrWhiteSpace(query)) {
                return BadRequest("Search query cannot be empty.");
            }

            try {
                var options = new EnumerationOptions {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false
                };
                if (hideSystem) {
                    options.AttributesToSkip = FileAttributes.System | FileAttributes.Hidden;
                } else {
                    options.AttributesToSkip = 0;
                }
                
                var entries = new List<object>();
                foreach (var entry in Directory.EnumerateFileSystemEntries(_homeDirectory, $"*{query}*", options)) {
                    var isDirectory = Directory.Exists(entry);
                    var fileInfo = isDirectory ? null : new FileInfo(entry);
                    var dirInfo = isDirectory ? new DirectoryInfo(entry) : null;

                    entries.Add(new {
                        name = Path.GetFileName(entry),
                        path = Path.GetRelativePath(_homeDirectory, entry).Replace('\\', '/'),
                        size = isDirectory ? 0 : fileInfo?.Length ?? 0,
                        isDirectory = isDirectory,
                        lastModified = isDirectory ? dirInfo?.LastWriteTime : fileInfo?.LastWriteTime
                    });
                }
                
                return Ok(entries);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error occurred while searching.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        /// <summary>
        /// Uploads a file to the specified directory.
        /// </summary>
        /// <param name="file">The file to upload.</param>
        /// <param name="path">The relative path to the target directory.</param>
        /// <returns>Success status.</returns>
        /// <response code="200">File successfully uploaded.</response>
        /// <response code="400">If the path is invalid or the file is missing.</response>
        /// <response code="404">If the target directory does not exist.</response>
        /// <response code="500">If an internal error occurs.</response>
        [HttpPost("upload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Upload([FromForm] UploadRequest request, [FromQuery] string path = "", [FromQuery] bool allowTraversal = false) {
            var file = request.File;
            if (file == null || file.Length == 0) {
                return BadRequest("No file provided.");
            }

            try {
                var validatedPath = GetValidatedPath(path, allowTraversal);
                if (validatedPath == null) return BadRequest("Invalid path. Path traversal is not allowed.");

                if (!Directory.Exists(validatedPath)) {
                    return NotFound("The target directory does not exist.");
                }

                var targetFilePath = Path.Combine(validatedPath, file.FileName);
                using (var stream = new FileStream(targetFilePath, FileMode.Create)) {
                    await file.CopyToAsync(stream);
                }

                return Ok(new { message = "File uploaded successfully." });
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error occurred while uploading file.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        /// <summary>
        /// Downloads a file as a stream.
        /// </summary>
        /// <param name="path">The relative path to the file.</param>
        /// <returns>The file stream.</returns>
        /// <response code="200">Returns the file stream.</response>
        /// <response code="400">If the path is invalid.</response>
        /// <response code="404">If the file does not exist.</response>
        /// <response code="500">If an internal error occurs.</response>
        [HttpGet("download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Download([FromQuery] string path, [FromQuery] bool allowTraversal = false) {
            try {
                var validatedPath = GetValidatedPath(path, allowTraversal);
                if (validatedPath == null) return BadRequest("Invalid path. Path traversal is not allowed.");

                if (!System.IO.File.Exists(validatedPath)) {
                    return NotFound("The specified file does not exist.");
                }

                var stream = new FileStream(validatedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var contentType = "application/octet-stream";
                return File(stream, contentType, Path.GetFileName(validatedPath));
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error occurred while downloading file.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        /// <summary>
        /// Deletes a file or directory recursively.
        /// </summary>
        /// <param name="path">The relative path to delete.</param>
        /// <returns>Success status.</returns>
        /// <response code="200">Item successfully deleted.</response>
        /// <response code="400">If the path is invalid.</response>
        /// <response code="404">If the item does not exist.</response>
        /// <response code="500">If an internal error occurs.</response>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Delete([FromQuery] string path, [FromQuery] bool allowTraversal = false) {
            try {
                var validatedPath = GetValidatedPath(path, allowTraversal);
                if (validatedPath == null) return BadRequest("Invalid path. Path traversal is not allowed.");

                if (validatedPath.Equals(_homeDirectory, StringComparison.OrdinalIgnoreCase)) {
                    return BadRequest("Cannot delete the root HomeDirectory.");
                }

                if (System.IO.File.Exists(validatedPath)) {
                    System.IO.File.Delete(validatedPath);
                    return Ok(new { message = "File deleted successfully." });
                } 
                else if (Directory.Exists(validatedPath)) {
                    Directory.Delete(validatedPath, recursive: true);
                    return Ok(new { message = "Directory deleted successfully." });
                }

                return NotFound("The specified path does not exist.");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error occurred while deleting.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }

        /// <summary>
        /// Moves or renames a file or directory.
        /// </summary>
        /// <param name="request">The move request containing source and destination paths.</param>
        /// <returns>Success status.</returns>
        /// <response code="200">Item successfully moved/renamed.</response>
        /// <response code="400">If paths are invalid or target already exists.</response>
        /// <response code="404">If the source does not exist.</response>
        /// <response code="500">If an internal error occurs.</response>
        [HttpPatch("move")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Move([FromBody] MoveRequest request) {
            try {
                if (request == null || string.IsNullOrWhiteSpace(request.SourcePath) || string.IsNullOrWhiteSpace(request.DestinationPath)) {
                    return BadRequest("SourcePath and DestinationPath are required.");
                }

                var source = GetValidatedPath(request.SourcePath, request.AllowTraversal);
                var dest = GetValidatedPath(request.DestinationPath, request.AllowTraversal);

                if (source == null || dest == null) return BadRequest("Invalid path. Path traversal is not allowed.");

                if (source.Equals(_homeDirectory, StringComparison.OrdinalIgnoreCase)) {
                    return BadRequest("Cannot move the root HomeDirectory.");
                }

                if (System.IO.File.Exists(source)) {
                    if (System.IO.File.Exists(dest) || Directory.Exists(dest)) {
                        return BadRequest("Destination already exists.");
                    }
                    System.IO.File.Move(source, dest);
                    return Ok(new { message = "File moved successfully." });
                }
                else if (Directory.Exists(source)) {
                    if (Directory.Exists(dest) || System.IO.File.Exists(dest)) {
                        return BadRequest("Destination already exists.");
                    }
                    Directory.Move(source, dest);
                    return Ok(new { message = "Directory moved successfully." });
                }

                return NotFound("The source path does not exist.");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error occurred while moving.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An internal error occurred.");
            }
        }
    }

    /// <summary>
    /// Model for the move/rename request.
    /// </summary>
    public class MoveRequest {
        /// <summary>
        /// The source relative path.
        /// </summary>
        public string SourcePath { get; set; } = "";
        
        /// <summary>
        /// The destination relative path.
        /// </summary>
        public string DestinationPath { get; set; } = "";

        /// <summary>
        /// Whether to allow path traversal.
        /// </summary>
        public bool AllowTraversal { get; set; } = false;
    }

    /// <summary>
    /// Model for file upload requests.
    /// </summary>
    public class UploadRequest {
        /// <summary>
        /// The file to upload.
        /// </summary>
        public IFormFile? File { get; set; }
    }
}
