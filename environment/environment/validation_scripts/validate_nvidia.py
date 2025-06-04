"""
NVIDIA CUDA Validation Script
Validates NVIDIA GPU setup and CUDA functionality
"""

import sys
import torch
import traceback


def validate_nvidia_setup():
    """Comprehensive NVIDIA CUDA validation"""
    results = {
        "overall_success": False,
        "checks": {},
        "errors": [],
        "warnings": [],
        "info": []
    }
    
    try:
        # Check 1: PyTorch CUDA availability
        results["checks"]["pytorch_cuda_available"] = torch.cuda.is_available()
        if torch.cuda.is_available():
            results["info"].append("✅ PyTorch CUDA is available")
        else:
            results["errors"].append("❌ PyTorch CUDA is not available")
            return results
        
        # Check 2: CUDA device count
        device_count = torch.cuda.device_count()
        results["checks"]["cuda_device_count"] = device_count
        results["info"].append(f"✅ Found {device_count} CUDA device(s)")
        
        if device_count == 0:
            results["errors"].append("❌ No CUDA devices found")
            return results
        
        # Check 3: CUDA device properties
        for i in range(device_count):
            device = torch.cuda.get_device_properties(i)
            device_info = {
                "name": device.name,
                "memory_mb": device.total_memory // (1024 * 1024),
                "compute_capability": f"{device.major}.{device.minor}",
                "multiprocessor_count": device.multi_processor_count
            }
            results["checks"][f"device_{i}"] = device_info
            results["info"].append(f"✅ Device {i}: {device.name} ({device_info['memory_mb']}MB)")
        
        # Check 4: CUDA version
        cuda_version = torch.version.cuda
        results["checks"]["cuda_version"] = cuda_version
        results["info"].append(f"✅ CUDA version: {cuda_version}")
        
        # Check 5: Basic tensor operations
        try:
            device = torch.device('cuda:0')
            test_tensor = torch.randn(100, 100, device=device)
            result_tensor = torch.matmul(test_tensor, test_tensor.t())
            cpu_result = result_tensor.cpu()
            
            results["checks"]["tensor_operations"] = True
            results["info"].append("✅ Basic CUDA tensor operations working")
            
        except Exception as e:
            results["checks"]["tensor_operations"] = False
            results["errors"].append(f"❌ CUDA tensor operations failed: {str(e)}")
        
        # Check 6: CuDNN availability
        try:
            cudnn_available = torch.backends.cudnn.enabled
            results["checks"]["cudnn_enabled"] = cudnn_available
            if cudnn_available:
                results["info"].append("✅ CuDNN is enabled")
            else:
                results["warnings"].append("⚠️ CuDNN is disabled")
        except Exception as e:
            results["warnings"].append(f"⚠️ Could not check CuDNN status: {str(e)}")
        
        # Check 7: Memory allocation test
        try:
            # Try to allocate 100MB
            test_mem = torch.cuda.FloatTensor(100 * 1024 * 1024 // 4).fill_(1.0)
            del test_mem
            torch.cuda.empty_cache()
            
            results["checks"]["memory_allocation"] = True
            results["info"].append("✅ GPU memory allocation test passed")
            
        except Exception as e:
            results["checks"]["memory_allocation"] = False
            results["errors"].append(f"❌ GPU memory allocation failed: {str(e)}")
        
        # Determine overall success
        critical_checks = [
            "pytorch_cuda_available",
            "tensor_operations",
            "memory_allocation"
        ]
        
        results["overall_success"] = all(
            results["checks"].get(check, False) for check in critical_checks
        )
        
        if results["overall_success"]:
            results["info"].append("🎉 All NVIDIA CUDA validation checks passed!")
        else:
            results["errors"].append("💥 Some critical NVIDIA CUDA validation checks failed")
        
    except Exception as e:
        results["errors"].append(f"💥 Validation script exception: {str(e)}")
        results["errors"].append(f"Traceback: {traceback.format_exc()}")
    
    return results


def print_validation_results(results):
    """Print validation results in a formatted way"""
    print("=" * 60)
    print("🔍 NVIDIA CUDA Validation Results")
    print("=" * 60)
    
    # Print info messages
    if results["info"]:
        print("\n📋 Validation Info:")
        for info in results["info"]:
            print(f"  {info}")
    
    # Print warnings
    if results["warnings"]:
        print("\n⚠️ Warnings:")
        for warning in results["warnings"]:
            print(f"  {warning}")
    
    # Print errors
    if results["errors"]:
        print("\n❌ Errors:")
        for error in results["errors"]:
            print(f"  {error}")
    
    # Print overall status
    print(f"\n🎯 Overall Status: {'✅ PASS' if results['overall_success'] else '❌ FAIL'}")
    print("=" * 60)


if __name__ == "__main__":
    print("🚀 Starting NVIDIA CUDA validation...")
    
    # Run validation
    validation_results = validate_nvidia_setup()
    
    # Print results
    print_validation_results(validation_results)
    
    # Exit with appropriate code
    sys.exit(0 if validation_results["overall_success"] else 1)
