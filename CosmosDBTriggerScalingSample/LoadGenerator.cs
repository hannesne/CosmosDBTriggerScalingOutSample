using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace CosmosDBTriggerScalingSample
{
    public static class LoadGenerator
    {
        private const string SaveDocumentFunctionName = "LoadGenerator_SaveDocument";
        private const string LoadGeneratorFunctionName = "LoadGenerator";
        private static readonly CosmosDBConnectionString ConnectionString = new CosmosDBConnectionString(Environment.GetEnvironmentVariable("CosmosDBConnectionString"));
        private static readonly string DatabaseId = Environment.GetEnvironmentVariable("CosmosDBDatabase");
        private static readonly string CollectionId = Environment.GetEnvironmentVariable("CosmosDBCollection");
        private static readonly int RuntimeMinutes = int.Parse(Environment.GetEnvironmentVariable("LoadGeneratorDurationMinutes"));
        private static readonly int SleepTimeSeconds = int.Parse(Environment.GetEnvironmentVariable("LoadGenerationCycleSleepDurationSeconds"));

        [FunctionName(LoadGeneratorFunctionName)]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            GeneratorOptions options = context.GetInput<GeneratorOptions>();
            
            int runCount = options.RunCount;
            for (int runNumber = 0; runNumber < runCount; runNumber++)
            {
                Task[] taskList = new Task[options.ItemsPerRunCount];
                for (int runItemNumber = 0; runItemNumber < options.ItemsPerRunCount; runItemNumber++)
                {
                    taskList[runItemNumber] = context.CallActivityAsync(SaveDocumentFunctionName,
                        CreateDocDBRecord(context, runNumber, runItemNumber, options));
                }

                await Task.WhenAll(taskList);
                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(options.SleepTimeInSeconds),
                    CancellationToken.None);
            }
        }

        private static DocDBRecord CreateDocDBRecord(DurableOrchestrationContext context, int runNumber, int runItemNumber,
            GeneratorOptions options)
        {
            string docDbRecordId = $"{context.CurrentUtcDateTime.ToShortTimeString()}_{runNumber}_{runItemNumber}_{options.RunId}";
            return new DocDBRecord()
            {
                Id = docDbRecordId,
                RunNumber = runNumber,
                RunItemNumber = runItemNumber,
                RunId = options.RunId,
                PartitionKey = Guid.NewGuid().ToString()
            };
        }

        [FunctionName(SaveDocumentFunctionName)]
        public static async Task SaveDocument([ActivityTrigger] DocDBRecord item, string instanceId, ILogger log)
        {
            using (DocumentClient client = new DocumentClient(ConnectionString.ServiceEndpoint, ConnectionString.AuthKey))
            {
                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId);
                
                await client.CreateDocumentAsync(collectionUri, item);

                log.LogInformation("Create record with id {ItemId} for run {RunId}", item.Id, item.RunId);
            }
        }

        [FunctionName("LoadGenerator_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            Guid runId = Guid.NewGuid();
            int partitionCount = await GetCosmosDBPartitionCount();
            log.LogInformation("Triggered run for {RunId}. Cosmos DB has {PartitionCount} partitions.", runId, partitionCount);
            GeneratorOptions generatorOptions = new GeneratorOptions
            {
                RunCount = (60 / SleepTimeSeconds) * RuntimeMinutes,
                ItemsPerRunCount = partitionCount * 2,
                SleepTimeInSeconds = SleepTimeSeconds,
                RunId = runId.ToString()
            };
            string instanceId = await starter.StartNewAsync(LoadGeneratorFunctionName, generatorOptions);
            
            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        private static async Task<int> GetCosmosDBPartitionCount()
        {
            
            int rangeCount = 0;
            using (DocumentClient client =
                new DocumentClient(ConnectionString.ServiceEndpoint, ConnectionString.AuthKey))
            {
                Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId);

                FeedResponse<PartitionKeyRange> response;
                do
                {
                    response = await client.ReadPartitionKeyRangeFeedAsync(collectionUri);
                    rangeCount += response.Count;

                } while (!string.IsNullOrEmpty(response.ResponseContinuation));

            }

            return rangeCount;
        }
    }

    public class GeneratorOptions
    {
        public int RunCount { get; set; }
        public int ItemsPerRunCount { get; set; }
        public int SleepTimeInSeconds { get; set; }
        public string RunId { get; set; }
    }
}