"""
Helper script to install required pytest packages
"""
import sys
import subprocess
import os

def install_dependencies():
    """Install required packages for testing"""
    required_packages = [
        "pytest",
        "pytest-asyncio"
    ]
    
    print("Installing required packages for testing...")
    for package in required_packages:
        try:
            print(f"Installing {package}...")
            subprocess.check_call([sys.executable, "-m", "pip", "install", package])
            print(f"Successfully installed {package}")
        except subprocess.CalledProcessError:
            print(f"Failed to install {package}")
            return False
    
    return True

if __name__ == "__main__":
    success = install_dependencies()
    if success:
        print("\nAll dependencies installed successfully.")
        print("You can now run the tests using: python test_monitoring.py")
    else:
        print("\nFailed to install dependencies.")
    
    sys.exit(0 if success else 1)
