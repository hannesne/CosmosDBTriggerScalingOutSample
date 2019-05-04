using System;

namespace CosmosDBTriggerScalingSample
{
    public class ProcessingResult
    {
        public string RowKey { get; set; }
        public string ProcessingSource { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string PartitionKey { get; set; }
        public TimeSpan ProcessingDuration { get; set; }
    }
}