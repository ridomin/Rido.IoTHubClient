using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Rido.IoTHubClient
{
    public class TopicParser
    {
        public static (int rid, int twinVersion) ParseTopic(string topic)
        {
            var segments = topic.Split('/');
            int twinVersion = -1;
            int rid = -1;
            if (topic.Contains('?'))
            {
                // parse qs to extract the rid
                var qs = HttpUtility.ParseQueryString(segments[^1]);
                int.TryParse(qs["$rid"], out rid);
                twinVersion = Convert.ToInt32(qs["$version"]);
            }
            return (rid, twinVersion);
        }
    }
}
