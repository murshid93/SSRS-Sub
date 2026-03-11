using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SSRS_Subscription.Models;
using SSRS_Subscription.Utils;

namespace SSRS_Subscription.Services
{
    public class SsrsService : ISsrsService
    {
        private readonly HttpClient _httpClient;
        private readonly SsrsSettings _settings;
        private readonly IConfiguration _config;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public SsrsService(HttpClient httpClient, IOptions<SsrsSettings> options, IConfiguration config)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _config = config;
        }

        public async Task<(string SubscriptionId, string TargetPath)> CreateDataDrivenSubscriptionAsync(SubscriptionRequest request)
        {
            var reportParams = new List<object>();

            foreach (var kvp in request.Parameters)
            {
                if (kvp.Value is IEnumerable<string> list && kvp.Value is not string)
                {
                    foreach (var val in list) reportParams.Add(new { Name = kvp.Key, Value = val });
                }
                else
                {
                    reportParams.Add(new { Name = kvp.Key, Value = kvp.Value?.ToString() ?? string.Empty });
                }
            }

            string deliveryExtension;
            object extensionSettings;
            string expectedPath = string.Empty;

            if (request.DeliveryMethod == DeliveryMethod.FileShare)
            {
                var finalPath = string.IsNullOrWhiteSpace(request.FilePath) ? _settings.DefaultFileSharePath : request.FilePath;
                var finalFileName = string.IsNullOrWhiteSpace(request.FileName) ? "Report" : request.FileName;
                
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
                        new { Name = "WRITEMODE", Value = "Overwrite" } 
                    }
                };
            }
            else 
            {
                expectedPath = request.EmailTo;
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
                        //new { Name = "Comment", Value = request.Comment }
                        new { Name = "Comment", Value = (!string.IsNullOrWhiteSpace(request.Comment) && !request.Comment.Contains("api", StringComparison.OrdinalIgnoreCase)) ? request.Comment : $"Dear {(string.IsNullOrWhiteSpace(request.EmailTo) ? "User" : (request.EmailTo.Contains("@") ? request.EmailTo.Split('@')[0] : request.EmailTo))},\n\nAttached is the {(string.IsNullOrWhiteSpace(request.Subject) ? "Report" : request.Subject)}\n\nNote: Please do NOT reply to this message. This is a system generated and outgoing message only.\n\nIf you have any queries or comments, please call to Corporate Information Technology Department, Aspial Corporation Limited." }

                    }
                };
            }

            // Embed metadata into Description so the cleanup job knows who to email later
            var descriptionMetadata = $"API Triggered | {request.DeliveryMethod} | {request.EmailTo} | {request.Subject}";

            // Set the Delay (If they pass 0, default to 1 minute so it runs "immediately")
            int delayMinutes = request.ScheduleMinutes > 0 ? request.ScheduleMinutes : 1; 

            var payload = new
            {
                Report = request.ReportPath,
                Description = descriptionMetadata,
                EventType = "TimedSubscription",
                DeliveryExtension = deliveryExtension,
                Schedule = new
                {
                    Definition = new
                    {
                        // Add the delayMinutes to the Current UTC Time
                        StartDateTime = DateTime.UtcNow.AddMinutes(delayMinutes).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        EndDateSpecified = false,
                        Recurrence = new
                        {
                            MinuteRecurrence = new { MinutesInterval = 1440 }
                        }
                    }
                },
                ExtensionSettings = extensionSettings,
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
            return (result.GetProperty("Id").GetString()!, expectedPath);
        }

        public async Task DeleteSubscriptionAsync(string subscriptionId)
        {
            var response = await _httpClient.DeleteAsync($"Subscriptions({subscriptionId})");
            response.EnsureSuccessStatusCode();
        }

        // The New Batch Processing & Cleanup Method
        public async Task<int> ProcessAndCleanupCompletedSubscriptionsAsync()
        {
            var response = await _httpClient.GetAsync("Subscriptions");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            if (!content.TryGetProperty("value", out var items) || items.GetArrayLength() == 0)
            {
                Console.WriteLine("[CLEANUP] No subscriptions found on the server.");
                return 0;
            }

            int processedCount = 0;

            foreach (var item in items.EnumerateArray())
            {
                // SAFELY extract properties without crashing if the case is wrong
                var description = item.TryGetProperty("Description", out var descProp) ? descProp.GetString() ?? "" : "";
                var status = item.TryGetProperty("LastStatus", out var statusProp) ? statusProp.GetString() ?? "" : "";
                var subId = item.TryGetProperty("Id", out var idProp) ? idProp.GetString() : "";

                // Only evaluate subscriptions made by our API
                if (description.StartsWith("API Triggered", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"\n[DEBUG] Found API Sub: {subId}");
                    Console.WriteLine($"[DEBUG] Description: {description}");
                    Console.WriteLine($"[DEBUG] Status: {status}");

                    // Make sure it's the NEW format with pipes, otherwise we can't extract the email!
                    if (!description.Contains("|"))
                    {
                        Console.WriteLine("[DEBUG] -> SKIPPING: This subscription uses the old description format. Please delete it manually.");
                        continue;
                    }

                    if (status.Contains("has been saved", StringComparison.OrdinalIgnoreCase) ||
                        status.Contains("Mail sent to", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var parts = description.Split("|");
                            var deliveryMethod = parts.Length > 1 ? parts[1].Trim() : "";
                            var emailTo = parts.Length > 2 ? parts[2].Trim() : "";
                            var subject = parts.Length > 3 ? parts[3].Trim() : "Report";

                            // Send FileShare notification
                            if (deliveryMethod.Equals("FileShare", StringComparison.OrdinalIgnoreCase) && 
                                status.Contains("has been saved", StringComparison.OrdinalIgnoreCase) && 
                                !string.IsNullOrWhiteSpace(emailTo))
                            {
                                string fullPath = "";

                                // Extract exact file name and path directly from the SSRS Status message
                                try 
                                {
                                    int fileStart = status.IndexOf('"') + 1;
                                    int fileEnd = status.IndexOf('"', fileStart);
                                    string fileName = status.Substring(fileStart, fileEnd - fileStart);

                                    int pathStart = status.IndexOf('"', fileEnd + 1) + 1;
                                    int pathEnd = status.IndexOf('"', pathStart);
                                    string folderPath = status.Substring(pathStart, pathEnd - pathStart);

                                    fullPath = $@"{folderPath}\{fileName}";
                                }
                                catch 
                                {
                                    // Fallback just in case the status string format ever changes
                                    fullPath = "your network folder"; 
                                }

                                Console.WriteLine($"[DEBUG] -> Sending email to {emailTo} for {fullPath}");

                                var smtpServer = _config["SmtpSettings:Server"];
                                var smtpPort = int.Parse(_config["SmtpSettings:Port"] ?? "587");
                                var senderEmail = _config["SmtpSettings:SenderEmail"];
                                var smtpUser = _config["SmtpSettings:Username"];
                                var smtpPass = _config["SmtpSettings:Password"];

                                await EmailHelper.SendFileReadyNotificationAsync(emailTo, subject, fullPath, smtpServer, smtpPort, senderEmail, smtpUser, smtpPass);
                            }

                            // Finally, delete the subscription
                            await DeleteSubscriptionAsync(subId!);
                            Console.WriteLine($"[DEBUG] -> Successfully DELETED subscription {subId}");
                            processedCount++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Failed to process/cleanup {subId}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[DEBUG] -> SKIPPING: Status does not indicate completion yet.");
                    }
                }
            }
            return processedCount;
        }

        // Keep this just in case you need to do a manual wipe
        public async Task<int> DeleteApiTriggeredSubscriptionsAsync()
        {
            var response = await _httpClient.GetAsync("Subscriptions");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadFromJsonAsync<JsonElement>();
            
            if (!content.TryGetProperty("value", out var items) || items.GetArrayLength() == 0) return 0;

            int deletedCount = 0;
            foreach (var item in items.EnumerateArray())
            {
                var description = item.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "";
                if (description.StartsWith("API Triggered", StringComparison.OrdinalIgnoreCase))
                {
                    var subId = item.GetProperty("Id").GetString();
                    if (!string.IsNullOrWhiteSpace(subId))
                    {
                        await DeleteSubscriptionAsync(subId);
                        deletedCount++;
                    }
                }
            }
            return deletedCount;
        }
    }
}