using System.Net;
using System.Net.Http;
using SSRS_Subscription.Models;

namespace SSRS_Subscription.Utils
{
    public static class AuthHelper
    {
        /// <summary>
        /// Generates an HttpClientHandler configured with NTLM Windows Authentication
        /// </summary>
        public static HttpClientHandler GetNtlmHandler(SsrsSettings settings)
        {
            var credentials = new NetworkCredential(
                settings.Username, 
                settings.Password, 
                settings.Domain
            );

            return new HttpClientHandler
            {
                Credentials = credentials,
                // Optional but recommended for NTLM environments to reuse connections
                PreAuthenticate = true 
            };
        }
    }
}