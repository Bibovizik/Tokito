using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Tokito.DTOs.AdminDTOs;
using Tokito.Services.DataInitialization;

namespace Tokito.Controllers
{
    [ApiController]
    [Route("api/admin/data-initialization")]
    [Authorize(Roles = "Admin")]
    public class AdminDataInitializationController : ControllerBase
    {
        private readonly IDataInitializationService _dataInitializationService;

        public AdminDataInitializationController(IDataInitializationService dataInitializationService)
        {
            _dataInitializationService = dataInitializationService;
        }

        [HttpPost]
        public async Task<IActionResult> InitializeData(
            [FromBody] DataInitializationRequestDto? request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _dataInitializationService.InitializeAsync(
                    request ?? new DataInitializationRequestDto(),
                    cancellationToken);

                return Ok(result);
            }
            catch (FileNotFoundException exception)
            {
                return NotFound(new { message = exception.Message, path = exception.FileName });
            }
            catch (JsonException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
        }
    }
}
