namespace DeviceMonitorApp.Models
{
    public class MemoryMetrics
    {
        public float TotalGB { get; set; }
        public float UsedGB { get; set; }
        public float AvailableGB { get; set; }
        public float UsagePercentage { get; set; }

        public override string ToString()
        {
            return $"Memory: {UsedGB:F1}GB / {TotalGB:F1}GB ({UsagePercentage:F1}%)";
        }
    }
}
