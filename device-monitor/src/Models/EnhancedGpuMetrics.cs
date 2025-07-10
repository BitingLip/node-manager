using System;

namespace DeviceMonitorApp.Models
{
    public class EnhancedGpuMetrics
    {
        public string GpuName { get; set; }
        public float UtilizationPercentage { get; set; }
        public long MemoryUsage { get; set; }
        public long MemoryTotal { get; set; }
        public float Temperature { get; set; }
        public float FanSpeed { get; set; }
        public float PowerUsage { get; set; }
        public float CoreVoltage { get; set; }
        public float MemoryUtilization { get; set; }
        public float CoreClock { get; set; }
        public float MemoryClock { get; set; }

        public EnhancedGpuMetrics()
        {
            GpuName = string.Empty;
            UtilizationPercentage = 0;
            MemoryUsage = 0;
            MemoryTotal = 0;
            Temperature = 0;
            FanSpeed = 0;
            PowerUsage = 0;
            CoreVoltage = 0;
            MemoryUtilization = 0;
            CoreClock = 0;
            MemoryClock = 0;
        }

        public EnhancedGpuMetrics(string gpuName, float utilizationPercentage, long memoryUsage, long memoryTotal, 
                                float temperature, float fanSpeed, float powerUsage, float coreVoltage, float memoryUtilization,
                                float coreClock, float memoryClock)
        {
            GpuName = gpuName;
            UtilizationPercentage = utilizationPercentage;
            MemoryUsage = memoryUsage;
            MemoryTotal = memoryTotal;
            Temperature = temperature;
            FanSpeed = fanSpeed;
            PowerUsage = powerUsage;
            CoreVoltage = coreVoltage;
            MemoryUtilization = memoryUtilization;
            CoreClock = coreClock;
            MemoryClock = memoryClock;
        }

        public override string ToString()
        {
            return $"GPU: {GpuName}, " +
                   $"Utilization: {UtilizationPercentage:F1}%, " +
                   $"Memory: {MemoryUsage / (1024 * 1024):F0}/{MemoryTotal / (1024 * 1024):F0} MB ({MemoryUtilization:F1}%), " +
                   $"Temperature: {Temperature:F1}°C, " +
                   $"Fan: {FanSpeed:F0} RPM, " +
                   $"Power: {PowerUsage:F1}W, " +
                   $"Voltage: {CoreVoltage:F3}V, " +
                   $"Core: {CoreClock:F0} MHz, " +
                   $"Memory: {MemoryClock:F0} MHz";
        }
    }
}
