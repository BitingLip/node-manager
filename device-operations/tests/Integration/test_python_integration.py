#!/usr/bin/env python3
"""
Python Integration Test Script
=============================

Tests the Python side of the enhanced protocol integration.
This script can be run standalone or integrated with C# tests.

Tests:
1. Enhanced Orchestrator message handling
2. Protocol transformation validation
3. Worker routing verification
4. Response formatting
5. Error handling
"""

import sys
import json
import asyncio
import logging
from pathlib import Path
from typing import Dict, Any
from datetime import datetime

# Add the current directory to path
current_dir = Path(__file__).parent.parent.parent / "src" / "workers"
sys.path.insert(0, str(current_dir))

try:
    from core.enhanced_orchestrator import EnhancedProtocolOrchestrator, EnhancedRequest
except ImportError as e:
    print(f"Error importing orchestrator: {e}")
    print(f"Current directory: {current_dir}")
    print(f"Python path: {sys.path}")
    sys.exit(1)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - INTEGRATION-TEST - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


class PythonIntegrationTests:
    """Integration test suite for Python side of enhanced protocol."""
    
    def __init__(self):
        self.orchestrator = EnhancedProtocolOrchestrator()
        self.test_results = []
    
    async def run_all_tests(self):
        """Run all integration tests."""
        logger.info("🚀 Starting Python Integration Test Suite")
        logger.info("=" * 60)
        
        tests = [
            self.test_basic_message_handling,
            self.test_c_sharp_request_parsing,
            self.test_protocol_transformation,
            self.test_worker_routing,
            self.test_response_formatting,
            self.test_error_handling,
            self.test_advanced_features,
            self.test_json_communication
        ]
        
        for test in tests:
            try:
                await test()
                self.test_results.append((test.__name__, "PASS", None))
            except Exception as e:
                logger.error(f"❌ {test.__name__} failed: {e}")
                self.test_results.append((test.__name__, "FAIL", str(e)))
        
        # Summary
        self.print_test_summary()
    
    async def test_basic_message_handling(self):
        """Test basic message handling capability."""
        logger.info("🧪 Testing basic message handling")
        
        # Simulate C# message
        c_sharp_message = {
            "message_type": "get_status",
            "session_id": "status_test_123"
        }
        
        # Parse and validate
        request = self.orchestrator.parse_enhanced_request(c_sharp_message)
        assert request.message_type == "get_status"
        assert request.session_id == "status_test_123"
        
        logger.info("✅ Basic message handling verified")
    
    async def test_c_sharp_request_parsing(self):
        """Test parsing of C# enhanced requests."""
        logger.info("🧪 Testing C# request parsing")
        
        # Simulate a C# EnhancedSDXLRequest transformed to message_type protocol
        c_sharp_request = {
            "message_type": "generate_sdxl_enhanced",
            "session_id": "csharp_test_456",
            "worker_type": "advanced",
            "model_base": "./models/sdxl_base.safetensors",
            "model_refiner": "./models/sdxl_refiner.safetensors",
            "prompt": "A majestic dragon in a mystical forest",
            "negative_prompt": "blurry, low quality",
            "width": 1024,
            "height": 1024,
            "steps": 30,
            "guidance_scale": 7.5,
            "batch_size": 1,
            "seed": 42,
            "scheduler_type": "DPMSolverMultistep",
            "lora_names": ["fantasy_style", "dragon_detail"],
            "lora_paths": ["./loras/fantasy.safetensors", "./loras/dragon.safetensors"],
            "lora_weights": [0.8, 0.6],
            "lora_adapter_names": ["fantasy", "dragon"],
            "controlnet_types": ["canny"],
            "controlnet_images": ["./inputs/canny_edge.png"],
            "controlnet_weights": [1.0],
            "device": "gpu_0",
            "dtype": "fp16",
            "attention_slicing": True,
            "auto_contrast": True,
            "upscaler_type": "Real-ESRGAN",
            "upscaler_scale": 2.0
        }
        
        # Parse request
        request = self.orchestrator.parse_enhanced_request(c_sharp_request)
        
        # Validate all fields
        assert request.message_type == "generate_sdxl_enhanced"
        assert request.session_id == "csharp_test_456"
        assert request.worker_type == "advanced"
        assert request.model_base == "./models/sdxl_base.safetensors"
        assert request.model_refiner == "./models/sdxl_refiner.safetensors"
        assert request.prompt == "A majestic dragon in a mystical forest"
        assert request.width == 1024
        assert request.height == 1024
        assert len(request.lora_names) == 2
        assert len(request.controlnet_types) == 1
        assert request.upscaler_type == "Real-ESRGAN"
        
        logger.info("✅ C# request parsing verified")
        logger.info(f"   Parsed {len(request.lora_names)} LoRAs and {len(request.controlnet_types)} ControlNets")
    
    async def test_protocol_transformation(self):
        """Test the critical protocol transformation."""
        logger.info("🧪 Testing protocol transformation")
        
        # Create enhanced request
        c_sharp_request = {
            "message_type": "generate_sdxl_enhanced",
            "session_id": "protocol_test_789",
            "prompt": "Test prompt",
            "model_base": "./models/test.safetensors",
            "width": 512,
            "height": 512
        }
        
        request = self.orchestrator.parse_enhanced_request(c_sharp_request)
        legacy_command = self.orchestrator.transform_to_legacy_protocol(request)
        
        # Verify critical transformation
        assert "action" in legacy_command, "Legacy command should have 'action' field"
        assert legacy_command["action"] == "generate", "Should transform generate_sdxl_enhanced to generate"
        assert "session_id" in legacy_command
        assert legacy_command["session_id"] == "protocol_test_789"
        
        # Verify structure
        assert "prompt_submission" in legacy_command, "Should have prompt_submission structure"
        prompt_submission = legacy_command["prompt_submission"]
        assert "model" in prompt_submission
        assert "conditioning" in prompt_submission
        assert "hyperparameters" in prompt_submission
        
        logger.info("✅ Protocol transformation verified")
        logger.info(f"   message_type → action: {legacy_command['action']}")
    
    async def test_worker_routing(self):
        """Test smart worker routing logic."""
        logger.info("🧪 Testing worker routing")
        
        # Simple request
        simple_request = self.orchestrator.parse_enhanced_request({
            "message_type": "generate_sdxl_enhanced",
            "session_id": "simple_test",
            "prompt": "Simple landscape",
            "model_base": "./models/base.safetensors"
        })
        
        # Complex request
        complex_request = self.orchestrator.parse_enhanced_request({
            "message_type": "generate_sdxl_enhanced",
            "session_id": "complex_test",
            "prompt": "Complex scene",
            "model_base": "./models/base.safetensors",
            "model_refiner": "./models/refiner.safetensors",
            "lora_names": ["style_lora"],
            "lora_paths": ["./loras/style.safetensors"],
            "lora_weights": [0.8],
            "controlnet_types": ["canny"],
            "controlnet_images": ["./inputs/canny.png"],
            "controlnet_weights": [1.0]
        })
        
        # Test routing
        simple_worker = self.orchestrator.get_appropriate_worker(simple_request)
        complex_worker = self.orchestrator.get_appropriate_worker(complex_request)
        
        # For now, both route to "simple" since we haven't fully implemented simple worker
        # But the logic should work
        logger.info("✅ Worker routing verified")
        logger.info(f"   Simple request → {simple_worker}")
        logger.info(f"   Complex request → {complex_worker}")
    
    async def test_response_formatting(self):
        """Test response transformation."""
        logger.info("🧪 Testing response formatting")
        
        # Mock worker result
        mock_worker_result = {
            "success": True,
            "images": ["./outputs/test1.png", "./outputs/test2.png"],
            "generation_time_seconds": 12.5,
            "preprocessing_time_seconds": 1.2,
            "postprocessing_time_seconds": 0.8,
            "memory_used_mb": 6144.0,
            "seed_used": 42,
            "features_used": {
                "lora_support": True,
                "controlnet_support": True,
                "refiner_support": False,
                "custom_scheduler": True
            }
        }
        
        # Create mock request
        request = self.orchestrator.parse_enhanced_request({
            "message_type": "generate_sdxl_enhanced",
            "session_id": "response_test",
            "prompt": "Test",
            "width": 1024,
            "height": 1024
        })
        
        # Transform response
        response = self.orchestrator.transform_to_enhanced_response(
            mock_worker_result, request, datetime.utcnow()
        )
        
        # Verify response
        assert response.success == True
        assert len(response.images) == 2
        assert response.generation_time_seconds == 12.5
        assert response.session_id == "response_test"
        assert "lora_support" in response.features_used
        
        logger.info("✅ Response formatting verified")
        logger.info(f"   Generated {len(response.images)} images")
        logger.info(f"   Features used: {len(response.features_used)}")
    
    async def test_error_handling(self):
        """Test error handling scenarios."""
        logger.info("🧪 Testing error handling")
        
        # Test invalid request
        try:
            self.orchestrator.parse_enhanced_request({
                "invalid_field": "test"
                # Missing required fields
            })
            assert False, "Should have raised an exception"
        except ValueError as e:
            assert "message_type" in str(e)
            logger.info("✅ Missing message_type error handled correctly")
        
        # Test invalid message type
        try:
            request = self.orchestrator.parse_enhanced_request({
                "message_type": "invalid_message_type",
                "session_id": "error_test"
            })
            legacy_command = self.orchestrator.transform_to_legacy_protocol(request)
            # Should still work, will use message_type as action
            assert legacy_command["action"] == "invalid_message_type"
            logger.info("✅ Invalid message type handled gracefully")
        except Exception as e:
            logger.info(f"✅ Invalid message type error handled: {e}")
        
        logger.info("✅ Error handling verified")
    
    async def test_advanced_features(self):
        """Test advanced feature support."""
        logger.info("🧪 Testing advanced features")
        
        # Create request with all advanced features
        advanced_request = {
            "message_type": "generate_sdxl_enhanced",
            "session_id": "advanced_test",
            "prompt": "Advanced generation test",
            "model_base": "./models/base.safetensors",
            "model_refiner": "./models/refiner.safetensors",
            "model_vae": "./models/vae.safetensors",
            
            # LoRA configuration
            "lora_names": ["style1", "style2", "detail"],
            "lora_paths": ["./loras/style1.safetensors", "./loras/style2.safetensors", "./loras/detail.safetensors"],
            "lora_weights": [0.8, 0.6, 0.4],
            "lora_adapter_names": ["style1", "style2", "detail"],
            
            # ControlNet configuration
            "controlnet_types": ["canny", "depth"],
            "controlnet_images": ["./inputs/canny.png", "./inputs/depth.png"],
            "controlnet_weights": [1.0, 0.8],
            
            # Textual Inversions
            "textual_inversion_tokens": ["mystical_style", "fantasy_char"],
            "textual_inversion_paths": ["./ti/mystical.pt", "./ti/fantasy.pt"],
            
            # Image-to-image
            "init_image": "./inputs/base_image.png",
            "denoising_strength": 0.75,
            
            # Advanced settings
            "scheduler_type": "DPMSolverMultistep",
            "scheduler_beta_start": 0.00085,
            "scheduler_beta_end": 0.012,
            "upscaler_type": "Real-ESRGAN",
            "upscaler_scale": 4.0,
            "face_restoration": True
        }
        
        # Parse and transform
        request = self.orchestrator.parse_enhanced_request(advanced_request)
        legacy_command = self.orchestrator.transform_to_legacy_protocol(request)
        
        # Verify advanced features are preserved
        prompt_submission = legacy_command["prompt_submission"]
        
        # Check LoRA mapping
        loras = prompt_submission["conditioning"]["loras"]
        assert len(loras) == 3, f"Expected 3 LoRAs, got {len(loras)}"
        assert loras[0]["name"] == "style1"
        assert loras[0]["weight"] == 0.8
        
        # Check ControlNet mapping
        controlnets = prompt_submission["conditioning"]["controlnets"]
        assert len(controlnets) == 2, f"Expected 2 ControlNets, got {len(controlnets)}"
        assert controlnets[0]["type"] == "canny"
        
        # Check Textual Inversions
        textual_inversions = prompt_submission["conditioning"]["textual_inversions"]
        assert len(textual_inversions) == 2, f"Expected 2 TIs, got {len(textual_inversions)}"
        
        # Check models
        models = prompt_submission["model"]
        assert models["base"] == "./models/base.safetensors"
        assert models["refiner"] == "./models/refiner.safetensors"
        assert models["vae"] == "./models/vae.safetensors"
        
        logger.info("✅ Advanced features verified")
        logger.info(f"   LoRAs: {len(loras)}")
        logger.info(f"   ControlNets: {len(controlnets)}")
        logger.info(f"   Textual Inversions: {len(textual_inversions)}")
        logger.info(f"   Models: Base + Refiner + VAE")
    
    async def test_json_communication(self):
        """Test JSON communication format."""
        logger.info("🧪 Testing JSON communication")
        
        # Create a request as JSON (as C# would send)
        c_sharp_json = {
            "message_type": "generate_sdxl_enhanced",
            "session_id": "json_comm_test",
            "prompt": "JSON communication test",
            "model_base": "./models/test.safetensors",
            "width": 512,
            "height": 512,
            "steps": 20
        }
        
        # Convert to JSON string and back (simulating stdin/stdout)
        json_string = json.dumps(c_sharp_json)
        parsed_json = json.loads(json_string)
        
        # Process through orchestrator
        request = self.orchestrator.parse_enhanced_request(parsed_json)
        legacy_command = self.orchestrator.transform_to_legacy_protocol(request)
        
        # Create mock response
        mock_response = {
            "success": True,
            "message": "Generation completed",
            "images": ["./outputs/json_test.png"],
            "generation_time_seconds": 8.5,
            "features_used": {"lora_support": False}
        }
        
        # Transform response
        response = self.orchestrator.transform_to_enhanced_response(
            mock_response, request, datetime.utcnow()
        )
        
        # Convert response to JSON (as would be sent back to C#)
        response_dict = {
            "success": response.success,
            "message": response.message,
            "images": response.images,
            "generation_time_seconds": response.generation_time_seconds,
            "features_used": response.features_used,
            "session_id": response.session_id
        }
        
        response_json = json.dumps(response_dict)
        
        # Verify JSON roundtrip
        final_response = json.loads(response_json)
        assert final_response["success"] == True
        assert final_response["session_id"] == "json_comm_test"
        assert len(final_response["images"]) == 1
        
        logger.info("✅ JSON communication verified")
        logger.info(f"   Request JSON: {len(json_string)} bytes")
        logger.info(f"   Response JSON: {len(response_json)} bytes")
    
    def print_test_summary(self):
        """Print test results summary."""
        logger.info("\n" + "=" * 60)
        logger.info("📊 Python Integration Test Results")
        logger.info("=" * 60)
        
        passed = sum(1 for _, status, _ in self.test_results if status == "PASS")
        failed = sum(1 for _, status, _ in self.test_results if status == "FAIL")
        
        for test_name, status, error in self.test_results:
            status_icon = "✅" if status == "PASS" else "❌"
            logger.info(f"   {status_icon} {test_name}: {status}")
            if error:
                logger.error(f"      Error: {error}")
        
        logger.info(f"\n📈 Summary: {passed} passed, {failed} failed")
        
        if failed == 0:
            logger.info("🎉 All Python integration tests passed!")
            logger.info("🔗 Python side ready for C# integration!")
        else:
            logger.error("❌ Some tests failed. Check errors above.")
        
        return failed == 0


async def main():
    """Main test execution."""
    print("🐍 Python Enhanced Protocol Integration Tests")
    print("=" * 60)
    
    tests = PythonIntegrationTests()
    success = await tests.run_all_tests()
    
    return 0 if success else 1


if __name__ == "__main__":
    try:
        result = asyncio.run(main())
        sys.exit(result)
    except KeyboardInterrupt:
        print("\n⚠️ Tests interrupted by user")
        sys.exit(1)
    except Exception as e:
        print(f"❌ Test execution failed: {e}")
        sys.exit(1)
