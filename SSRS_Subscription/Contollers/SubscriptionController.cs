using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SSRS_Subscription.Models;
using SSRS_Subscription.Services;
using SSRS_Subscription.Utils; // Added to access UrlParser

namespace SSRS_Subscription.Controllers
{
    public class UrlRequest
    {
        public string Url { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api")] 
    public class SubscriptionController : ControllerBase
    {
        private readonly ISsrsService _service;

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

                return Ok(new
                {
                    status = "success",
                    subscription_id = subId,
                    // Dynamically injects "Email" or "FileShare"
                    message = $"Report delivery triggered via {req.DeliveryMethod} (JSON Payload)"
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { detail = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { detail = ex.Message });
            }
        }

        // ✅ Route 2: URL-based Endpoint
        [HttpPost("send-report-from-url")]
        public async Task<IActionResult> SendReportFromUrl([FromBody] UrlRequest req)
        {
            try
            {
                // 1. Parse the URL right here in the controller
                var parsedRequest = UrlParser.ParseReportUrl(req.Url);

                // 2. Pass the fully parsed model to the service
                var subId = await _service.CreateDataDrivenSubscriptionAsync(parsedRequest);
                await _service.TriggerSubscriptionAsync(subId);

                return Ok(new
                {
                    status = "success",
                    subscription_id = subId,
                    // Now we can dynamically read the DeliveryMethod from the parsed request!
                    message = $"Report delivery triggered via {parsedRequest.DeliveryMethod} (URL Parsed)"
                });
            }
            catch (ArgumentException ex)
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