using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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

            var payload = new
            {
                Report = request.ReportPath,   // SSRS expects Report (path)
                Description = $"API Triggered - {request.Subject}",
                EventType = "TimedSubscription",
                DeliveryExtension = "Report Server Email",

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

                ExtensionSettings = new
                {
                    ParameterValues = new[]
                    {
                        new { Name = "TO", Value = request.EmailTo },
                        new { Name = "ReplyTo", Value = request.EmailTo },
                        new { Name = "IncludeReport", Value = "True" },
                        new { Name = "IncludeLink", Value = "False" },
                        new { Name = "RenderFormat", Value = _settings.DefaultRenderFormat },
                        new { Name = "Subject", Value = request.Subject },
                        new { Name = "Priority", Value = "NORMAL" }
                    }
                },

                ParameterValues = reportParams
            };

            
            var json = JsonSerializer.Serialize(payload, JsonOptions);


            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("Subscriptions", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to create subscription. Status: {response.StatusCode}. Error: {error}");
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

        // ✅ Parse URL → Create → Trigger
        public async Task<string> ProcessSubscriptionFromUrlAsync(string url)
        {
            var request = UrlParser.ParseReportUrl(url);

            var subId = await CreateDataDrivenSubscriptionAsync(request);
            await TriggerSubscriptionAsync(subId);

            return subId;
        }
    }
}