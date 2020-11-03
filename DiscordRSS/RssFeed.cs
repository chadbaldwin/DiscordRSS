using System;

namespace DiscordRSS
{
    class RssFeed
    {
        public string FeedUrl { get; set; }
        public DateTime LastCheckDate { get; set; } = DateTime.UtcNow;
        public DateTime LastPublishDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastErrorDate { get; set; }
        public Exception LastError { get; set; }
    }
}