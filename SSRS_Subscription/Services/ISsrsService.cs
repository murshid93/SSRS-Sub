using System.Threading.Tasks;
using SSRS_Subscription.Models;

namespace SSRS_Subscription.Services
{
    public interface ISsrsService
    {
        Task<string> ProcessSubscriptionFromUrlAsync(string url);
        Task<string> CreateDataDrivenSubscriptionAsync(SubscriptionRequest request);
        Task TriggerSubscriptionAsync(string subscriptionId);
        Task DeleteSubscriptionAsync(string subscriptionId);
    }
}