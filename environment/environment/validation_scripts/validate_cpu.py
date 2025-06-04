"""
CPU PyTorch Validation Script
Validates CPU-only PyTorch setup for systems without GPU or as fallback
"""

import sys
import traceback


def validate_cpu_setup():
    """Comprehensive CPU PyTorch validation"""
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
        
        # Check 2: PyTorch version
        try:
            pytorch_version = torch.__version__
            results["checks"]["pytorch_version"] = pytorch_version
            results["info"].append(f"✅ PyTorch version: {pytorch_version}")
        except Exception as e:
            results["warnings"].append(f"⚠️ Could not get PyTorch version: {str(e)}")
        
        # Check 3: CPU tensor operations
        try:
            # Test basic tensor creation and operations
            test_tensor = torch.randn(100, 100)
            result_tensor = torch.matmul(test_tensor, test_tensor.t())
            
            # Verify result shape
            if result_tensor.shape == (100, 100):
                results["checks"]["cpu_tensor_operations"] = True
                results["info"].append("✅ CPU tensor operations working")
            else:
                results["checks"]["cpu_tensor_operations"] = False
                results["errors"].append("❌ CPU tensor operations produced wrong shape")
                
        except Exception as e:
            results["checks"]["cpu_tensor_operations"] = False
            results["errors"].append(f"❌ CPU tensor operations failed: {str(e)}")
        
        # Check 4: Math operations accuracy
        try:
            # Test basic mathematical operations
            x = torch.tensor([1.0, 2.0, 3.0])
            y = torch.tensor([4.0, 5.0, 6.0])
            
            # Addition
            add_result = x + y
            expected_add = torch.tensor([5.0, 7.0, 9.0])
            
            # Multiplication
            mul_result = x * y
            expected_mul = torch.tensor([4.0, 10.0, 18.0])
            
            # Matrix multiplication
            mat_a = torch.randn(10, 20)
            mat_b = torch.randn(20, 15)
            mat_result = torch.matmul(mat_a, mat_b)
            
            if (torch.allclose(add_result, expected_add) and 
                torch.allclose(mul_result, expected_mul) and 
                mat_result.shape == (10, 15)):
                
                results["checks"]["cpu_math_operations"] = True
                results["info"].append("✅ CPU mathematical operations accurate")
            else:
                results["checks"]["cpu_math_operations"] = False
                results["errors"].append("❌ CPU mathematical operations inaccurate")
                
        except Exception as e:
            results["checks"]["cpu_math_operations"] = False
            results["errors"].append(f"❌ CPU math operations failed: {str(e)}")
        
        # Check 5: Memory allocation
        try:
            # Try to allocate reasonable amount of memory (100MB)
            test_mem = torch.zeros(100 * 1024 * 1024 // 4)  # 100MB of float32
            memory_size = test_mem.numel() * 4 // (1024 * 1024)  # Size in MB
            del test_mem
            
            results["checks"]["cpu_memory_allocation"] = True
            results["info"].append(f"✅ CPU memory allocation test passed ({memory_size}MB)")
            
        except Exception as e:
            results["checks"]["cpu_memory_allocation"] = False
            results["errors"].append(f"❌ CPU memory allocation failed: {str(e)}")
        
        # Check 6: Neural network operations
        try:
            import torch.nn as nn
            import torch.nn.functional as F
            
            # Create a simple neural network
            class SimpleNet(nn.Module):
                def __init__(self):
                    super(SimpleNet, self).__init__()
                    self.fc1 = nn.Linear(10, 50)
                    self.fc2 = nn.Linear(50, 1)
                
                def forward(self, x):
                    x = F.relu(self.fc1(x))
                    x = self.fc2(x)
                    return x
            
            # Test the network
            net = SimpleNet()
            test_input = torch.randn(32, 10)  # Batch of 32, 10 features each
            output = net(test_input)
            
            if output.shape == (32, 1):
                results["checks"]["cpu_neural_network"] = True
                results["info"].append("✅ CPU neural network operations working")
            else:
                results["checks"]["cpu_neural_network"] = False
                results["errors"].append("❌ CPU neural network operations failed")
                
        except Exception as e:
            results["checks"]["cpu_neural_network"] = False
            results["errors"].append(f"❌ CPU neural network operations failed: {str(e)}")
        
        # Check 7: Gradient computation
        try:
            # Test automatic differentiation
            x = torch.tensor([1.0, 2.0, 3.0], requires_grad=True)
            y = x ** 2
            loss = y.sum()
            loss.backward()
            
            expected_grad = torch.tensor([2.0, 4.0, 6.0])
            if x.grad is not None and torch.allclose(x.grad, expected_grad):
                results["checks"]["cpu_gradients"] = True
                results["info"].append("✅ CPU gradient computation working")
            else:
                results["checks"]["cpu_gradients"] = False
                results["errors"].append("❌ CPU gradient computation failed")
                
        except Exception as e:
            results["checks"]["cpu_gradients"] = False
            results["errors"].append(f"❌ CPU gradient computation failed: {str(e)}")
        
        # Check 8: Confirm no GPU acceleration (expected for CPU validation)
        try:
            gpu_available = torch.cuda.is_available()
            if gpu_available:
                results["warnings"].append("⚠️ GPU is available but running CPU validation")
                results["info"].append("💡 This is expected if validating CPU fallback configuration")
            else:
                results["info"].append("✅ No GPU detected - CPU-only configuration confirmed")
            results["checks"]["cpu_only_confirmed"] = True
            
        except Exception as e:
            results["warnings"].append(f"⚠️ Could not check GPU status: {str(e)}")
            results["checks"]["cpu_only_confirmed"] = True  # Assume CPU-only if check fails
        
        # Determine overall success
        critical_checks = [
            "pytorch_available",
            "cpu_tensor_operations",
            "cpu_math_operations",
            "cpu_memory_allocation",
            "cpu_neural_network",
            "cpu_gradients"
        ]
        
        results["overall_success"] = all(
            results["checks"].get(check, False) for check in critical_checks
        )
        
        if results["overall_success"]:
            results["info"].append("🎉 All CPU PyTorch validation checks passed!")
        else:
            results["errors"].append("💥 Some critical CPU PyTorch validation checks failed")
        
    except Exception as e:
        results["errors"].append(f"💥 Validation script exception: {str(e)}")
        results["errors"].append(f"Traceback: {traceback.format_exc()}")
    
    return results


def print_validation_results(results):
    """Print validation results in a formatted way"""
    print("=" * 60)
    print("🔍 CPU PyTorch Validation Results")
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
    print("🚀 Starting CPU PyTorch validation...")
    
    # Run validation
    validation_results = validate_cpu_setup()
    
    # Print results
    print_validation_results(validation_results)
    
    # Exit with appropriate code
    sys.exit(0 if validation_results["overall_success"] else 1)
