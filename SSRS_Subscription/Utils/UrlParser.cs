using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;
using SSRS_Subscription.Models;

namespace SSRS_Subscription.Utils
{
    public static class UrlParser
    {
        // 1. Moved to a class-level variable and made case-insensitive for safety
        private static readonly HashSet<string> TopLevelKeys = new(StringComparer.OrdinalIgnoreCase) 
        { 
            "report_path", "email_to", "subject", "comment" 
        };

        public static SubscriptionRequest ParseReportUrl(string urlString)
        {
            if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Invalid URL format.", nameof(urlString));
            }

            var queryParams = QueryHelpers.ParseQuery(uri.Query);

            // 2. Used TryGetValue for safer and cleaner dictionary access
            var reportPath = queryParams.TryGetValue("report_path", out var pathVal) ? pathVal.ToString() : string.Empty;
            var emailTo = queryParams.TryGetValue("email_to", out var emailVal) ? emailVal.ToString() : string.Empty;

            // 3. Upgraded to IsNullOrWhiteSpace to catch spaces-only inputs
            if (string.IsNullOrWhiteSpace(reportPath) || string.IsNullOrWhiteSpace(emailTo))
            {
                throw new ArgumentException("URL is missing required parameters: 'report_path' or 'email_to'.");
            }

            // 4. Replaced the foreach loop with a clean LINQ expression
            var parameters = queryParams
                .Where(kvp => !TopLevelKeys.Contains(kvp.Key))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => ProcessParameterValues(kvp.Value.ToArray())
                );

            return new SubscriptionRequest
            {
                ReportPath = reportPath,
                EmailTo = emailTo,
                Subject = reportPath.TrimEnd('/').Split('/').LastOrDefault() ?? string.Empty,
                Comment = queryParams.TryGetValue("comment", out var commentVal) ? commentVal.ToString() : string.Empty,
                Parameters = parameters
            };
        }

        // 5. Extracted the complex parameter logic into a dedicated, null-safe helper method
        private static object ProcessParameterValues(string?[] values)
        {
            var firstValue = values.FirstOrDefault();

            // Handle single comma-separated strings safely
            if (values.Length == 1 && !string.IsNullOrWhiteSpace(firstValue) && firstValue.Contains(','))
            {
                return firstValue.Split(',').Select(v => v.Trim()).ToList();
            }

            // Fallback: treat as a standard list of strings, filtering out any accidental nulls
            return values.Where(v => v != null).ToList()!;
        }
    }
}