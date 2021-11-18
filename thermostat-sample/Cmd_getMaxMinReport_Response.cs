using System.Text.Json.Serialization;

namespace com_example
{
    public class Cmd_getMaxMinReport_Response
    {
        [JsonIgnore]
        public int _status { get; set; }
        [JsonIgnore]
        public int _rid { get; set; }

        public double maxTemp { get; set; }
        public double minTemp { get; set; }
        public double avgTemp { get; set; }
        public DateTimeOffset startTime { get; set; }
        public DateTimeOffset endTime { get; set; }
    }
}