using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using GetPodcastLink.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace GetPodcastLink
{
    public static class GetPodcastLink
    {
        static HttpClient client = CreateHttpClient();

        [FunctionName("GetPodcastLink")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            var query = req.Query["query"];
            if (string.IsNullOrEmpty(query))
                return new BadRequestObjectResult(new {
                    message = "Must pass a 'query' parameter",
                });

            Uri queryUrl;
            try
            {
                queryUrl = new Uri(query);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(new {
                    message = ex.Message,
                });
            }

            var lookerUpper = new PodcastLookerUpper(client, log);
            try
            {
                var feedUrls = await lookerUpper.GetFeedUrls(queryUrl, CreateTimeout(TimeSpan.FromSeconds(30)));

                return new OkObjectResult(new {
                    query = queryUrl,
                    feedUrls,
                });
            }
            catch (Exception ex)
            {
                return new ExceptionResult(ex, includeErrorDetail: true);
            }
        }
       
        static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
            };
            var client = new HttpClient(handler);
            
            client.DefaultRequestHeaders.Add("User-Agent", "HttpClient");
            
            return client;
        }
        
        static CancellationToken CreateTimeout(TimeSpan timeoutDuration)
        {
            return new CancellationTokenSource(timeoutDuration).Token;
        }
    }
}
