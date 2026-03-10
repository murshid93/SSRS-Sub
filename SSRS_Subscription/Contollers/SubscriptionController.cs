using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SSRS_Subscription.Models;
using SSRS_Subscription.Services;
using SSRS_Subscription.Utils;

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

        // ✅ Cleaned up constructor (no more IServiceScopeFactory needed)
        public SubscriptionController(ISsrsService service)
        {
            _service = service;
        }

        [HttpPost("send-report")]
        public async Task<IActionResult> SendReport([FromBody] SubscriptionRequest req)
        {
            try
            {
                var (subId, targetPath) = await _service.CreateDataDrivenSubscriptionAsync(req);
                
                return Ok(new
                {
                    status = "success",
                    subscription_id = subId,
                    message = $"Subscription created for {req.DeliveryMethod}. It will run on its configured schedule.",
                    expected_destination = targetPath
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { detail = ex.Message });
            }
        }

        [HttpPost("send-report-from-url")]
        public async Task<IActionResult> SendReportFromUrl([FromBody] UrlRequest req)
        {
            try
            {
                var parsedRequest = UrlParser.ParseReportUrl(req.Url);
                var (subId, targetPath) = await _service.CreateDataDrivenSubscriptionAsync(parsedRequest);
                
                return Ok(new
                {
                    status = "success",
                    subscription_id = subId,
                    message = $"Subscription created for {parsedRequest.DeliveryMethod} via URL. It will run on its configured schedule.",
                    expected_destination = targetPath
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

        // ✅ NEW: The Batch Cleanup Endpoint
        [HttpPost("process-completed")]
        public async Task<IActionResult> ProcessCompletedSubscriptions()
        {
            try
            {
                int count = await _service.ProcessAndCleanupCompletedSubscriptionsAsync();

                return Ok(new
                {
                    status = "success",
                    message = $"Successfully processed, notified, and deleted {count} completed API-triggered subscriptions."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { detail = ex.Message });
            }
        }

        
        
    }
}