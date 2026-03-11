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

        public int ScheduleMinutes { get; set; } 
        
        public Dictionary<string, object> Parameters { get; set; } = new();

        public string EmailTo { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty; 
        public string FileName { get; set; } = string.Empty; 
        public string FileUserName { get; set; } = string.Empty; 
        public string FilePassword { get; set; } = string.Empty;
    }
}