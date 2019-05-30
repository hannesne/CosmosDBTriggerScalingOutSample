using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CosmosDBTriggerScalingSample;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

internal static class DocumentProcessor
{
    public static readonly TimeSpan ProcessingTime =
        TimeSpan.FromSeconds(Int32.Parse(Environment.GetEnvironmentVariable("ItemProcessingDurationSeconds")));

    public static readonly bool UseCPUBoundProcessing =
        Boolean.Parse(Environment.GetEnvironmentVariable("UseCPUBoundProcessing"));

    private static async Task DoWork()
    {

        if (!UseCPUBoundProcessing)
            await Task.Delay(ProcessingTime);
        else
            await Task.Run(DoCPUWork);
    }

    private static void DoCPUWork()
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();
        do
        {
            Thread.SpinWait(100000);
        } while (sw.Elapsed < ProcessingTime);

        sw.Stop();
    }

    public static DocDBRecord DeserializeDocument(Document doc)
    {
        return JsonConvert.DeserializeObject<DocDBRecord>(doc.ToString());
    }

    public static async Task ProcessDocument(DocDBRecord document, IAsyncCollector<ProcessingResult> tableOutput, ILogger log, string processorName)
    {
        await DoWork();
        await tableOutput.AddAsync(CreateProcessingResult(document, processorName, log));
    }

    public static async Task ProcessDocument(Document document, IAsyncCollector<ProcessingResult> tableOutput, ILogger log, string cosmosDbChangeProcessorName)
    {
        await ProcessDocument(DeserializeDocument(document), tableOutput, log, cosmosDbChangeProcessorName);
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