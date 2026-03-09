using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;
using SSRS_Subscription.Models;

namespace SSRS_Subscription.Utils
{
    public static class UrlParser
    {
        // ✅ Added "schedule_minutes" to the top-level keys
        private static readonly HashSet<string> TopLevelKeys = new(StringComparer.OrdinalIgnoreCase) 
        { 
            "report_path", "email_to", "subject", "comment", 
            "delivery_method", "file_path", "file_name", "file_username", "file_password", "schedule_minutes"
        };

        public static SubscriptionRequest ParseReportUrl(string urlString)
        {
            if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Invalid URL format.", nameof(urlString));
            }

            var queryParams = QueryHelpers.ParseQuery(uri.Query);

            var reportPath = queryParams.TryGetValue("report_path", out var pathVal) ? pathVal.ToString() : string.Empty;
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                throw new ArgumentException("URL is missing the required parameter: 'report_path'.");
            }

            var rawReportName = reportPath.TrimEnd('/').Split('/').LastOrDefault() ?? "Report";
            if (rawReportName.EndsWith(".rpt", StringComparison.OrdinalIgnoreCase))
                rawReportName = rawReportName.Substring(0, rawReportName.Length - 4);
            if (rawReportName.StartsWith("rpt", StringComparison.OrdinalIgnoreCase))
                rawReportName = rawReportName.Substring(3);

            var subject = queryParams.TryGetValue("subject", out var subVal) && !string.IsNullOrWhiteSpace(subVal.ToString()) 
                ? subVal.ToString() 
                : rawReportName;

            var emailTo = queryParams.TryGetValue("email_to", out var emailVal) ? emailVal.ToString() : string.Empty;
            var emailUsername = "System";
            if (!string.IsNullOrWhiteSpace(emailTo) && emailTo.Contains('@'))
            {
                emailUsername = emailTo.Split('@')[0];
            }

            var dateAndTimeOfCreation = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dynamicFileName = $"{subject}_{emailUsername}_{dateAndTimeOfCreation}";

            var deliveryMethodStr = queryParams.TryGetValue("delivery_method", out var dmVal) ? dmVal.ToString() : "Email";
            if (!Enum.TryParse<DeliveryMethod>(deliveryMethodStr, true, out var deliveryMethod))
            {
                deliveryMethod = DeliveryMethod.Email; 
            }

            if (deliveryMethod == DeliveryMethod.Email && string.IsNullOrWhiteSpace(emailTo))
            {
                throw new ArgumentException("URL is missing the required parameter 'email_to' for Email delivery.");
            }

            var filePath = queryParams.TryGetValue("file_path", out var fpVal) ? fpVal.ToString() : string.Empty;
            var fileUsername = queryParams.TryGetValue("file_username", out var fuVal) ? fuVal.ToString() : string.Empty;
            var filePassword = queryParams.TryGetValue("file_password", out var fpassVal) ? fpassVal.ToString() : string.Empty;
            var comment = queryParams.TryGetValue("comment", out var commentVal) ? commentVal.ToString() : string.Empty;

            // ✅ Extract schedule minutes (defaults to 0 if not provided)
            var scheduleStr = queryParams.TryGetValue("schedule_minutes", out var schedVal) ? schedVal.ToString() : "0";
            int.TryParse(scheduleStr, out int scheduleMinutes);

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
                EmailTo = emailTo,
                Subject = subject,
                Comment = comment,
                FilePath = filePath,
                FileName = dynamicFileName, 
                FileUserName = fileUsername,
                FilePassword = filePassword,
                ScheduleMinutes = scheduleMinutes, // ✅ Assigned here
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