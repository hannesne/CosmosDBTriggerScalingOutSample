using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DurableTask.AzureStorage;
using Microsoft.AspNetCore.Mvc;
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
            LoadGeneratorOptions options = context.GetInput<LoadGeneratorOptions>();
            
            int runCount = options.RunCount;
            for (int runNumber = 0; runNumber < runCount; runNumber++)
            {
                Task[] taskList = new Task[options.ItemsPerRunCount];
                for (int runItemNumber = 0; runItemNumber < options.ItemsPerRunCount; runItemNumber++)
                {
                    DocDBRecord docDbRecord = DocDBRecord.Create(runNumber, runItemNumber, options.RunId, context.CurrentUtcDateTime.ToShortTimeString());
                    taskList[runItemNumber] = context.CallActivityAsync(SaveDocumentFunctionName, docDbRecord);
                }

                await Task.WhenAll(taskList);
                await context.CreateTimer(context.CurrentUtcDateTime.AddSeconds(options.SleepTimeInSeconds),
                    CancellationToken.None);
            }
        }

        [FunctionName(SaveDocumentFunctionName)]
        public static async Task SaveDocument([ActivityTrigger] DocDBRecord item, ILogger log)
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
            string runId = Guid.NewGuid().ToString();
            int partitionCount = await GetCosmosDBPartitionCount();
            log.LogInformation("Triggered run for {RunId}. Cosmos DB has {PartitionCount} partitions.", runId, partitionCount);

            LoadGeneratorOptions loadGeneratorOptions = LoadGeneratorOptions.Create(partitionCount, runId, RuntimeMinutes, SleepTimeSeconds);

            await starter.StartNewAsync(LoadGeneratorFunctionName, loadGeneratorOptions);
            return req.CreateResponse(loadGeneratorOptions);
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
}