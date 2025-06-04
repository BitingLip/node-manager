"""
ROCm Validation Script  
Validates AMD ROCm setup for RDNA3/RDNA4 GPUs on Linux/WSL
"""

import sys
import traceback


def validate_rocm_setup():
    """Comprehensive ROCm validation for AMD GPUs"""
    results = {
        "overall_success": False,
        "checks": {},
        "errors": [],
        "warnings": [],
        "info": []
    }
    
    try:
        # Check 1: PyTorch availability
        try:
            import torch
            results["checks"]["pytorch_available"] = True
            results["info"].append("✅ PyTorch is available")
        except ImportError as e:
            results["checks"]["pytorch_available"] = False
            results["errors"].append(f"❌ PyTorch not available: {str(e)}")
            return results
        
        # Check 2: ROCm PyTorch support
        try:            # Check if this PyTorch build supports ROCm
            hip_version = getattr(torch.version, 'hip', None)
            rocm_available = torch.cuda.is_available() and hip_version and "rocm" in str(hip_version)
            results["checks"]["rocm_available"] = rocm_available
            
            if rocm_available:
                results["info"].append("✅ ROCm PyTorch build detected")
            else:
                # Could still be ROCm - check differently
                try:
                    import torch.utils.cpp_extension
                    # Try to detect ROCm through CUDA calls (ROCm maps CUDA API)
                    if torch.cuda.is_available():
                        device_name = torch.cuda.get_device_name(0)
                        if "AMD" in device_name or "Radeon" in device_name:
                            results["checks"]["rocm_available"] = True
                            results["info"].append(f"✅ AMD GPU detected via ROCm: {device_name}")
                        else:
                            results["warnings"].append("⚠️ CUDA available but non-AMD GPU detected")
                            results["checks"]["rocm_available"] = False
                    else:
                        results["warnings"].append("⚠️ No GPU acceleration detected")
                        results["checks"]["rocm_available"] = False
                        
                except Exception as e:
                    results["warnings"].append(f"⚠️ Could not determine ROCm status: {str(e)}")
                    results["checks"]["rocm_available"] = False
                    
        except Exception as e:
            results["checks"]["rocm_available"] = False
            results["errors"].append(f"❌ ROCm detection failed: {str(e)}")
        
        # Check 3: HIP availability (ROCm's CUDA equivalent)
        try:
            if results["checks"].get("rocm_available", False):
                # Check HIP version through PyTorch
                if hasattr(torch.version, 'hip') and torch.version.hip:
                    hip_version = torch.version.hip
                    results["checks"]["hip_version"] = hip_version
                    results["info"].append(f"✅ HIP version: {hip_version}")
                else:
                    results["warnings"].append("⚠️ Could not detect HIP version")
                    
        except Exception as e:
            results["warnings"].append(f"⚠️ HIP version check failed: {str(e)}")
        
        # Check 4: Device count and properties
        if results["checks"].get("rocm_available", False):
            try:
                device_count = torch.cuda.device_count()
                results["checks"]["rocm_device_count"] = device_count
                results["info"].append(f"✅ Found {device_count} ROCm device(s)")
                
                # Get device properties
                for i in range(device_count):
                    try:
                        device_props = torch.cuda.get_device_properties(i)
                        device_info = {
                            "name": device_props.name,
                            "memory_mb": device_props.total_memory // (1024 * 1024),
                            "multiprocessor_count": device_props.multi_processor_count
                        }
                        results["checks"][f"rocm_device_{i}"] = device_info
                        results["info"].append(f"✅ ROCm Device {i}: {device_props.name} ({device_info['memory_mb']}MB)")
                        
                    except Exception as e:
                        results["warnings"].append(f"⚠️ Could not get device {i} properties: {str(e)}")
                        
            except Exception as e:
                results["errors"].append(f"❌ ROCm device detection failed: {str(e)}")
                results["checks"]["rocm_device_count"] = 0
        else:
            results["checks"]["rocm_device_count"] = 0
        
        # Check 5: Basic tensor operations
        if results["checks"].get("rocm_device_count", 0) > 0:
            try:
                device = torch.device('cuda:0')  # ROCm maps to cuda device in PyTorch
                test_tensor = torch.randn(100, 100, device=device)
                result_tensor = torch.matmul(test_tensor, test_tensor.t())
                cpu_result = result_tensor.cpu()
                
                results["checks"]["rocm_tensor_operations"] = True
                results["info"].append("✅ Basic ROCm tensor operations working")
                
            except Exception as e:
                results["checks"]["rocm_tensor_operations"] = False
                results["errors"].append(f"❌ ROCm tensor operations failed: {str(e)}")
        else:
            results["checks"]["rocm_tensor_operations"] = False
            results["warnings"].append("⚠️ Skipping tensor operations - no ROCm devices")
        
        # Check 6: Memory allocation test
        if results["checks"].get("rocm_device_count", 0) > 0:
            try:
                device = torch.device('cuda:0')
                # Try to allocate 100MB
                test_mem = torch.zeros(100 * 1024 * 1024 // 4, device=device)
                del test_mem
                torch.cuda.empty_cache()
                
                results["checks"]["rocm_memory_allocation"] = True
                results["info"].append("✅ ROCm memory allocation test passed")
                
            except Exception as e:
                results["checks"]["rocm_memory_allocation"] = False
                results["errors"].append(f"❌ ROCm memory allocation failed: {str(e)}")
        else:
            results["checks"]["rocm_memory_allocation"] = False
            results["warnings"].append("⚠️ Skipping memory allocation - no ROCm devices")
        
        # Check 7: ROCm integration test
        if results["checks"].get("rocm_device_count", 0) > 0:
            try:
                device = torch.device('cuda:0')
                x = torch.tensor([1.0, 2.0, 3.0], device=device)
                y = x * 2
                result = y.cpu().numpy()
                
                expected = [2.0, 4.0, 6.0]
                if all(abs(a - b) < 0.001 for a, b in zip(result, expected)):
                    results["checks"]["rocm_integration"] = True
                    results["info"].append("✅ PyTorch ROCm integration working correctly")
                else:
                    results["checks"]["rocm_integration"] = False
                    results["errors"].append("❌ ROCm computation results incorrect")
                    
            except Exception as e:
                results["checks"]["rocm_integration"] = False
                results["errors"].append(f"❌ ROCm integration test failed: {str(e)}")
        else:
            results["checks"]["rocm_integration"] = False
            results["warnings"].append("⚠️ Skipping integration test - no ROCm devices")
        
        # Determine overall success
        device_count = results["checks"].get("rocm_device_count", 0)
        if device_count > 0:
            critical_checks = [
                "pytorch_available",
                "rocm_available",
                "rocm_tensor_operations",
                "rocm_memory_allocation",
                "rocm_integration"
            ]
        else:
            # If no devices, just check that software is available
            critical_checks = [
                "pytorch_available",
                "rocm_available"
            ]
        
        results["overall_success"] = all(
            results["checks"].get(check, False) for check in critical_checks
        )
        
        if results["overall_success"]:
            if device_count > 0:
                results["info"].append("🎉 All ROCm validation checks passed!")
            else:
                results["info"].append("📦 ROCm software available (no devices detected)")
        else:
            results["errors"].append("💥 Some critical ROCm validation checks failed")
        
    except Exception as e:
        results["errors"].append(f"💥 Validation script exception: {str(e)}")
        results["errors"].append(f"Traceback: {traceback.format_exc()}")
    
    return results


def print_validation_results(results):
    """Print validation results in a formatted way"""
    print("=" * 60)
    print("🔍 ROCm Validation Results")
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
    print("🚀 Starting ROCm validation...")
    
    # Run validation
    validation_results = validate_rocm_setup()
    
    # Print results
    print_validation_results(validation_results)
    
    # Exit with appropriate code
    sys.exit(0 if validation_results["overall_success"] else 1)
