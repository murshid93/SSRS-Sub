namespace SSRS_Subscription.Models
{
    public class SsrsSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        
        public string Username { get; set; } = string.Empty;
        
        public string Password { get; set; } = string.Empty;
        
        public string Domain { get; set; } = string.Empty;
        
        public string DefaultEmailTo { get; set; } = string.Empty;
        
        public string DefaultRenderFormat { get; set; } = "PDF";
        public string DefaultEmailBody { get; set; } = string.Empty;
    }
}