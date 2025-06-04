"""
Tests for the environment setup orchestrator
"""

import os
import unittest
import sys
import tempfile
import asyncio
from unittest import mock
from pathlib import Path
import json

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

from environment.orchestrator.environment_setup import (
    EnvironmentSetupOrchestrator,
    SetupSummary
)
from environment.gpu.gpu_detector import GPUInfo, GPUVendor, EnvironmentRequirement


class EnvironmentSetupOrchestratorTests(unittest.TestCase):
    """Tests for the environment setup orchestrator"""
    
    def setUp(self):
        """Set up test environment"""
        self.temp_dir = tempfile.TemporaryDirectory()
        
        # Create patch for base directory
        patcher = mock.patch.object(
            Path, 'cwd', 
            return_value=Path(self.temp_dir.name)
        )
        self.addCleanup(patcher.stop)
        patcher.start()
        
        # Create an orchestrator with mocked dependencies
        self.orchestrator = EnvironmentSetupOrchestrator()
    
    def tearDown(self):
        """Clean up test environment"""
        self.temp_dir.cleanup()
    
    @mock.patch('environment.orchestrator.environment_setup.GPUDetector')
    @mock.patch('environment.orchestrator.environment_setup.EnvironmentPlanner')
    @mock.patch('environment.orchestrator.environment_setup.EnvironmentValidator')
    @mock.patch('environment.orchestrator.environment_setup.VenvManager')
    @mock.patch('environment.orchestrator.environment_setup.get_strategy_requirements')
    async def test_setup_all_environments(self, mock_strategy, mock_venv, mock_validator, mock_planner, mock_detector):
        """Test setting up all environments"""
        # Mock GPU detection
        nvidia_gpu = mock.Mock()
        nvidia_gpu.device_id = "0"
        nvidia_gpu.vendor = GPUVendor.NVIDIA
        nvidia_gpu.name = "NVIDIA GeForce RTX 3080"
        
        mock_detector_instance = mock.MagicMock()
        mock_detector_instance.detect_all_gpus.return_value = [nvidia_gpu]
        mock_detector.return_value = mock_detector_instance
        
        # Mock strategy analysis
        mock_strategy.return_value = {
            "strategy": "create_separate_envs",
            "nvidia_gpus": ["0"],
            "amd_gpus": [],
            "intel_gpus": []
        }
        
        # Mock environment planner
        mock_env_spec = mock.MagicMock()
        mock_env_spec.name = "cuda_env"
        mock_env_spec.target_gpus = ["0"]
        
        mock_planner_instance = mock.MagicMock()
        mock_planner_instance.plan_environments.return_value = {"cuda_env": mock_env_spec}
        mock_planner_instance.create_environment.return_value = mock.MagicMock(success=True)
        mock_planner.return_value = mock_planner_instance
        
        # Mock venv manager
        mock_venv_instance = mock.MagicMock()
        mock_venv.return_value = mock_venv_instance
        
        # Mock validator
        mock_validation_result = mock.MagicMock()
        mock_validation_result.overall_success = True
        mock_validation_result.env_name = "cuda_env"
        
        mock_validator_instance = mock.MagicMock()
        mock_validator_instance.validate_environment.return_value = mock_validation_result
        mock_validator.return_value = mock_validator_instance
          # Run the setup process
        summary = await self.orchestrator.setup_all()
        
        # Verify the setup was successful
        self.assertEqual(summary.total_gpus, 1)
        self.assertEqual(summary.environments_created, 1)
        self.assertEqual(summary.environments_successful, 1)
        
        # Verify the right methods were called
        mock_detector_instance.detect_all_gpus.assert_called_once()
        mock_planner_instance.plan_environments.assert_called_once()
        mock_planner_instance.create_environment.assert_called_once()
        mock_validator_instance.validate_environment.assert_called_once()
    
    @mock.patch('environment.orchestrator.environment_setup.GPUDetector')
    async def test_get_status(self, mock_detector):
        """Test getting system status"""
        # Mock GPU detection
        nvidia_gpu = mock.Mock()
        nvidia_gpu.device_id = "0"
        nvidia_gpu.vendor = GPUVendor.NVIDIA
        nvidia_gpu.name = "NVIDIA GeForce RTX 3080"
        
        mock_detector_instance = mock.MagicMock()
        mock_detector_instance.detect_all_gpus.return_value = [nvidia_gpu]
        mock_detector.return_value = mock_detector_instance
        
        # Get status
        status = self.orchestrator.get_status()
        
        # Verify status info
        self.assertIn("detected_gpus", status)
        self.assertEqual(status["detected_gpus"], 1)


if __name__ == '__main__':
    unittest.main()
