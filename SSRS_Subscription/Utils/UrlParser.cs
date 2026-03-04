using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;
using SSRS_Subscription.Models;

namespace SSRS_Subscription.Utils
{
    public static class UrlParser
    {
        // 1. Added the new File Share keys and DeliveryMethod to the ignore list
        private static readonly HashSet<string> TopLevelKeys = new(StringComparer.OrdinalIgnoreCase) 
        { 
            "report_path", "email_to", "subject", "comment", 
            "delivery_method", "file_path", "file_name", "file_username", "file_password"
        };

        public static SubscriptionRequest ParseReportUrl(string urlString)
        {
            if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Invalid URL format.", nameof(urlString));
            }

            var queryParams = QueryHelpers.ParseQuery(uri.Query);

            // Extract core path
            var reportPath = queryParams.TryGetValue("report_path", out var pathVal) ? pathVal.ToString() : string.Empty;
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                throw new ArgumentException("URL is missing the required parameter: 'report_path'.");
            }

            // 2. Determine Delivery Method (Default to Email if not provided or misspelled)
            var deliveryMethodStr = queryParams.TryGetValue("delivery_method", out var dmVal) ? dmVal.ToString() : "Email";
            if (!Enum.TryParse<DeliveryMethod>(deliveryMethodStr, true, out var deliveryMethod))
            {
                deliveryMethod = DeliveryMethod.Email; 
            }

            // Extract Email fields
            var emailTo = queryParams.TryGetValue("email_to", out var emailVal) ? emailVal.ToString() : string.Empty;
            
            // 3. Conditional Validation based on Delivery Method
            if (deliveryMethod == DeliveryMethod.Email && string.IsNullOrWhiteSpace(emailTo))
            {
                throw new ArgumentException("URL is missing the required parameter 'email_to' for Email delivery.");
            }

            // Extract File Share fields
            var filePath = queryParams.TryGetValue("file_path", out var fpVal) ? fpVal.ToString() : string.Empty;
            var fileName = queryParams.TryGetValue("file_name", out var fnVal) ? fnVal.ToString() : string.Empty;
            var fileUsername = queryParams.TryGetValue("file_username", out var fuVal) ? fuVal.ToString() : string.Empty;
            var filePassword = queryParams.TryGetValue("file_password", out var fpassVal) ? fpassVal.ToString() : string.Empty;

            // Extract Subject (prioritize URL parameter, fallback to report name)
            var subject = queryParams.TryGetValue("subject", out var subVal) && !string.IsNullOrWhiteSpace(subVal.ToString()) 
                ? subVal.ToString() 
                : reportPath.TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty;

            var comment = queryParams.TryGetValue("comment", out var commentVal) ? commentVal.ToString() : string.Empty;

            // Extract dynamic SSRS Report Parameters
            var parameters = queryParams
                .Where(kvp => !TopLevelKeys.Contains(kvp.Key))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => ProcessParameterValues(kvp.Value.ToArray())
                );

            return new SubscriptionRequest
            {
                ReportPath = reportPath,
                DeliveryMethod = deliveryMethod,
                
                // Email fields
                EmailTo = emailTo,
                Subject = subject,
                Comment = comment,
                
                // File Share fields
                FilePath = filePath,
                FileName = fileName,
                FileUserName = fileUsername,
                FilePassword = filePassword,
                
                // SSRS Parameters
                Parameters = parameters
            };
        }

        private static object ProcessParameterValues(string?[] values)
        {
            var firstValue = values.FirstOrDefault();

            if (values.Length == 1 && !string.IsNullOrWhiteSpace(firstValue) && firstValue.Contains(','))
            {
                return firstValue.Split(',').Select(v => v.Trim()).ToList();
            }

            return values.Where(v => v != null).ToList()!;
        }
    }
}