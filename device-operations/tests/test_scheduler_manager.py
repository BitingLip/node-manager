"""
Test script for the Scheduler Manager - Phase 2 Implementation
Tests basic functionality without requiring diffusers installation.
"""

import sys
import os
import asyncio
import logging

# Add the src directory to the path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src'))

# Set up logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

async def test_scheduler_manager():
    """Test the SchedulerManager functionality."""
    try:
        # Direct import approach
        import sys
        import os
        sys.path.append(os.path.join(os.path.dirname(__file__), 'src', 'workers', 'features'))
        from scheduler_manager import SchedulerManager
        
        logger.info("✅ Successfully imported SchedulerManager")
        
        # Initialize manager
        manager = SchedulerManager()
        logger.info("✅ Successfully initialized SchedulerManager")
        
        # Test supported schedulers list
        supported = manager.list_supported_schedulers()
        logger.info(f"✅ Listed {len(supported)} supported schedulers")
        
        # Test scheduler info
        info = manager.get_scheduler_info("DPMSolverMultistepScheduler")
        logger.info(f"✅ Got scheduler info: {info['name']}")
        
        # Test legacy mapping
        normalized = manager._normalize_scheduler_name("DPMSolverMultistep")
        logger.info(f"✅ Legacy mapping works: DPMSolverMultistep -> {normalized}")
        
        # Test scheduler recommendation
        recommendation = manager.recommend_scheduler("speed", 20)
        logger.info(f"✅ Scheduler recommendation: {recommendation['recommended_scheduler']}")
        
        # Test cache stats
        stats = manager.get_cache_stats()
        logger.info(f"✅ Cache stats: {stats}")
        
        logger.info("🎉 All SchedulerManager tests passed!")
        return True
        
    except Exception as e:
        logger.error(f"❌ Test failed: {str(e)}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    success = asyncio.run(test_scheduler_manager())
    if success:
        print("\n✅ Scheduler Manager is working correctly!")
        print("🚀 Ready for Phase 2 integration with Enhanced SDXL Worker")
    else:
        print("\n❌ Scheduler Manager has issues")
        sys.exit(1)
