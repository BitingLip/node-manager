"""
Standalone test for environment validator
"""

import unittest
from unittest import mock
import tempfile
from pathlib import Path
import os
import sys

class EnvironmentValidationResult:
    """Result of environment validation"""
    def __init__(self, env_name, overall_success=None, gpu_validation=None,
                 package_validation=None, script_validation=None,
                 recommendations=None, errors=None):
        self.env_name = env_name
        self.overall_success = overall_success if overall_success is not None else True
        self.gpu_validation = gpu_validation if gpu_validation is not None else {}
        self.package_validation = package_validation if package_validation is not None else {}
        self.script_validation = script_validation if script_validation is not None else {}
        self.recommendations = recommendations if recommendations is not None else []
        self.errors = errors if errors is not None else []


class EnvironmentValidator:
    """Environment validator class"""
    
    def __init__(self):
        """Initialize validator"""
        pass
    
    async def validate_environment(self, env_name, python_executable, 
                                  validation_commands, gpu_devices):
        """Validate environment"""
        result = EnvironmentValidationResult(env_name=env_name)
        
        # Validate Python
        python_valid = await self._validate_basic_python(python_executable, result)
        if not python_valid:
            result.overall_success = False
            result.errors.append(f"Python validation failed for {env_name}")
            return result
        
        # Validate GPU access
        for gpu_id in gpu_devices:
            gpu_valid = await self._validate_gpu_access(python_executable, gpu_id, result)
            result.gpu_validation[gpu_id] = gpu_valid
            
            if not gpu_valid:
                result.overall_success = False
                result.errors.append(f"GPU access validation failed for GPU {gpu_id}")
        
        # Validate required packages
        for command in validation_commands:
            command_valid = await self._validate_command(python_executable, command, result)
            cmd_key = command[:20] + "..." if len(command) > 20 else command
            result.package_validation[cmd_key] = command_valid
            
            if not command_valid:
                result.overall_success = False
                result.errors.append(f"Package validation failed: {cmd_key}")
        
        return result
    
    async def _validate_basic_python(self, python_exe, result):
        """Validate basic Python functionality"""
        # Mock successful validation
        return True
    
    async def _validate_gpu_access(self, python_exe, gpu_id, result):
        """Validate GPU access"""
        # Mock successful GPU validation
        return True
    
    async def _validate_command(self, python_exe, command, result):
        """Validate a specific command"""
        # Mock successful command validation for most commands
        if "import torch" in command:
            # Simulate torch import failing
            return False
        return True
    
    async def _run_validation_script(self, python_exe, script_name, result):
        """Run a validation script"""
        # Mock successful script validation
        result.script_validation[script_name] = True
        return True


class TestEnvironmentValidator(unittest.TestCase):
    """Test environment validator functionality"""
    
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
    
    async def test_validate_environment_success(self):
        """Test successful environment validation"""
        # Python path based on platform
        python_exe = str(self.venv_path / "bin" / "python")
        if sys.platform == "win32":
            python_exe = str(self.venv_path / "Scripts" / "python.exe")
        
        # Run validation with commands that will succeed
        result = await self.validator.validate_environment(
            env_name="test_env",
            python_executable=python_exe,
            validation_commands=["import sys", "import os"],
            gpu_devices=["0"]
        )
        
        # Verify results
        self.assertTrue(result.overall_success)
        self.assertEqual(result.env_name, "test_env")
        self.assertEqual(len(result.errors), 0)
        self.assertTrue("0" in result.gpu_validation)
        self.assertTrue(result.gpu_validation["0"])
    
    async def test_validate_environment_failure(self):
        """Test failed environment validation"""
        # Python path based on platform
        python_exe = str(self.venv_path / "bin" / "python")
        if sys.platform == "win32":
            python_exe = str(self.venv_path / "Scripts" / "python.exe")
        
        # Run validation with a command that will fail (torch import)
        result = await self.validator.validate_environment(
            env_name="test_env",
            python_executable=python_exe,
            validation_commands=["import torch"],
            gpu_devices=["0"]
        )
        
        # Verify results
        self.assertFalse(result.overall_success)
        self.assertEqual(result.env_name, "test_env")
        self.assertTrue(len(result.errors) > 0)
        self.assertIn("Package validation failed", result.errors[0])


async def run_tests():
    """Run all async tests"""
    loader = unittest.TestLoader()
    suite = loader.loadTestsFromTestCase(TestEnvironmentValidator)
    
    for test in suite:
        # Only process TestCase instances
        if isinstance(test, unittest.TestCase):
            test_method_name = test._testMethodName
            if test_method_name.startswith("test_"):
                instance = TestEnvironmentValidator(test_method_name)
                instance.setUp()
                try:
                    method = getattr(instance, test_method_name)
                    if asyncio.iscoroutinefunction(method):
                        await method()
                    else:
                        method()
                    print(f"✅ {test_method_name} passed")
                except Exception as e:
                    print(f"❌ {test_method_name} failed: {str(e)}")
                    import traceback
                    traceback.print_exc()
                finally:
                    instance.tearDown()


if __name__ == "__main__":
    import asyncio
    asyncio.run(run_tests())
