using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

internal static class ProcessorWorker
{
    public static readonly TimeSpan ProcessingTime =
        TimeSpan.FromSeconds(int.Parse(Environment.GetEnvironmentVariable("ItemProcessingDurationSeconds")));

    public static readonly bool UseCPUBoundProcessing =
        bool.Parse(Environment.GetEnvironmentVariable("UseCPUBoundProcessing"));

    public static async Task DoWork()
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
}