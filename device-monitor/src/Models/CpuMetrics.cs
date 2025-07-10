namespace DeviceMonitorApp.Models
{
    public class CpuMetrics
    {
        public string Name { get; set; } = string.Empty;
        public float Usage { get; set; }
        public float Temperature { get; set; }
        public float PowerConsumption { get; set; }
        public int CoreCount { get; set; }

        public override string ToString()
        {
            return $"CPU: {Name}, Usage: {Usage:F1}%, Temperature: {Temperature:F1}°C, Power: {PowerConsumption:F1}W, Cores: {CoreCount}";
        }
    }
}
