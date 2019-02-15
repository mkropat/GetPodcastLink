using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GetPodcastLink.Helpers
{
    public class PodcastLookerUpper
    {
        static string[] feedMimeTypes = new[]
        {
            "application/atom+xml",
            "application/rss+xml",
            "application/xml",
        };

        readonly HttpClient _client;
        readonly ILogger _log;

        public PodcastLookerUpper(HttpClient client, ILogger log)
        {
            _client = client;
            _log = log;
        }

        public async Task<IEnumerable<Uri>> GetFeedUrls(Uri podcastUrl, CancellationToken cancelToken)
        {
            var itunesId = GetItunesId(podcastUrl);
            if (!string.IsNullOrEmpty(itunesId))
                return await GetItunesFeedUrls(itunesId, cancelToken);
            
            _log.LogTrace($"[GetFeedUrls] GET {podcastUrl}");
            using (var resp = await _client.GetAsync(podcastUrl, cancelToken))
            {
                if (await IsPodcastFeed(resp, cancelToken))
                    return Enumerable.Repeat(podcastUrl, 1);
                
                return await FindLinksInHtml(resp, podcastUrl, cancelToken);
            }
        }

        async Task<IEnumerable<Uri>> FindLinksInHtml(HttpResponseMessage resp, Uri podcastUrl, CancellationToken cancelToken)
        {
            var contentType = resp.Content.Headers.ContentType;
            if (contentType?.MediaType != "text/html")
                return Enumerable.Empty<Uri>();
                
            var results = new List<Uri>();

            var doc = new HtmlDocument();
            try
            {
                using (var stream = await resp.Content.ReadAsStreamAsync())
                {
                    doc.Load(stream);
                }
            }
            catch (OperationCanceledException)
            {
                return Enumerable.Empty<Uri>();
            }
            
            results.AddRange(await FindItunesLinks(doc, podcastUrl, cancelToken));
            results.AddRange(await FindLinkTags(doc, podcastUrl, cancelToken));
            
            if (!results.Any())
                results.AddRange(await FindATagLinks(doc, podcastUrl, cancelToken));
            
            return results.Distinct();
        }

        async Task<IEnumerable<Uri>> FindATagLinks(HtmlDocument doc, Uri docUrl, CancellationToken cancelToken)
        {
            var links = doc.DocumentNode.SelectNodes("//a").ToArray();
            var urls = links
                .Select(link => TryParseUrl(docUrl, link.Attributes["href"]?.Value))
                .Where(url => url != null);
            var interestingUrls = urls.Take(10)
                .Concat(urls.TakeFromEnd(10))
                .Distinct();
                
            var lookupTasks = interestingUrls.Select(async url => new
            {
                IsPodcastFeed = await IsPodcastFeed(url, cancelToken),
                Url = url,
            });
            return (await Task.WhenAll(lookupTasks))
                .Where(x => x.IsPodcastFeed)
                .Select(x => x.Url);
        }

        async Task<IEnumerable<Uri>> FindItunesLinks(HtmlDocument doc, Uri docUrl, CancellationToken cancelToken)
        {
            var links = doc.DocumentNode.SelectNodes("//a");
            var ids = links
                .Select(x => GetItunesId(TryParseUrl(docUrl, x.Attributes["href"]?.Value)))
                .Where(x => x != null);
                
            var lookupTasks = ids.Select(id => GetItunesFeedUrls(id, cancelToken));
            return (await Task.WhenAll(lookupTasks))
                .SelectMany(x => x);
        }

        async Task<IEnumerable<Uri>> FindLinkTags(HtmlDocument doc, Uri docUrl, CancellationToken cancelToken)
        {
            var links = doc.DocumentNode.SelectNodes("//link");
            var urls = links
                .Where(link => feedMimeTypes.Contains(link.Attributes["type"]?.Value))
                .Select(link => TryParseUrl(docUrl, link.Attributes["href"]?.Value))
                .Where(url => url != null);

            var lookupTasks = urls.Select(async url => new
            {
                IsPodcastFeed = await IsPodcastFeed(url, cancelToken, skipHeadCheck: true),
                Url = url,
            });
            return (await Task.WhenAll(lookupTasks))
                .Where(x => x.IsPodcastFeed)
                .Select(x => x.Url);
        }

        async Task<IEnumerable<Uri>> GetItunesFeedUrls(string id, CancellationToken cancelToken)
        {
            try
            {
                var url = $"https://itunes.apple.com/lookup?id={id}&entity=podcast";
                _log.LogTrace($"[GetItunesFeedUrls] GET {url}");
                using (var response = await _client.GetAsync(url, cancelToken))
                {
                    if (!response.IsSuccessStatusCode)
                        return Enumerable.Empty<Uri>();

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        var apiResult = ParseJsonStream(stream);
                        return apiResult.SelectTokens("results[*].feedUrl")
                            .OfType<JValue>()
                            .Select(x => x.Value as string)
                            .Where(x => x != null)
                            .Select(x => new Uri(x));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return Enumerable.Empty<Uri>();
            }
        }

        static string GetItunesId(Uri url)
        {
            if (url.Host != "itunes.apple.com" && !url.Host.EndsWith(".itunes.apple.com"))
                return null;

            var idMatch = Regex.Match(url.AbsolutePath, @"/id(\d+)$");
            if (!idMatch.Success)
                return null;
            
            return idMatch.Groups[1].Value;
        }

        async Task<bool> IsPodcastFeed(Uri url, CancellationToken cancelToken, bool skipHeadCheck=false)
        {
            if (url.Scheme != "http" && url.Scheme != "https")
                return false;
                
            try
            {
                if (!skipHeadCheck && !await IsPodcastFeedType(url, cancelToken))
                    return false;

                _log.LogTrace($"[IsPodcastFeed] GET {url}");
                using (var resp = await _client.GetAsync(url, cancelToken))
                {
                    return await IsPodcastFeed(resp, cancelToken);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error checking if podcast URL", url);
                return false;
            }
        }

        async Task<bool> IsPodcastFeedType(Uri url, CancellationToken cancelToken)
        {
            var headRequest = new HttpRequestMessage
            {
                Method = HttpMethod.Head,
                RequestUri = url,
            };
            try
            {
                _log.LogTrace($"[IsPodcastFeedType] HEAD {url}");
                using (var headResp = await _client.SendAsync(headRequest, cancelToken))
                {
                    var contentType = headResp.Content.Headers.ContentType;
                    return contentType != null && feedMimeTypes.Contains(contentType.MediaType);
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        static async Task<bool> IsPodcastFeed(HttpResponseMessage response, CancellationToken cancelToken)
        {
            if (!response.IsSuccessStatusCode)
                return false;
            
            var contentType = response.Content.Headers.ContentType;
            if (contentType == null || !feedMimeTypes.Contains(contentType.MediaType))
                return false;

            using (var body = await response.Content.ReadAsStreamAsync())
            using (var reader = new StreamReader(body))
            using (var xmlReader = new XmlCharacterFilter(reader))
            {
                var doc = await XDocument.LoadAsync(xmlReader, new LoadOptions(), cancelToken);
                if (doc.Root.Name != "rss")
                    return false;

                var enclosures = doc.XPathSelectElements("//enclosure");
                return enclosures.Any(x => x.Attribute("type")?.Value == "audio/mpeg");
            }
        }

        static JObject ParseJsonStream(Stream stream)
        {
            var serializer = new JsonSerializer();

            using (var textReader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return serializer.Deserialize(jsonReader) as JObject;
            }
        }

        static Uri TryParseUrl(Uri baseUrl, string url)
        {
            try
            {
                return new Uri(baseUrl, url);
            }
            catch
            {
                return null;
            }
        }
    }
}