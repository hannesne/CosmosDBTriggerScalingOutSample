namespace CosmosDBTriggerScalingSample
{
    public class QueueWorkItem
    {
        public string Id { get; set; }
        public string PartitionKey { get; set; }
    }
}