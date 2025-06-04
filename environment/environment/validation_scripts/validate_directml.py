"""
DirectML Validation Script
Validates DirectML setup for AMD GPUs (RDNA1/RDNA2) on Windows
"""

import sys
import traceback


def validate_directml_setup():
    """Comprehensive DirectML validation for AMD GPUs"""
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
        
        # Check 2: DirectML extension availability
        try:
            import torch_directml
            results["checks"]["torch_directml_available"] = True
            results["info"].append("✅ torch-directml extension is available")
        except ImportError as e:
            results["checks"]["torch_directml_available"] = False
            results["errors"].append(f"❌ torch-directml not available: {str(e)}")
            results["errors"].append("💡 Install with: pip install torch-directml")
            return results
        
        # Check 3: DirectML device detection
        try:
            device_count = torch_directml.device_count()
            results["checks"]["directml_device_count"] = device_count
            
            if device_count > 0:
                results["info"].append(f"✅ Found {device_count} DirectML device(s)")
            else:
                results["warnings"].append("⚠️ No DirectML devices found")
                results["warnings"].append("💡 Check if AMD GPU drivers are properly installed")
        except Exception as e:
            results["checks"]["directml_device_count"] = 0
            results["errors"].append(f"❌ Could not detect DirectML devices: {str(e)}")
        
        # Check 4: DirectML device properties
        device_count = results["checks"].get("directml_device_count", 0)
        if device_count > 0:
            try:
                for i in range(device_count):
                    device_name = torch_directml.device_name(i)
                    results["checks"][f"directml_device_{i}"] = device_name
                    results["info"].append(f"✅ DirectML Device {i}: {device_name}")
            except Exception as e:
                results["warnings"].append(f"⚠️ Could not get device properties: {str(e)}")
        
        # Check 5: Basic DirectML tensor operations
        try:
            if device_count > 0:
                device = torch_directml.device()
                test_tensor = torch.randn(100, 100, device=device)
                result_tensor = torch.matmul(test_tensor, test_tensor.t())
                cpu_result = result_tensor.cpu()
                
                results["checks"]["directml_tensor_operations"] = True
                results["info"].append("✅ Basic DirectML tensor operations working")
            else:
                results["checks"]["directml_tensor_operations"] = False
                results["warnings"].append("⚠️ Skipping tensor operations - no DirectML devices")
                
        except Exception as e:
            results["checks"]["directml_tensor_operations"] = False
            results["errors"].append(f"❌ DirectML tensor operations failed: {str(e)}")
        
        # Check 6: Memory allocation test
        try:
            if device_count > 0:
                device = torch_directml.device()
                # Try to allocate 50MB (smaller than CUDA test due to DirectML limitations)
                test_mem = torch.zeros(50 * 1024 * 1024 // 4, device=device)
                del test_mem
                
                results["checks"]["directml_memory_allocation"] = True
                results["info"].append("✅ DirectML memory allocation test passed")
            else:
                results["checks"]["directml_memory_allocation"] = False
                results["warnings"].append("⚠️ Skipping memory allocation - no DirectML devices")
                
        except Exception as e:
            results["checks"]["directml_memory_allocation"] = False
            results["errors"].append(f"❌ DirectML memory allocation failed: {str(e)}")
        
        # Check 7: PyTorch DirectML integration
        try:
            # Check if DirectML is properly integrated with PyTorch
            if device_count > 0:
                device = torch_directml.device()
                x = torch.tensor([1.0, 2.0, 3.0], device=device)
                y = x * 2
                result = y.cpu().numpy()
                
                expected = [2.0, 4.0, 6.0]
                if all(abs(a - b) < 0.001 for a, b in zip(result, expected)):
                    results["checks"]["directml_integration"] = True
                    results["info"].append("✅ PyTorch DirectML integration working correctly")
                else:
                    results["checks"]["directml_integration"] = False
                    results["errors"].append("❌ DirectML computation results incorrect")
            else:
                results["checks"]["directml_integration"] = False
                results["warnings"].append("⚠️ Skipping integration test - no DirectML devices")
                
        except Exception as e:
            results["checks"]["directml_integration"] = False
            results["errors"].append(f"❌ DirectML integration test failed: {str(e)}")
        
        # Determine overall success
        if device_count > 0:
            critical_checks = [
                "pytorch_available",
                "torch_directml_available",
                "directml_tensor_operations",
                "directml_memory_allocation",
                "directml_integration"
            ]
        else:
            # If no devices, just check that software is installed correctly
            critical_checks = [
                "pytorch_available",
                "torch_directml_available"
            ]
        
        results["overall_success"] = all(
            results["checks"].get(check, False) for check in critical_checks
        )
        
        if results["overall_success"]:
            if device_count > 0:
                results["info"].append("🎉 All DirectML validation checks passed!")
            else:
                results["info"].append("📦 DirectML software installed correctly (no devices detected)")
        else:
            results["errors"].append("💥 Some critical DirectML validation checks failed")
        
    except Exception as e:
        results["errors"].append(f"💥 Validation script exception: {str(e)}")
        results["errors"].append(f"Traceback: {traceback.format_exc()}")
    
    return results


def print_validation_results(results):
    """Print validation results in a formatted way"""
    print("=" * 60)
    print("🔍 DirectML Validation Results")
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
    print("🚀 Starting DirectML validation...")
    
    # Run validation
    validation_results = validate_directml_setup()
    
    # Print results
    print_validation_results(validation_results)
    
    # Exit with appropriate code
    sys.exit(0 if validation_results["overall_success"] else 1)
