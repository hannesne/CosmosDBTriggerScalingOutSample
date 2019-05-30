using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace CosmosDBTriggerScalingSample
{
    public static class QueueProcessorFunctions
    {
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

            foreach (DocDBRecord item in input.Select(DocumentProcessor.DeserializeDocument))
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
            await DocumentProcessor.ProcessDocument(document, tableOutput, log, nameof(QueueProcessor));
        }
    }
}