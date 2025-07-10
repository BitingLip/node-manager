using System;

namespace DeviceMonitorApp.Models
{
    public class DeviceRecord
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceVendor { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Status { get; set; } = "online";
        public string? StatusMessage { get; set; }
        public int MemoryCapacity { get; set; } // MB
        public float MemoryUsage { get; set; } // MB
        public float ProcessingUsage { get; set; } // Percentage
        public DateTime TimeCreated { get; set; }
        public DateTime TimeUpdated { get; set; }

        public DeviceRecord()
        {
            TimeCreated = DateTime.UtcNow;
            TimeUpdated = DateTime.UtcNow;
        }

        public static DeviceRecord FromCpuMetrics(CpuMetrics cpu, MemoryMetrics memory)
        {
            return new DeviceRecord
            {
                DeviceId = "cpu_0",
                DeviceVendor = ExtractVendor(cpu.Name),
                DeviceName = cpu.Name,
                Status = "online",
                StatusMessage = null,
                MemoryCapacity = (int)(memory.TotalGB * 1024), // Convert GB to MB
                MemoryUsage = memory.UsedGB * 1024, // Convert GB to MB
                ProcessingUsage = cpu.Usage,
                TimeUpdated = DateTime.UtcNow
            };
        }

        public static DeviceRecord FromGpuMetrics(EnhancedGpuMetrics gpu, int gpuIndex)
        {
            return new DeviceRecord
            {
                DeviceId = $"gpu_{gpuIndex}",
                DeviceVendor = ExtractVendor(gpu.GpuName),
                DeviceName = ExtractModelName(gpu.GpuName),
                Status = "online",
                StatusMessage = null,
                MemoryCapacity = (int)(gpu.MemoryTotal / (1024 * 1024)), // Convert bytes to MB
                MemoryUsage = gpu.MemoryUsage / (1024 * 1024), // Convert bytes to MB
                ProcessingUsage = gpu.UtilizationPercentage,
                TimeUpdated = DateTime.UtcNow
            };
        }

        private static string ExtractVendor(string deviceName)
        {
            if (deviceName.ToUpper().Contains("AMD"))
                return "AMD";
            if (deviceName.ToUpper().Contains("INTEL"))
                return "INTEL";
            if (deviceName.ToUpper().Contains("NVIDIA"))
                return "NVIDIA";
            
            return "UNKNOWN";
        }

        private static string ExtractModelName(string fullName)
        {
            // Extract model name from full GPU name
            // "AMD Radeon RX 6800 XT" -> "RX 6800 XT"
            if (fullName.ToUpper().Contains("AMD RADEON"))
                return fullName.Replace("AMD Radeon ", "").Trim();
            if (fullName.ToUpper().Contains("NVIDIA"))
                return fullName.Replace("NVIDIA ", "").Trim();
            
            return fullName.Trim();
        }

        public override string ToString()
        {
            return $"{DeviceId}: {DeviceVendor} {DeviceName} - {ProcessingUsage:F1}% load, {MemoryUsage:F0}/{MemoryCapacity} MB memory";
        }
    }
}
