using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CosmosDBTriggerScalingSample
{
    public static class CosmosDBChangeProcessorFunctions
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

            await input.ParallelForEachAsync( async doc =>
            {
                await DocumentProcessor.ProcessDocument(doc, tableOutput, log, nameof(CosmosDbChangeProcessor));
            });

        }
    }
}
