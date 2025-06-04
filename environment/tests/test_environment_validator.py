"""
Tests for the environment validator module
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

from environment.environment.environment_validator import (
    EnvironmentValidator,
    EnvironmentValidationResult
)


class EnvironmentValidatorTests(unittest.TestCase):
    """Tests for the environment validator"""
    
    def setUp(self):
        """Set up test environment"""
        self.temp_dir = tempfile.TemporaryDirectory()
        self.validator = EnvironmentValidator()
        
        # Create a mock venv path
        self.venv_path = Path(self.temp_dir.name) / "test_venv"
        self.venv_path.mkdir(exist_ok=True)
        
        # Create mock python executable in venv
        python_dir = self.venv_path / "bin"
        python_dir.mkdir(exist_ok=True)
        python_path = python_dir / "python"
        python_path.touch()
        
        # Set executable flag if on Unix
        if sys.platform != "win32":
            os.chmod(python_path, 0o755)
        
        # Windows structure
        if sys.platform == "win32":
            scripts_dir = self.venv_path / "Scripts"
            scripts_dir.mkdir(exist_ok=True)
            win_python = scripts_dir / "python.exe"
            win_python.touch()
    
    def tearDown(self):
        """Clean up test environment"""
        self.temp_dir.cleanup()
    
    @mock.patch('environment.environment.environment_validator.asyncio.create_subprocess_exec')
    async def test_validate_environment_success(self, mock_exec):
        """Test validation of a working environment"""
        # Mock async process for successful validation
        mock_process = mock.AsyncMock()
        mock_process.returncode = 0
        mock_process.stdout.read = mock.AsyncMock(return_value=b"Validation successful")
        mock_process.stderr.read = mock.AsyncMock(return_value=b"")
        mock_process.communicate = mock.AsyncMock(return_value=(b"Validation successful", b""))
        
        mock_exec.return_value = mock_process
        
        # Get python path based on platform
        python_exe = str(self.venv_path / "bin" / "python")
        if sys.platform == "win32":
            python_exe = str(self.venv_path / "Scripts" / "python.exe")
          # Run validation
        result = await self.validator.validate_environment(
            env_name="test_env",
            python_executable=python_exe,
            validation_commands=["import sys; print('Python version:', sys.version)"],
            gpu_devices=["0"]
        )
        
        # Verify results
        self.assertTrue(result.overall_success)
        self.assertEqual(result.env_name, "test_env")
        self.assertEqual(len(result.errors), 0)
    
    @mock.patch('environment.environment.environment_validator.asyncio.create_subprocess_exec')
    async def test_validate_environment_failure(self, mock_exec):
        """Test validation of a failing environment"""
        # Mock async process for failed validation
        def mock_create_process(*args, **kwargs):
            mock_process = mock.AsyncMock()
            
            # Check if this is the Python validation or GPU validation
            if args[0].endswith('python') and '-c' in args:
                cmd_str = args[2]
                if 'import torch' in cmd_str:
                    mock_process.returncode = 1
                    mock_process.stdout.read = mock.AsyncMock(return_value=b"")
                    mock_process.stderr.read = mock.AsyncMock(return_value=b"ModuleNotFoundError: No module named 'torch'")
                    mock_process.communicate = mock.AsyncMock(return_value=(b"", b"ModuleNotFoundError: No module named 'torch'"))
                else:
                    mock_process.returncode = 0
                    mock_process.stdout.read = mock.AsyncMock(return_value=b"Python works")
                    mock_process.stderr.read = mock.AsyncMock(return_value=b"")
                    mock_process.communicate = mock.AsyncMock(return_value=(b"Python works", b""))
            else:
                mock_process.returncode = 0
                mock_process.stdout.read = mock.AsyncMock(return_value=b"OK")
                mock_process.stderr.read = mock.AsyncMock(return_value=b"")
                mock_process.communicate = mock.AsyncMock(return_value=(b"OK", b""))
                
            return mock_process
        
        mock_exec.side_effect = mock_create_process
        
        # Get python path based on platform
        python_exe = str(self.venv_path / "bin" / "python")
        if sys.platform == "win32":
            python_exe = str(self.venv_path / "Scripts" / "python.exe")
          # Run validation
        result = await self.validator.validate_environment(
            env_name="test_env",
            python_executable=python_exe,
            validation_commands=["import torch; print(torch.__version__)"],
            gpu_devices=["0"]
        )
        
        # Verify results
        self.assertFalse(result.overall_success)
        self.assertEqual(result.env_name, "test_env")
        self.assertTrue(len(result.errors) > 0)
    
    @mock.patch('environment.environment.environment_validator.asyncio.create_subprocess_exec')
    async def test_validate_gpu_access(self, mock_exec):
        """Test validation of GPU access"""
        # Mock async process for GPU validation
        mock_process = mock.AsyncMock()
        mock_process.returncode = 0
        mock_process.stdout.read = mock.AsyncMock(return_value=b"GPU 0: NVIDIA GeForce RTX 3080")
        mock_process.stderr.read = mock.AsyncMock(return_value=b"")
        mock_process.communicate = mock.AsyncMock(return_value=(b"GPU 0: NVIDIA GeForce RTX 3080", b""))
        
        mock_exec.return_value = mock_process
          # Create a result object to pass in
        result = EnvironmentValidationResult(
            env_name="test_env",
            overall_success=True,
            gpu_validation={},
            package_validation={},
            script_validation={},
            recommendations=[],
            errors=[]
        )
        
        # Python executable path
        python_exe = str(self.venv_path / "bin" / "python")
        if sys.platform == "win32":
            python_exe = str(self.venv_path / "Scripts" / "python.exe")
        
        # Run GPU validation
        success = await self.validator._validate_gpu_access(python_exe, "0", result)
        
        # Verify results
        self.assertTrue(success)
        self.assertTrue("0" in result.gpu_validation)
    
    @mock.patch('environment.environment.environment_validator.asyncio.create_subprocess_exec')
    async def test_run_validation_script(self, mock_exec):
        """Test running a validation script"""
        # Mock async process
        mock_process = mock.AsyncMock()
        mock_process.returncode = 0
        mock_process.stdout.read = mock.AsyncMock(return_value=b"Script validation successful")
        mock_process.stderr.read = mock.AsyncMock(return_value=b"")
        mock_process.communicate = mock.AsyncMock(return_value=(b"Script validation successful", b""))
        
        mock_exec.return_value = mock_process
          # Create a result object
        result = EnvironmentValidationResult(
            env_name="test_env",
            overall_success=True,
            gpu_validation={},
            package_validation={},
            script_validation={},
            recommendations=[],
            errors=[]
        )
        
        # Python path
        python_exe = str(self.venv_path / "bin" / "python")
        if sys.platform == "win32":
            python_exe = str(self.venv_path / "Scripts" / "python.exe")
        
        # Run validation script
        success = await self.validator._run_validation_script(python_exe, "test_script.py", result)
        
        # Verify results
        self.assertTrue(success)
        self.assertTrue("test_script.py" in result.script_validation)


if __name__ == '__main__':
    unittest.main()
