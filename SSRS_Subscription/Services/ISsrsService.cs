using System.Threading.Tasks;
using SSRS_Subscription.Models;

namespace SSRS_Subscription.Services
{
    public interface ISsrsService
    {
        // 1. Updated to return a Tuple: (string SubscriptionId, string TargetPath)
        Task<(string SubscriptionId, string TargetPath)> CreateDataDrivenSubscriptionAsync(SubscriptionRequest request);
        
        Task TriggerSubscriptionAsync(string subscriptionId);
        
        Task DeleteSubscriptionAsync(string subscriptionId);

        // 2. New method to fetch the raw status string from SSRS
        Task<string> GetSubscriptionStatusAsync(string subscriptionId);
        
        // 3. New method to handle the background polling loop and email notification
        Task PollAndNotifyAsync(string subscriptionId, string emailTo, string subject, string fallbackPath);

        // ✅ NEW: Cleanup method
        Task<int> DeleteApiTriggeredSubscriptionsAsync();
    }
}