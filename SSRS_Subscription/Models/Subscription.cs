using System.Collections.Generic;

namespace SSRS_Subscription.Models
{
    public class SubscriptionRequest
    {
        public string ReportPath { get; set; } = string.Empty;
        
        public string EmailTo { get; set; } = string.Empty;
        
        public string Subject { get; set; } = string.Empty;
        
        public string Comment { get; set; } = string.Empty;

        
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }
}