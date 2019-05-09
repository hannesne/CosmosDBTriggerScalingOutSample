using System;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;

namespace CosmosDBTriggerScalingSample
{
    public class DocDBRecord : Document
    {
        [JsonProperty("runnumber")]
        public int RunNumber;
        
        [JsonProperty("runitemnumber")]
        public int RunItemNumber;

        [JsonProperty("runid")]
        public string RunId;

        [JsonProperty("partitionkey")]
        public string PartitionKey;
    }
}