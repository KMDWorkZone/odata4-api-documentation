using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;

namespace WorkZone.OData4.Client
{
    internal class AzureAuthentication
    {
        IConfidentialClientApplication clientApp;
        AcquireTokenForClientParameterBuilder builder;

        public AzureAuthentication(string tenantId, string clientId, string clientSecret, string workzoneApplicationIdUri)
        {
            clientApp = ConfidentialClientApplicationBuilder.Create(clientId)
                .WithTenantId(tenantId)
                .WithClientSecret(clientSecret)
                .Build();

            builder = clientApp.AcquireTokenForClient(new string[] { workzoneApplicationIdUri + "/.default" });
        }

        public async Task<string> GetAccessTokenAsync()
        {
            var result = await builder.ExecuteAsync();
            return result.AccessToken;
        }

        public string GetAccessToken()
        {
            return AsyncHelper.RunSync(GetAccessTokenAsync);
        }

        private static class AsyncHelper
        {
            private static readonly TaskFactory taskFactory = new TaskFactory(CancellationToken.None, TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

            public static TResult RunSync<TResult>(Func<Task<TResult>> func)
            {
                return taskFactory.StartNew(() =>
                {
                    return func();
                }).Unwrap().GetAwaiter().GetResult();
            }
        }
    }
}
