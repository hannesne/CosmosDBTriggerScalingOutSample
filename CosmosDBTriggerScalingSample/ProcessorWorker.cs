using System;
using System.Diagnostics;
using System.Threading;
using CosmosDBTriggerScalingSample;

internal static class ProcessorWorker
{
    public static readonly TimeSpan ProcessingTime =
        TimeSpan.FromSeconds(int.Parse(Environment.GetEnvironmentVariable("ItemProcessingDurationSeconds")));

    public static readonly bool UseCPUBoundProcessing =
        bool.Parse(Environment.GetEnvironmentVariable("UseCPUBoundProcessing"));

    public static void DoWork()
    {
        if (!UseCPUBoundProcessing)
            Thread.Sleep(ProcessingTime);
        else
            DoCPUWork();
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
}