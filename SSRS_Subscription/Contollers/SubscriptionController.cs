using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection; // Added for IServiceScopeFactory
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
        private readonly IServiceScopeFactory _scopeFactory; // Added to handle background tasks safely

        public SubscriptionController(ISsrsService service, IServiceScopeFactory scopeFactory)
        {
            _service = service;
            _scopeFactory = scopeFactory;
        }

        // ✅ Route 1: JSON Payload Endpoint
        [HttpPost("send-report")]
        public async Task<IActionResult> SendReport([FromBody] SubscriptionRequest req)
        {
            try
            {
                // Unpack the tuple to get both the ID and the calculated path
                var (subId, targetPath) = await _service.CreateDataDrivenSubscriptionAsync(req);
                await _service.TriggerSubscriptionAsync(subId);

                // Start background polling if it is a FileShare and needs an email
                if (req.DeliveryMethod == DeliveryMethod.FileShare && !string.IsNullOrWhiteSpace(req.EmailTo))
                {
                    _ = Task.Run(async () =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var backgroundService = scope.ServiceProvider.GetRequiredService<ISsrsService>();
                        await backgroundService.PollAndNotifyAsync(subId, req.EmailTo, req.Subject, targetPath);
                    });
                }

                return Ok(new
                {
                    status = "success",
                    subscription_id = subId,
                    message = (req.DeliveryMethod == DeliveryMethod.FileShare && !string.IsNullOrWhiteSpace(req.EmailTo))
                        ? $"Report delivery triggered via {req.DeliveryMethod}. Email notification will be sent when saved."
                        : $"Report delivery triggered via {req.DeliveryMethod} (JSON Payload)",
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

        // ✅ Route 2: URL-based Endpoint
        [HttpPost("send-report-from-url")]
        public async Task<IActionResult> SendReportFromUrl([FromBody] UrlRequest req)
        {
            try
            {
                var parsedRequest = UrlParser.ParseReportUrl(req.Url);

                // Unpack the tuple
                var (subId, targetPath) = await _service.CreateDataDrivenSubscriptionAsync(parsedRequest);
                await _service.TriggerSubscriptionAsync(subId);

                // Start background polling if it is a FileShare and needs an email
                if (parsedRequest.DeliveryMethod == DeliveryMethod.FileShare && !string.IsNullOrWhiteSpace(parsedRequest.EmailTo))
                {
                    _ = Task.Run(async () =>
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var backgroundService = scope.ServiceProvider.GetRequiredService<ISsrsService>();
                        await backgroundService.PollAndNotifyAsync(subId, parsedRequest.EmailTo, parsedRequest.Subject, targetPath);
                    });
                }

                return Ok(new
                {
                    status = "success",
                    subscription_id = subId,
                    message = (parsedRequest.DeliveryMethod == DeliveryMethod.FileShare && !string.IsNullOrWhiteSpace(parsedRequest.EmailTo))
                        ? $"Report delivery triggered via {parsedRequest.DeliveryMethod}. Email notification will be sent when saved."
                        : $"Report delivery triggered via {parsedRequest.DeliveryMethod} (URL Parsed)",
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
    }
}