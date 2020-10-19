using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace DiscordRSS
{
    class ServiceConfiguration
    {
        public List<string> Feeds { get; set; }
        public string DiscordAPI { get; set; }
        public int CheckFrequencyMinutes { get; set; }
        public string MessageTemplate { get; set; }

        public static ServiceConfiguration FromConfiguration(IConfiguration config)
        {
            ServiceConfiguration serviceConfig = new ServiceConfiguration();
            config.Bind(serviceConfig);

            return serviceConfig;
        }
    }
}