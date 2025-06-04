"""
CPU PyTorch Validation Script (ASCII version)
Validates CPU-only PyTorch setup for systems without GPU or as fallback
"""

import sys
import traceback
import torch


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
            results["checks"]["pytorch_available"] = True
            results["info"].append("OK PyTorch is available")
        except ImportError as e:
            results["checks"]["pytorch_available"] = False
            results["errors"].append(f"ERROR PyTorch not available: {str(e)}")
            return results
        
        # Check 2: PyTorch version
        try:
            pytorch_version = torch.__version__
            results["checks"]["pytorch_version"] = pytorch_version
            results["info"].append(f"OK PyTorch version: {pytorch_version}")
        except Exception as e:
            results["warnings"].append(f"WARN Could not get PyTorch version: {str(e)}")
        
        # Check 3: CPU tensor operations
        try:
            test_tensor = torch.randn(100, 100)
            result_tensor = torch.matmul(test_tensor, test_tensor.t())
            
            if result_tensor.shape == (100, 100):
                results["checks"]["cpu_tensor_operations"] = True
                results["info"].append("OK CPU tensor operations working")
            else:
                results["checks"]["cpu_tensor_operations"] = False
                results["errors"].append("ERROR CPU tensor operations produced wrong shape")
                
        except Exception as e:
            results["checks"]["cpu_tensor_operations"] = False
            results["errors"].append(f"ERROR CPU tensor operations failed: {str(e)}")
        
        # Check 4: Memory allocation
        try:
            test_mem = torch.zeros(100 * 1024 * 1024 // 4)  # 100MB of float32
            memory_size = test_mem.numel() * 4 // (1024 * 1024)  # Size in MB
            del test_mem
            
            results["checks"]["cpu_memory_allocation"] = True
            results["info"].append(f"OK CPU memory allocation test passed ({memory_size}MB)")
            
        except Exception as e:
            results["checks"]["cpu_memory_allocation"] = False
            results["errors"].append(f"ERROR CPU memory allocation failed: {str(e)}")
        
        # Check 5: Neural network operations
        try:
            import torch.nn as nn
            import torch.nn.functional as F
            
            class SimpleNet(nn.Module):
                def __init__(self):
                    super(SimpleNet, self).__init__()
                    self.fc1 = nn.Linear(10, 50)
                    self.fc2 = nn.Linear(50, 1)
                
                def forward(self, x):
                    x = F.relu(self.fc1(x))
                    x = self.fc2(x)
                    return x
            
            net = SimpleNet()
            test_input = torch.randn(32, 10)
            output = net(test_input)
            
            if output.shape == (32, 1):
                results["checks"]["cpu_neural_network"] = True
                results["info"].append("OK CPU neural network operations working")
            else:
                results["checks"]["cpu_neural_network"] = False
                results["errors"].append("ERROR CPU neural network operations failed")
                
        except Exception as e:
            results["checks"]["cpu_neural_network"] = False
            results["errors"].append(f"ERROR CPU neural network operations failed: {str(e)}")
        
        # Check 6: Gradient computation
        try:
            x = torch.tensor([1.0, 2.0, 3.0], requires_grad=True)
            y = x ** 2
            loss = y.sum()
            loss.backward()
            
            expected_grad = torch.tensor([2.0, 4.0, 6.0])
            if x.grad is not None and torch.allclose(x.grad, expected_grad):
                results["checks"]["cpu_gradients"] = True
                results["info"].append("OK CPU gradient computation working")
            else:
                results["checks"]["cpu_gradients"] = False
                results["errors"].append("ERROR CPU gradient computation failed")
                
        except Exception as e:
            results["checks"]["cpu_gradients"] = False
            results["errors"].append(f"ERROR CPU gradient computation failed: {str(e)}")
        
        # Determine overall success
        critical_checks = [
            "pytorch_available",
            "cpu_tensor_operations", 
            "cpu_memory_allocation",
            "cpu_neural_network",
            "cpu_gradients"
        ]
        
        results["overall_success"] = all(
            results["checks"].get(check, False) for check in critical_checks
        )
        
        if results["overall_success"]:
            results["info"].append("SUCCESS All CPU PyTorch validation checks passed!")
        else:
            results["errors"].append("FAIL Some critical CPU PyTorch validation checks failed")
        
    except Exception as e:
        results["errors"].append(f"EXCEPTION Validation script exception: {str(e)}")
        results["errors"].append(f"Traceback: {traceback.format_exc()}")
    
    return results


def print_validation_results(results):
    """Print validation results in a formatted way"""
    print("=" * 60)
    print("CPU PyTorch Validation Results")
    print("=" * 60)
    
    if results["info"]:
        print("\nValidation Info:")
        for info in results["info"]:
            print(f"  {info}")
    
    if results["warnings"]:
        print("\nWarnings:")
        for warning in results["warnings"]:
            print(f"  {warning}")
    
    if results["errors"]:
        print("\nErrors:")
        for error in results["errors"]:
            print(f"  {error}")
    
    status = "PASS" if results["overall_success"] else "FAIL"
    print(f"\nOverall Status: {status}")
    print("=" * 60)


if __name__ == "__main__":
    print("Starting CPU PyTorch validation...")
    
    validation_results = validate_cpu_setup()
    print_validation_results(validation_results)
    
    sys.exit(0 if validation_results["overall_success"] else 1)
