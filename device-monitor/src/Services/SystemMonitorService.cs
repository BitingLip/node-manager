using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using DeviceMonitorApp.Models;

namespace DeviceMonitorApp.Services
{
    public class SystemMonitorService : IDisposable
    {
        private readonly Computer _computer;
        private bool _isInitialized;

        public SystemMonitorService()
        {
            _computer = new Computer
            {
                IsGpuEnabled = true,        // GPU temperature, power, clocks
                IsCpuEnabled = true,        // CPU usage, temperature
                IsMemoryEnabled = true,     // RAM usage
                IsMotherboardEnabled = true, // System temperatures
                IsControllerEnabled = true,  // Storage controllers
                IsNetworkEnabled = true,     // Network adapters
                IsStorageEnabled = true      // Storage devices
            };
        }

        public void Initialize()
        {
            try
            {
                _computer.Open();
                
                // Update all hardware components
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    foreach (var subHardware in hardware.SubHardware)
                    {
                        subHardware.Update();
                    }
                }

                _isInitialized = true;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public List<EnhancedGpuMetrics> GetGpuMetrics()
        {
            if (!_isInitialized)
            {
                return new List<EnhancedGpuMetrics>();
            }

            try
            {
                var gpuMetricsList = new List<EnhancedGpuMetrics>();
                
                // Update all hardware components
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    foreach (var subHardware in hardware.SubHardware)
                    {
                        subHardware.Update();
                    }
                }

                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.GpuAmd || 
                        hardware.HardwareType == HardwareType.GpuNvidia)
                    {
                        var gpuMetrics = new EnhancedGpuMetrics(
                            hardware.Name,
                            0, 0, 0, 0, 0, 0, 0, 0, 0, 0
                        );

                        // AMD GPU sensors based on LibreHardwareMonitor source analysis
                        var coreLoadSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == "D3D 3D");
                        var memoryUsedSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name == "D3D Dedicated Memory Used");
                        var memoryFreeSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name == "D3D Dedicated Memory Free");
                        var memoryTotalSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name == "D3D Dedicated Memory Total");
                        var sharedMemoryUsedSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name == "D3D Shared Memory Used");
                        var temperatureCoreSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name == "GPU Core");
                        var temperatureHotSpotSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name == "GPU Hot Spot");
                        var fanRpmSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Fan && s.Name == "GPU Fan");
                        var powerCoreSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == "GPU Core");
                        var powerPackageSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == "GPU Package");
                        var coreClockSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name == "GPU Core");
                        var memoryClockSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name == "GPU Memory");
                        var coreVoltageSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage && s.Name == "GPU Core");
                        
                        // Apply sensor values to metrics
                        if (coreLoadSensor?.Value.HasValue == true)
                            gpuMetrics.UtilizationPercentage = (float)coreLoadSensor.Value.Value;
                        
                        // Memory data from D3D sensors
                        if (memoryUsedSensor?.Value.HasValue == true)
                            gpuMetrics.MemoryUsage = (long)(memoryUsedSensor.Value.Value * 1024 * 1024); // Convert MB to bytes
                        
                        if (memoryTotalSensor?.Value.HasValue == true)
                        {
                            gpuMetrics.MemoryTotal = (long)(memoryTotalSensor.Value.Value * 1024 * 1024); // Convert MB to bytes
                            // Calculate memory utilization percentage
                            if (memoryUsedSensor?.Value.HasValue == true)
                            {
                                gpuMetrics.MemoryUtilization = 
                                    ((float)memoryUsedSensor.Value.Value / (float)memoryTotalSensor.Value.Value) * 100f;
                            }
                        }
                        
                        // Enhanced metrics from ADL sensors (if available)
                        if (temperatureCoreSensor?.Value.HasValue == true)
                            gpuMetrics.Temperature = (float)temperatureCoreSensor.Value.Value;
                        
                        if (fanRpmSensor?.Value.HasValue == true)
                            gpuMetrics.FanSpeed = (float)fanRpmSensor.Value.Value;
                        
                        if (powerCoreSensor?.Value.HasValue == true)
                            gpuMetrics.PowerUsage = (float)powerCoreSensor.Value.Value;
                        else if (powerPackageSensor?.Value.HasValue == true)
                            gpuMetrics.PowerUsage = (float)powerPackageSensor.Value.Value;
                        
                        if (coreVoltageSensor?.Value.HasValue == true)
                            gpuMetrics.CoreVoltage = (float)coreVoltageSensor.Value.Value;
                        
                        if (coreClockSensor?.Value.HasValue == true)
                            gpuMetrics.CoreClock = (float)coreClockSensor.Value.Value;
                        
                        if (memoryClockSensor?.Value.HasValue == true)
                            gpuMetrics.MemoryClock = (float)memoryClockSensor.Value.Value;

                        gpuMetricsList.Add(gpuMetrics);
                    }
                }

                return gpuMetricsList;
            }
            catch (Exception)
            {
                return new List<EnhancedGpuMetrics>();
            }
        }

        public CpuMetrics GetCpuMetrics()
        {
            if (!_isInitialized)
                return new CpuMetrics { Name = "Not Initialized" };

            try
            {
                // Update all hardware components
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    foreach (var subHardware in hardware.SubHardware)
                    {
                        subHardware.Update();
                    }
                }

                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Cpu)
                    {
                        // Enhanced CPU sensor detection for LibreHardwareMonitor 0.9.4
                        var totalLoadSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == "CPU Total");
                        var coreMaxTempSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name == "Core Max");
                        var coreAvgTempSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name == "Core Average");
                        var packagePowerSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == "CPU Package");
                        var coresPowerSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == "CPU Cores");

                        int coreCount = hardware.Sensors.Count(s => s.SensorType == SensorType.Load && s.Name.Contains("CPU Core") && s.Name.Contains("Thread"));
                        
                        return new CpuMetrics
                        {
                            Name = hardware.Name,
                            Usage = totalLoadSensor?.Value ?? 0,
                            Temperature = coreMaxTempSensor?.Value ?? 0,
                            PowerConsumption = packagePowerSensor?.Value ?? 0,
                            CoreCount = coreCount / 2 // Each core has 2 threads
                        };
                    }
                }
            }
            catch (Exception)
            {
                // Silent operation
            }

            return new CpuMetrics { Name = "No CPU found" };
        }

        public MemoryMetrics GetMemoryMetrics()
        {
            if (!_isInitialized)
                return new MemoryMetrics { TotalGB = 0, UsedGB = 0, AvailableGB = 0 };

            try
            {
                // Update all hardware components
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    foreach (var subHardware in hardware.SubHardware)
                    {
                        subHardware.Update();
                    }
                }

                foreach (var hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.Memory)
                    {
                        var usedMemorySensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name == "Memory Used");
                        var availableMemorySensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name == "Memory Available");
                        var memoryLoadSensor = hardware.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name == "Memory");

                        float usedGB = usedMemorySensor?.Value ?? 0;
                        float availableGB = availableMemorySensor?.Value ?? 0;
                        float totalGB = usedGB + availableGB;

                        return new MemoryMetrics
                        {
                            TotalGB = totalGB,
                            UsedGB = usedGB,
                            AvailableGB = availableGB,
                            UsagePercentage = totalGB > 0 ? (usedGB / totalGB) * 100f : 0
                        };
                    }
                }
            }
            catch (Exception)
            {
                // Silent operation
            }

            return new MemoryMetrics { TotalGB = 0, UsedGB = 0, AvailableGB = 0 };
        }

        private string GetSensorUnit(SensorType sensorType)
        {
            return sensorType switch
            {
                SensorType.Temperature => "°C",
                SensorType.Load => "%",
                SensorType.Clock => "MHz",
                SensorType.Voltage => "V",
                SensorType.Current => "A",
                SensorType.Power => "W",
                SensorType.Data => "MB",
                SensorType.SmallData => "MB",
                SensorType.Fan => "RPM",
                SensorType.Flow => "L/h",
                SensorType.Control => "%",
                SensorType.Level => "%",
                SensorType.Factor => "",
                _ => ""
            };
        }

        public void Dispose()
        {
            try
            {
                _computer?.Close();
            }
            catch (Exception)
            {
                // Silent operation
            }
        }
    }
}
