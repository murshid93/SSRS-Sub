using System.Threading.Tasks;
using SSRS_Subscription.Models;

namespace SSRS_Subscription.Services
{
    public interface ISsrsService
    {
        // Creates a data driven subscription
        Task<(string SubscriptionId, string TargetPath)> CreateDataDrivenSubscriptionAsync(SubscriptionRequest request);

        // Deletes a subscription
        Task DeleteSubscriptionAsync(string subscriptionId);

        // Processes completed subscriptions and performs cleanup
        Task<int> ProcessAndCleanupCompletedSubscriptionsAsync();
    }
}