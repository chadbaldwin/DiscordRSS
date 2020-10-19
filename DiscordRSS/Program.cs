using Microsoft.Extensions.Configuration;
using Microsoft.Toolkit.Parsers.Rss;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordRSS
{
    class Program
    {
        /*
         Misc TODO / Ideas:
            - Discord 429 error - rate limiting - check headers on discord reponses to appropriately throttle post rate
            - Specify target Discord channel per feed?
            - Change order of discord posts - instead of posting in order of feed, instead post in order of post date
         */

        static readonly HttpClient _client = new HttpClient();
        static string _DiscordApi;

        static async Task Main()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("appsettings.priv.json", optional: true, reloadOnChange: true)
                .Build();

            var config = ServiceConfiguration.FromConfiguration(configuration);
            _DiscordApi = config.DiscordAPI;

            // Using in place of persistent storage...for now
            var feeds = new List<RssFeed>();
            foreach (var feed in config.Feeds)
            {
                feeds.Add(new RssFeed { FeedUrl = feed });
            }

            while (true)
            {
                // Get all feeds that need to be processed, then loop through them
                foreach (var feed in GetStaleFeeds(feeds, config.CheckFrequencyMinutes))
                {
                    // Touch the LastCheckDate - If something fails, we'll try again in the next attempt
                    feed.LastCheckDate = DateTime.UtcNow;

                    Console.WriteLine($"Processing: {feed.FeedUrl}");

                    // Get all the posts for that feed
                    var rssItems = await GetRssItems(feed.FeedUrl, feed.LastPublishDate);

                    foreach (var item in rssItems)
                    {
                        try
                        {
                            Console.WriteLine("Posting to discord");
                            var message = GetMessageFromTemplate(config.MessageTemplate, item);
                            await PostDiscordMessage(message);
                        }
                        catch (Exception ex)
                        {
                            feed.LastErrorDate = DateTime.UtcNow;
                            feed.LastError = ex;
                            break;
                        }

                        feed.LastPublishDate = item.PublishDate;
                    }
                }
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }

        static string GetMessageFromTemplate(string Template, RssSchema post)
        {
            return Template.Replace("{{Author}}", post.Author, StringComparison.OrdinalIgnoreCase)
                           .Replace("{{Title}}", post.Title, StringComparison.OrdinalIgnoreCase)
                           .Replace("{{Summary}}", post.Summary, StringComparison.OrdinalIgnoreCase)
                           .Replace("{{FeedUrl}}", post.FeedUrl, StringComparison.OrdinalIgnoreCase)
                           .Replace("{{ImageUrl}}", post.ImageUrl, StringComparison.OrdinalIgnoreCase)
                           .Replace("{{MediaUrl}}", post.MediaUrl, StringComparison.OrdinalIgnoreCase)
                           .Replace("{{ExtraImageUrl}}", post.ExtraImageUrl, StringComparison.OrdinalIgnoreCase)
                           .Replace("{{PubDate}}", post.PublishDate.ToString("f"), StringComparison.OrdinalIgnoreCase);
        }

        static List<RssFeed> GetStaleFeeds(List<RssFeed> feeds, int minutesOld)
        {
            return feeds.Where(f => f.LastCheckDate < DateTime.UtcNow.AddMinutes(-minutesOld)).ToList();
        }

        static async Task<IOrderedEnumerable<RssSchema>> GetRssItems(string feedUri, DateTime? lookBackDate = null)
        {
            var parser = new RssParser();

            using var response = await _client.GetAsync(feedUri);

            var content = await response.Content.ReadAsStringAsync();
            response.EnsureSuccessStatusCode();

            var posts = parser.Parse(content);

            return posts
                    .Where(x => lookBackDate == null || x.PublishDate > lookBackDate)
                    .OrderBy(x => x.PublishDate);
        }

        static async Task PostDiscordMessage(string message)
        {
            var body = JsonSerializer.Serialize(new DiscordPost() { content = message });

            using var payload = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _client.PostAsync(_DiscordApi, payload);

            response.EnsureSuccessStatusCode();

            Thread.Sleep(1000); // Poor mans rate limiter
        }
    }
}