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

        public static DocDBRecord Create(int runNumber, int runItemNumber, string runId, string createdAt)
        {
            string docDbRecordId = $"{createdAt}_{runNumber}_{runItemNumber}_{runId}";
            return new DocDBRecord()
            {
                Id = docDbRecordId,
                RunNumber = runNumber,
                RunItemNumber = runItemNumber,
                RunId = runId,
                PartitionKey = Guid.NewGuid().ToString()
            };
        }
    }
}