using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CosmosDBTriggerScalingSample
{
    public static class ChangeProcessor
    {

        [FunctionName("CosmosDBChangeProcessor")]
        public static async Task CosmosDbChangeProcessor([CosmosDBTrigger(
            databaseName: "%CosmosDBDatabase%",
            collectionName: "%CosmosDBCollection%",
            ConnectionStringSetting = "CosmosDBConnectionString",
            LeaseCollectionName = "CosmosDBChangeProcessorLeases",
            MaxItemsPerInvocation = 9, /*we need to set this to prevent timeouts when too many records are grabbed. Calculated using (functiontimeoutduration * 60 / ItemProcessingDurationSeconds) -1 for overhead*/
            CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> input,
            [Table("%ProcessingResultsTable%", Connection = "StorageConnectionString")] IAsyncCollector<ProcessingResult> tableOutput,
            ILogger log)
        {
            if (input == null || input.Count <= 0) return;
            foreach (DocDBRecord document in input.Select(DeserializeDocument))
            {
                await ProcessorWorker.DoWork();

                await tableOutput.AddAsync(CreateProcessingResult(document, nameof(CosmosDbChangeProcessor), log));
            }
        }

        private static DocDBRecord DeserializeDocument(Document doc)
        {
            return JsonConvert.DeserializeObject<DocDBRecord>(doc.ToString());
        }

        [FunctionName("CosmosDbChangeEnqueuer")]
        public static async Task CosmosDbChangeEnqueuer([CosmosDBTrigger(
                databaseName: "%CosmosDBDatabase%",
                collectionName: "%CosmosDBCollection%",
                ConnectionStringSetting = "CosmosDBConnectionString",
                LeaseCollectionName = "CosmosDbChangeEnqueuerLeases",
                CreateLeaseCollectionIfNotExists = true)]
            IReadOnlyList<Document> input, [Queue("%QueueName%", Connection = "StorageConnectionString")]
            IAsyncCollector<QueueWorkItem> queueOutput, ILogger log)
        {
            if (input == null || input.Count <= 0) return;

            log.LogInformation("Adding document with id {DocumentId} to queue.", input.First().Id);

            foreach (DocDBRecord item in input.Select(DeserializeDocument))
            {
                await queueOutput.AddAsync(new QueueWorkItem
                    {Id = item.Id, PartitionKey = item.PartitionKey});
            }

        }

        [FunctionName("QueueProcessor")]
        public static async Task QueueProcessor([QueueTrigger("%QueueName%", Connection = "StorageConnectionString")]
            QueueWorkItem myQueueItem, [CosmosDB(
                                            databaseName: "%CosmosDBDatabase%",
                                            collectionName: "%CosmosDBCollection%",
                                            ConnectionStringSetting = "CosmosDBConnectionString",
                                            Id = "{Id}", PartitionKey = "{Partitionkey}")]DocDBRecord document,
            [Table("%ProcessingResultsTable%", Connection = "StorageConnectionString")] IAsyncCollector<ProcessingResult> tableOutput,
            ILogger log)
        {
            await ProcessorWorker.DoWork();
            await tableOutput.AddAsync(CreateProcessingResult(document, nameof(QueueProcessor), log));

        }

        private static ProcessingResult CreateProcessingResult(DocDBRecord document, string changeSource, ILogger log)
        {
            DateTime processedAt = DateTime.Now;
            DateTime createdAt = document.Timestamp;

            TimeSpan processingDuration = processedAt.ToUniversalTime() - createdAt.ToUniversalTime();
            ProcessingResult result = new ProcessingResult
            {
                RowKey = document.Id,
                ProcessingSource = changeSource,
                CreatedAt = createdAt,
                ProcessedAt = processedAt,
                ProcessingDuration = processingDuration,
                PartitionKey = Guid.NewGuid().ToString(),
                RunId = document.RunId
            };
            log.LogInformation("Processed document with id {DocumentId} for run {RunId} from {ChangeSource}. Duration: {ProcessingDuration}", document.Id, document.RunId, changeSource, processingDuration);
            return result;
        }
    }
}
