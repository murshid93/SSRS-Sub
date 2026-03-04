using System.Collections.Generic;

namespace SSRS_Subscription.Models
{
    public enum DeliveryMethod
    {
        Email,
        FileShare
    }

    public class SubscriptionRequest
    {
        // General
        public string ReportPath { get; set; } = string.Empty;
        public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.Email; // Default to email
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        // Email Specific
        public string EmailTo { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        // File Share Specific
        public string FilePath { get; set; } = string.Empty; // e.g., \\server\share\folder
        public string FileName { get; set; } = string.Empty; // e.g., @ReportName or "DailySales"
        public string FileUserName { get; set; } = string.Empty; 
        public string FilePassword { get; set; } = string.Empty;
    }
}