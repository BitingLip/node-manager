import asyncio
import sys
import os

# Add src to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '../../src'))

from Workers.core.enhanced_orchestrator import EnhancedProtocolOrchestrator

async def quick_test():
    print("[INFO] Starting quick integration test...")
    
    orchestrator = EnhancedProtocolOrchestrator()
    
    # Test basic C# request format
    csharp_request = {
        "message_type": "generate_sdxl_enhanced",
        "session_id": "test_session_001",
        "prompt": "A beautiful landscape",
        "worker_type": "simple"
    }
    
    try:
        # Parse the request
        request = orchestrator.parse_enhanced_request(csharp_request)
        print(f"[SUCCESS] Parsed request: {request.message_type}")
        
        # Transform to legacy protocol
        legacy_command = orchestrator.transform_to_legacy_protocol(request)
        print(f"[SUCCESS] Transformed to legacy: {legacy_command['action']}")
        
        # Test worker routing
        worker_type = orchestrator.get_appropriate_worker(request)
        print(f"[SUCCESS] Worker routing: {worker_type}")
        
        print("[PASS] All integration tests passed!")
        return True
        
    except Exception as e:
        print(f"[ERROR] Test failed: {e}")
        return False

if __name__ == "__main__":
    result = asyncio.run(quick_test())
    sys.exit(0 if result else 1)
