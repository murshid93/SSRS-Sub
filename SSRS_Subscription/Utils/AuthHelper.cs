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

            // 2. Explicitly tie the credentials to the NTLM scheme and your SSRS Base URL
            // Make sure settings.BaseUrl matches the exact base address (e.g., "http://your-ssrs-server/")
            credentialCache.Add(new Uri(settings.BaseUrl), "NTLM", networkCredential);

            return new HttpClientHandler
            {
                // 3. Assign the cache instead of the raw NetworkCredential
                Credentials = credentialCache, 
                
                // Optional but recommended for NTLM environments to reuse connections
                PreAuthenticate = true 
            };
        }
    }
}