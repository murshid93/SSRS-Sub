using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SSRS_Subscription.Models;
using SSRS_Subscription.Services;

namespace SSRS_Subscription.Controllers
{
    // A simple DTO to catch the URL payload, replacing your Pydantic UrlRequest model
    public class UrlRequest
    {
        public string Url { get; set; } = string.Empty;
    }

    [ApiController]
    // You can set a base route for all endpoints here, e.g., "api/subscription" 
    // If you want them at the root level like FastAPI, just use [Route("")]
    [Route("api")] 
    public class SubscriptionController : ControllerBase
    {
        private readonly ISsrsService _service;

        // The interface is injected here automatically by the .NET DI container
        public SubscriptionController(ISsrsService service)
        {
            _service = service;
        }

        // ✅ Route 1: JSON Payload Endpoint
        [HttpPost("send-report")]
        public async Task<IActionResult> SendReport([FromBody] SubscriptionRequest req)
        {
            try
            {
                var subId = await _service.CreateDataDrivenSubscriptionAsync(req);
                await _service.TriggerSubscriptionAsync(subId);

                // Ok() automatically serializes this anonymous object into a 200 JSON response
                return Ok(new
                {
                    status = "success",
                    subscription_id = subId,
                    message = "Report delivered to email (via JSON Payload)"
                });
            }
            catch (ArgumentException ex)
            {
                // Maps to a 400 Bad Request
                return BadRequest(new { detail = ex.Message });
            }
            catch (Exception ex)
            {
                // Maps to a 500 Internal Server Error
                return StatusCode(500, new { detail = ex.Message });
            }
        }

        // ✅ Route 2: URL-based Endpoint
        [HttpPost("send-report-from-url")]
        public async Task<IActionResult> SendReportFromUrl([FromBody] UrlRequest req)
        {
            try
            {
                var subId = await _service.ProcessSubscriptionFromUrlAsync(req.Url);

                return Ok(new
                {
                    status = "success",
                    subscription_id = subId,
                    message = "Report delivered to email (via URL)"
                });
            }
            catch (ArgumentException ex) // Thrown by our UrlParser
            {
                return BadRequest(new { detail = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { detail = ex.Message });
            }
        }
    }
}