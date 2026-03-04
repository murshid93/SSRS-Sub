using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SSRS_Subscription.Models;
using SSRS_Subscription.Utils;

namespace SSRS_Subscription.Services
{
    public class SsrsService : ISsrsService
    {
        private readonly HttpClient _httpClient;
        private readonly SsrsSettings _settings;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public SsrsService(HttpClient httpClient, IOptions<SsrsSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;
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

        public async Task<string> CreateDataDrivenSubscriptionAsync(SubscriptionRequest request)
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

            if (request.DeliveryMethod == DeliveryMethod.FileShare)
            {
                deliveryExtension = "Report Server FileShare";
                extensionSettings = new
                {
                    ParameterValues = new[]
                    {
                        // Using properties we discussed adding to SsrsSettings for fallbacks
                        new { Name = "PATH", Value = string.IsNullOrWhiteSpace(request.FilePath) ? _settings.DefaultFileSharePath : request.FilePath },
                        new { Name = "FILENAME", Value = string.IsNullOrWhiteSpace(request.FileName) ? "@ReportName" : request.FileName },
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
                deliveryExtension = "Report Server Email";
                extensionSettings = new
                {
                    ParameterValues = new[]
                    {
                        new { Name = "TO", Value = request.EmailTo },
                        new { Name = "ReplyTo", Value = request.EmailTo }, // Could also be mapped to a default setting if needed
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
            return result.GetProperty("Id").GetString()!;
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

        public async Task<string> ProcessSubscriptionFromUrlAsync(string url)
        {
            var request = UrlParser.ParseReportUrl(url);

            var subId = await CreateDataDrivenSubscriptionAsync(request);
            await TriggerSubscriptionAsync(subId);

            return subId;
        }
    }
}