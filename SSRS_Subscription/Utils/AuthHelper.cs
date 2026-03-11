using System;
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
            var networkCredential = new NetworkCredential(
                settings.Username, 
                settings.Password, 
                settings.Domain
            );

            // 1. Create the cache
            var credentialCache = new CredentialCache();

            // 2. Add the NetworkCredential to the cache for the specific URL and NTLM scheme
            credentialCache.Add(new Uri(settings.BaseUrl), "NTLM", networkCredential);

            return new HttpClientHandler
            {
                // 3. Assign the cache instead of the raw NetworkCredential
                Credentials = credentialCache, 
                
                PreAuthenticate = true 
            };
        }
    }
}