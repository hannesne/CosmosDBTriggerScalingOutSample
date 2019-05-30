namespace CosmosDBTriggerScalingSample
{
    public class LoadGeneratorOptions
    {
        public int RunCount => (60 / SleepTimeInSeconds) * RuntimeMinutes;
        public int RuntimeMinutes { get; set; }

        public int ItemsPerRunCount => PartitionCount * 2;
        public int PartitionCount { get; set; }

        public int SleepTimeInSeconds { get; set; }
        public string RunId { get; set; }

        public bool UseCPUBoundProcessing => DocumentProcessor.UseCPUBoundProcessing;
        public int ProcessingTimeSeconds => (int) DocumentProcessor.ProcessingTime.TotalSeconds;

        public static LoadGeneratorOptions Create(int partitionCount, string runId, int runtimeMinutes, int sleepTimeInSeconds)
        {
            LoadGeneratorOptions loadGeneratorOptions = new LoadGeneratorOptions
            {
                PartitionCount = partitionCount,
                RuntimeMinutes = runtimeMinutes,
                SleepTimeInSeconds = sleepTimeInSeconds,
                RunId = runId
            };
            return loadGeneratorOptions;
        }
    }
}