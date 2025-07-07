#!/usr/bin/env python3
"""
Workers Cleanup Script
======================

Removes obsolete files after transitioning to direct communication architecture.
"""

import os
import shutil
from pathlib import Path

def cleanup_obsolete_files():
    """Remove obsolete worker files."""
    workers_dir = Path(__file__).parent / "src" / "Workers"
    
    if not workers_dir.exists():
        print(f"❌ Workers directory not found: {workers_dir}")
        return False
    
    print("🧹 Cleaning up obsolete worker files...")
    print("=" * 60)
    
    # Files to remove
    obsolete_files = [
        # HTTP Server Architecture
        "workers_bridge.py",           # Complex HTTP server bridge
        "main.py",                     # Complex WorkerOrchestrator
        "run_worker.py",               # Wrapper for main.py
        
        # Complex Communication Infrastructure
        "core/communication.py",       # Complex IPC system
        "core/enhanced_orchestrator.py", # Complex protocol orchestrator
        "core/enhanced_request.py",    # Enhanced request handling
        
        # Configuration & Schema Files
        "config.json",                 # Configuration for complex orchestrator
        "schemas/prompt_submission_schema.json", # Schema validation
        "schemas/example_prompt.json", # Example schema file
        
        # Duplicate Files
        "features/scheduler_manager.py", # Duplicate of scheduler_manager_clean.py
        
        # Log Files
        "logs/enhanced-orchestrator.log", # Log from old orchestrator
    ]
    
    # Directories to remove entirely
    obsolete_directories = [
        "legacy",                      # Entire legacy directory
        "schemas",                     # Schema directory (after removing files)
        "logs",                        # Logs directory (after removing files)
    ]
    
    removed_files = []
    removed_dirs = []
    failed_removals = []
    
    # Remove individual files
    for file_path in obsolete_files:
        full_path = workers_dir / file_path
        try:
            if full_path.exists():
                if full_path.is_file():
                    full_path.unlink()
                    removed_files.append(str(file_path))
                    print(f"🗑️  Removed file: {file_path}")
                else:
                    print(f"⚠️  Skipped (not a file): {file_path}")
            else:
                print(f"ℹ️  Not found (already removed): {file_path}")
        except Exception as e:
            failed_removals.append(f"{file_path}: {str(e)}")
            print(f"❌ Failed to remove {file_path}: {str(e)}")
    
    # Remove directories
    for dir_path in obsolete_directories:
        full_path = workers_dir / dir_path
        try:
            if full_path.exists() and full_path.is_dir():
                shutil.rmtree(full_path)
                removed_dirs.append(str(dir_path))
                print(f"🗑️  Removed directory: {dir_path}")
            else:
                print(f"ℹ️  Directory not found: {dir_path}")
        except Exception as e:
            failed_removals.append(f"{dir_path}: {str(e)}")
            print(f"❌ Failed to remove directory {dir_path}: {str(e)}")
    
    # Summary
    print("\n" + "=" * 60)
    print("📊 Cleanup Summary:")
    print(f"✅ Files removed: {len(removed_files)}")
    print(f"✅ Directories removed: {len(removed_dirs)}")
    print(f"❌ Failed removals: {len(failed_removals)}")
    
    if removed_files:
        print(f"\n🗑️  Removed Files ({len(removed_files)}):")
        for file in removed_files:
            print(f"   • {file}")
    
    if removed_dirs:
        print(f"\n🗑️  Removed Directories ({len(removed_dirs)}):")
        for directory in removed_dirs:
            print(f"   • {directory}/")
    
    if failed_removals:
        print(f"\n❌ Failed Removals ({len(failed_removals)}):")
        for failure in failed_removals:
            print(f"   • {failure}")
    
    return len(failed_removals) == 0

def show_remaining_structure():
    """Show the remaining clean structure."""
    workers_dir = Path(__file__).parent / "src" / "Workers"
    
    print("\n" + "=" * 60)
    print("📁 Remaining Clean Structure:")
    print("=" * 60)
    
    def print_tree(directory, prefix="", max_depth=3, current_depth=0):
        if current_depth >= max_depth:
            return
            
        items = sorted([item for item in directory.iterdir() if not item.name.startswith('.')])
        
        for i, item in enumerate(items):
            is_last = i == len(items) - 1
            current_prefix = "└── " if is_last else "├── "
            print(f"{prefix}{current_prefix}{item.name}")
            
            if item.is_dir() and current_depth < max_depth - 1:
                next_prefix = prefix + ("    " if is_last else "│   ")
                print_tree(item, next_prefix, max_depth, current_depth + 1)
    
    if workers_dir.exists():
        print("src/Workers/")
        print_tree(workers_dir, "")
    else:
        print("❌ Workers directory not found")

def explain_remaining_files():
    """Explain what the remaining files do."""
    print("\n" + "=" * 60)
    print("📋 Remaining Files Explained:")
    print("=" * 60)
    
    explanations = {
        "✅ KEEP - Core Direct Communication": [
            "ml_worker_direct.py - New direct stdin/stdout worker",
            "core/base_worker.py - Base worker classes",
            "core/device_manager.py - Device management"
        ],
        "✅ KEEP - Active ML Workers": [
            "inference/sdxl_worker.py - Main SDXL inference",
            "inference/enhanced_sdxl_worker.py - Enhanced SDXL",
            "inference/pipeline_manager.py - Pipeline management",
            "inference/batch_processor.py - Batch processing",
            "inference/memory_optimizer.py - Memory optimization"
        ],
        "✅ KEEP - Model Management": [
            "models/model_loader.py - Model loading",
            "models/vae.py - VAE models",
            "models/unet.py - UNet models",
            "models/encoders.py - Text encoders",
            "models/tokenizers.py - Tokenizers"
        ],
        "✅ KEEP - Feature Workers": [
            "features/batch_manager.py - Batch processing",
            "features/controlnet_worker.py - ControlNet",
            "features/lora_worker.py - LoRA adapters",
            "features/upscaler_worker.py - Image upscaling",
            "features/vae_manager.py - VAE management",
            "features/scheduler_manager_clean.py - Scheduler management"
        ],
        "✅ KEEP - Processing Components": [
            "schedulers/ - All scheduler implementations",
            "postprocessing/ - Image post-processing",
            "conditioning/ - Input conditioning"
        ]
    }
    
    for category, files in explanations.items():
        print(f"\n{category}:")
        for file_desc in files:
            print(f"   • {file_desc}")

def main():
    """Main cleanup function."""
    print("🧹 Workers Directory Cleanup")
    print("=" * 60)
    print("Removing obsolete files after transitioning to direct communication...")
    print()
    
    # Ask for confirmation
    response = input("⚠️  This will permanently delete obsolete files. Continue? (y/N): ")
    if response.lower() != 'y':
        print("❌ Cleanup cancelled by user")
        return
    
    # Perform cleanup
    success = cleanup_obsolete_files()
    
    # Show results
    show_remaining_structure()
    explain_remaining_files()
    
    if success:
        print(f"\n🎉 Cleanup completed successfully!")
        print("✅ Workers directory is now clean and optimized")
        print("✅ Only files needed for direct communication remain")
    else:
        print(f"\n⚠️  Cleanup completed with some failures")
        print("ℹ️  Check the failed removals above")

if __name__ == "__main__":
    main()