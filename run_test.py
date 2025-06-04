"""
Run all monitoring tests in the monitoring/tests folder.
"""
import sys
import pytest

if __name__ == "__main__":
    # Run all tests in the monitoring/tests directory
    sys.exit(pytest.main(["-v", "monitoring/tests"]))
