using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration; // Added for SMTP settings
using Microsoft.Extensions.Options;
using SSRS_Subscription.Models;
using SSRS_Subscription.Utils;

namespace SSRS_Subscription.Services
{
    public class SsrsService : ISsrsService
    {
        private readonly HttpClient _httpClient;
        private readonly SsrsSettings _settings;
        private readonly IConfiguration _config; // Added configuration

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Inject IConfiguration here
        public SsrsService(HttpClient httpClient, IOptions<SsrsSettings> options, IConfiguration config)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _config = config;
        }

        private async Task<string> GetReportIdAsync(string reportPath)
        {
            var reportName = reportPath.TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty;
            var requestUrl = $"CatalogItems?$filter=Name eq '{reportName}'";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (!content.TryGetProperty("value", out var items) || items.GetArrayLength() == 0)
                throw new Exception($"Report not found: {reportName}");

            foreach (var item in items.EnumerateArray())
            {
                var path = item.GetProperty("Path").GetString();
                if (string.Equals(path, reportPath, StringComparison.OrdinalIgnoreCase))
                    return item.GetProperty("Id").GetString()!;
            }

            return items[0].GetProperty("Id").GetString()!;
        }

        // Updated signature to return the Tuple
        public async Task<(string SubscriptionId, string TargetPath)> CreateDataDrivenSubscriptionAsync(SubscriptionRequest request)
        {
            var reportParams = new List<object>();

            // 1. Process standard report parameters
            foreach (var kvp in request.Parameters)
            {
                if (kvp.Value is IEnumerable<string> list && kvp.Value is not string)
                {
                    foreach (var val in list)
                        reportParams.Add(new { Name = kvp.Key, Value = val });
                }
                else
                {
                    reportParams.Add(new
                    {
                        Name = kvp.Key,
                        Value = kvp.Value?.ToString() ?? string.Empty
                    });
                }
            }

            // 2. Determine Extension Settings based on Delivery Method
            string deliveryExtension;
            object extensionSettings;
            string expectedPath = string.Empty; // Added variable to hold the path

            if (request.DeliveryMethod == DeliveryMethod.FileShare)
            {
                var finalPath = string.IsNullOrWhiteSpace(request.FilePath) ? _settings.DefaultFileSharePath : request.FilePath;
                var finalFileName = string.IsNullOrWhiteSpace(request.FileName) ? "@ReportName" : request.FileName;
                
                // Calculate the expected path to return to the controller
                expectedPath = $@"{finalPath}\{finalFileName}.{_settings.DefaultRenderFormat.ToLower()}";

                deliveryExtension = "Report Server FileShare";
                extensionSettings = new
                {
                    ParameterValues = new[]
                    {
                        new { Name = "PATH", Value = finalPath },
                        new { Name = "FILENAME", Value = finalFileName },
                        new { Name = "FILEEXTN", Value = "True" },
                        new { Name = "USERNAME", Value = string.IsNullOrWhiteSpace(request.FileUserName) ? _settings.DefaultFileShareUsername : request.FileUserName },
                        new { Name = "PASSWORD", Value = string.IsNullOrWhiteSpace(request.FilePassword) ? _settings.DefaultFileSharePassword : request.FilePassword },
                        new { Name = "RENDER_FORMAT", Value = _settings.DefaultRenderFormat },
                        new { Name = "WRITEMODE", Value = "Overwrite" } // Overwrite existing files
                    }
                };
            }
            else // Default to Email
            {
                expectedPath = request.EmailTo; // The "path" is just the email

                deliveryExtension = "Report Server Email";
                extensionSettings = new
                {
                    ParameterValues = new[]
                    {
                        new { Name = "TO", Value = request.EmailTo },
                        new { Name = "ReplyTo", Value = request.EmailTo }, 
                        new { Name = "IncludeReport", Value = "True" },
                        new { Name = "IncludeLink", Value = "False" },
                        new { Name = "RenderFormat", Value = _settings.DefaultRenderFormat },
                        new { Name = "Subject", Value = request.Subject },
                        new { Name = "Priority", Value = "NORMAL" },
                        new { Name = "Comment", Value = (!string.IsNullOrWhiteSpace(request.Comment) && !request.Comment.Contains("api", StringComparison.OrdinalIgnoreCase)) ? request.Comment : $"Dear {(string.IsNullOrWhiteSpace(request.EmailTo) ? "User" : (request.EmailTo.Contains("@") ? request.EmailTo.Split('@')[0] : request.EmailTo))},\n\nAttached is the {(string.IsNullOrWhiteSpace(request.Subject) ? "Report" : request.Subject)}\n\nNote: Please do NOT reply to this message. This is a system generated and outgoing message only.\n\nIf you have any queries or comments, please call to Corporate Information Technology Department, Aspial Corporation Limited." }
                    }
                };
            }

            // 3. Build the final payload
            var payload = new
            {
                Report = request.ReportPath,
                Description = $"API Triggered ({request.DeliveryMethod}) - {(request.DeliveryMethod == DeliveryMethod.Email ? request.Subject : request.FileName)}",
                EventType = "TimedSubscription",
                DeliveryExtension = deliveryExtension,

                Schedule = new
                {
                    Definition = new
                    {
                        StartDateTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        EndDateSpecified = false,
                        Recurrence = new
                        {
                            DailyRecurrence = new { DaysInterval = 365 }
                        }
                    }
                },

                ExtensionSettings = extensionSettings,
                ParameterValues = reportParams
            };

            // 4. Send request to SSRS
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("Subscriptions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to create {request.DeliveryMethod} subscription. Status: {response.StatusCode}. Error: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            // Return BOTH the ID and the calculated path
            return (result.GetProperty("Id").GetString()!, expectedPath);
        }

        public async Task TriggerSubscriptionAsync(string subscriptionId)
        {
            var response = await _httpClient.PostAsync(
                $"Subscriptions({subscriptionId})/Model.Execute",
                new StringContent("")
            );

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteSubscriptionAsync(string subscriptionId)
        {
            var response = await _httpClient.DeleteAsync($"Subscriptions({subscriptionId})");
            response.EnsureSuccessStatusCode();
        }

        // ✅ NEW: Method to get SSRS Status
        public async Task<string> GetSubscriptionStatusAsync(string subscriptionId)
        {
            var response = await _httpClient.GetAsync($"Subscriptions({subscriptionId})");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            if (content.TryGetProperty("LastStatus", out var lastStatus))
            {
                return lastStatus.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        
        // ✅ NEW: Background Polling Method 
        public async Task PollAndNotifyAsync(string subscriptionId, string emailTo, string subject, string fallbackPath)
        {
            int maxAttempts = 30; // Max 5 minutes
            int delayMs = 10000; // Check every 10 seconds

            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(delayMs);
                
                var currentStatus = await GetSubscriptionStatusAsync(subscriptionId);

                // SSRS File Share success string check
                if (currentStatus.Contains("has been saved", StringComparison.OrdinalIgnoreCase))
                {
                    // Grab SMTP settings and fire email using our pre-calculated expected path
                    var smtpServer = _config["SmtpSettings:Server"];
                    var smtpPort = int.Parse(_config["SmtpSettings:Port"] ?? "587");
                    var senderEmail = _config["SmtpSettings:SenderEmail"];
                    var smtpUser = _config["SmtpSettings:Username"];
                    var smtpPass = _config["SmtpSettings:Password"];

                    // We now pass 'fallbackPath' directly!
                    await EmailHelper.SendFileReadyNotificationAsync(emailTo, subject, fallbackPath, smtpServer, smtpPort, senderEmail, smtpUser, smtpPass);
                    return; // Exit loop!
                }
                else if (currentStatus.Contains("Failure", StringComparison.OrdinalIgnoreCase) || currentStatus.Contains("Error"))
                {
                    Console.WriteLine($"[ERROR] SSRS failed to save {subscriptionId}. Status: {currentStatus}");
                    return; // Exit loop!
                }
            }
            Console.WriteLine($"[TIMEOUT] Stopped polling {subscriptionId}.");
        }

        // ✅ NEW: Housekeeping method to delete all API-generated subscriptions
        public async Task<int> DeleteApiTriggeredSubscriptionsAsync()
        {
            // 1. Fetch all subscriptions from the report server
            var response = await _httpClient.GetAsync("Subscriptions");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            if (!content.TryGetProperty("value", out var items) || items.GetArrayLength() == 0)
            {
                return 0; // No subscriptions found at all
            }

            int deletedCount = 0;

            // 2. Iterate through all subscriptions
            foreach (var item in items.EnumerateArray())
            {
                var description = item.GetProperty("Description").GetString() ?? string.Empty;
                
                // 3. Check if it was created by our API
                if (description.StartsWith("API Triggered", StringComparison.OrdinalIgnoreCase))
                {
                    var subId = item.GetProperty("Id").GetString();
                    if (!string.IsNullOrWhiteSpace(subId))
                    {
                        // Delete it using your existing method
                        await DeleteSubscriptionAsync(subId);
                        deletedCount++;
                    }
                }
            }

            return deletedCount;
        }
    }
}