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
            MaxItemsPerInvocation = 1,
            CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> input,
            [Table("processingresults", Connection = "StorageConnectionString")] IAsyncCollector<ProcessingResult> tableOutput,
            ILogger log)
        {
            if (input == null || input.Count <= 0) return;
            foreach (Document document in input)
            {
                log.LogInformation($"Processing document with id {document.Id} from CosmosDb Change Feed.");

                ProcessorWorker.DoWork();

                await tableOutput.AddAsync(CreateProcessingResult(document, nameof(CosmosDbChangeProcessor)));
            }
            
        }

        [FunctionName("CosmosDbChangeEnqueuer")]
        public static async Task CosmosDbChangeEnqueuer([CosmosDBTrigger(
                databaseName: "%CosmosDBDatabase%",
                collectionName: "%CosmosDBCollection%",
                ConnectionStringSetting = "CosmosDBConnectionString",
                LeaseCollectionName = "CosmosDbChangeEnqueuerLeases",
                CreateLeaseCollectionIfNotExists = true)]
            IReadOnlyList<Document> input, [Queue("workqueue", Connection = "StorageConnectionString")]
            IAsyncCollector<QueueWorkItem> queueOutput, ILogger log)
        {
            if (input == null || input.Count <= 0) return;

            log.LogInformation($"Adding document with id {input.First().Id} to queue.");

            foreach (Document item in input)
            {
                dynamic workItem = JsonConvert.DeserializeObject(item.ToString());

                await queueOutput.AddAsync(new QueueWorkItem
                    {Id = workItem.id, PartitionKey = workItem.partitionkey});
            }

        }

        [FunctionName("QueueProcessor")]
        public static async Task QueueProcessor([QueueTrigger("workqueue", Connection = "StorageConnectionString")]
            QueueWorkItem myQueueItem, [CosmosDB(
                                            databaseName: "%CosmosDBDatabase%",
                                            collectionName: "%CosmosDBCollection%",
                                            ConnectionStringSetting = "CosmosDBConnectionString",
                                            Id = "{Id}", PartitionKey = "{Partitionkey}")]Document document,
            [Table("processingresults", Connection = "StorageConnectionString")] IAsyncCollector<ProcessingResult> tableOutput,
            ILogger log)
        {
            
            log.LogInformation($"Processing document with id {document.Id} from queue.");
            
            ProcessorWorker.DoWork();
            await tableOutput.AddAsync(CreateProcessingResult(document, nameof(QueueProcessor)));

        }

        private static ProcessingResult CreateProcessingResult(Document document, string cosmosDbChangeProcessorName)
        {
            DateTime processedAt = DateTime.Now;
            DateTime createdAt = document.Timestamp;

            ProcessingResult result = new ProcessingResult
            {
                RowKey = document.Id,
                ProcessingSource = cosmosDbChangeProcessorName,
                CreatedAt = createdAt,
                ProcessedAt = processedAt,
                ProcessingDuration = processedAt - createdAt,
                PartitionKey = Guid.NewGuid().ToString()
            };
            return result;
        }
    }
}
