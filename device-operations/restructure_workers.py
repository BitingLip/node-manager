#!/usr/bin/env python3
"""
Workers Folder Restructuring Script
===================================

Restructures the /Workers folder for better organization, clarity, and separation of concerns.
Maintains all functionality while improving structure.
"""

import os
import shutil
from pathlib import Path
import re

class WorkersRestructurer:
    def __init__(self, workers_dir: Path):
        self.workers_dir = workers_dir
        self.backup_dir = workers_dir.parent / "workers_backup"
        self.moves_performed = []
        self.consolidations_performed = []
        self.errors = []

    def create_backup(self):
        """Create backup of current workers directory."""
        print("📦 Creating backup...")
        if self.backup_dir.exists():
            shutil.rmtree(self.backup_dir)
        shutil.copytree(self.workers_dir, self.backup_dir)
        print(f"✅ Backup created: {self.backup_dir}")

    def create_new_directories(self):
        """Create new directory structure."""
        print("\n📁 Creating new directory structure...")
        
        new_dirs = [
            "models/adapters",
            "coordination", 
            "testing",
            "docs",
            "docs/completion_summaries"
        ]
        
        for dir_path in new_dirs:
            full_path = self.workers_dir / dir_path
            full_path.mkdir(parents=True, exist_ok=True)
            print(f"✅ Created: {dir_path}/")

    def consolidate_lora_management(self):
        """Consolidate LoRA management into single location."""
        print("\n🔧 Consolidating LoRA management...")
        
        try:
            # Create adapters directory
            adapters_dir = self.workers_dir / "models" / "adapters"
            adapters_dir.mkdir(exist_ok=True)
            
            # Move main LoRA manager
            main_lora = self.workers_dir / "models" / "lora_manager.py"
            target_lora = adapters_dir / "lora_manager.py"
            
            if main_lora.exists():
                shutil.move(str(main_lora), str(target_lora))
                self.consolidations_performed.append("LoRA: models/lora_manager.py → models/adapters/lora_manager.py")
                print("✅ Moved main LoRA manager to adapters/")
            
            # Remove duplicate LoRA manager in conditioning
            duplicate_lora = self.workers_dir / "conditioning" / "lora_manager.py"
            if duplicate_lora.exists():
                duplicate_lora.unlink()
                self.consolidations_performed.append("LoRA: Removed duplicate conditioning/lora_manager.py")
                print("✅ Removed duplicate LoRA manager")
                
            # Create __init__.py in adapters
            init_file = adapters_dir / "__init__.py"
            init_file.write_text('"""Adapter management modules."""\n')
            
        except Exception as e:
            self.errors.append(f"LoRA consolidation: {str(e)}")
            print(f"❌ Error consolidating LoRA: {str(e)}")

    def consolidate_batch_processing(self):
        """Consolidate batch processing functionality."""
        print("\n🔧 Consolidating batch processing...")
        
        try:
            enhanced_batch = self.workers_dir / "features" / "batch_manager.py"
            basic_batch = self.workers_dir / "inference" / "batch_processor.py"
            target_batch = self.workers_dir / "inference" / "batch_manager.py"
            
            # Keep the enhanced version and remove the basic one
            if enhanced_batch.exists():
                shutil.move(str(enhanced_batch), str(target_batch))
                self.consolidations_performed.append("Batch: features/batch_manager.py → inference/batch_manager.py")
                print("✅ Moved enhanced batch manager to inference/")
            
            if basic_batch.exists():
                basic_batch.unlink()
                self.consolidations_performed.append("Batch: Removed basic inference/batch_processor.py")
                print("✅ Removed basic batch processor")
                
        except Exception as e:
            self.errors.append(f"Batch consolidation: {str(e)}")
            print(f"❌ Error consolidating batch processing: {str(e)}")

    def consolidate_vae_management(self):
        """Consolidate VAE management."""
        print("\n🔧 Consolidating VAE management...")
        
        try:
            enhanced_vae = self.workers_dir / "features" / "vae_manager.py"
            basic_vae = self.workers_dir / "models" / "vae.py"
            target_vae = self.workers_dir / "models" / "vae_manager.py"
            
            # Keep the enhanced version
            if enhanced_vae.exists():
                shutil.move(str(enhanced_vae), str(target_vae))
                self.consolidations_performed.append("VAE: features/vae_manager.py → models/vae_manager.py")
                print("✅ Moved enhanced VAE manager to models/")
            
            if basic_vae.exists():
                basic_vae.unlink()
                self.consolidations_performed.append("VAE: Removed basic models/vae.py")
                print("✅ Removed basic VAE module")
                
        except Exception as e:
            self.errors.append(f"VAE consolidation: {str(e)}")
            print(f"❌ Error consolidating VAE: {str(e)}")

    def move_workers_to_correct_categories(self):
        """Move workers to their correct categories."""
        print("\n📦 Moving workers to correct categories...")
        
        moves = [
            # Inference workers
            ("features/controlnet_worker.py", "inference/controlnet_worker.py"),
            ("features/lora_worker.py", "inference/lora_worker.py"),
            
            # Postprocessing workers  
            ("features/upscaler_worker.py", "postprocessing/upscaler_worker.py"),
            
            # Coordination components
            ("ml_worker_direct.py", "coordination/ml_worker_direct.py"),
            ("features/model_suite_coordinator.py", "coordination/model_suite_coordinator.py"),
            ("features/sdxl_refiner_pipeline.py", "coordination/sdxl_refiner_pipeline.py"),
            
            # Testing utilities
            ("features/comprehensive_testing.py", "testing/comprehensive_testing.py"),
        ]
        
        for source, target in moves:
            try:
                source_path = self.workers_dir / source
                target_path = self.workers_dir / target
                
                if source_path.exists():
                    # Ensure target directory exists
                    target_path.parent.mkdir(parents=True, exist_ok=True)
                    
                    shutil.move(str(source_path), str(target_path))
                    self.moves_performed.append(f"{source} → {target}")
                    print(f"✅ Moved: {source} → {target}")
                else:
                    print(f"ℹ️  Not found: {source}")
                    
            except Exception as e:
                self.errors.append(f"Move {source}: {str(e)}")
                print(f"❌ Error moving {source}: {str(e)}")

    def move_documentation(self):
        """Move documentation files to docs directory."""
        print("\n📚 Moving documentation...")
        
        doc_moves = [
            ("features/api_documentation.md", "docs/api_documentation.md"),
            ("features/deployment_instructions.md", "docs/deployment_instructions.md"),
            ("features/performance_guide.md", "docs/performance_guide.md"),
            ("features/troubleshooting_guide.md", "docs/troubleshooting_guide.md"),
            ("features/PHASE3_WEEK6_COMPLETION_SUMMARY.md", "docs/completion_summaries/PHASE3_WEEK6_COMPLETION_SUMMARY.md"),
        ]
        
        for source, target in doc_moves:
            try:
                source_path = self.workers_dir / source
                target_path = self.workers_dir / target
                
                if source_path.exists():
                    # Ensure target directory exists
                    target_path.parent.mkdir(parents=True, exist_ok=True)
                    
                    shutil.move(str(source_path), str(target_path))
                    self.moves_performed.append(f"📚 {source} → {target}")
                    print(f"✅ Moved: {source} → {target}")
                else:
                    print(f"ℹ️  Not found: {source}")
                    
            except Exception as e:
                self.errors.append(f"Doc move {source}: {str(e)}")
                print(f"❌ Error moving {source}: {str(e)}")

    def clean_up_naming(self):
        """Clean up file naming."""
        print("\n🏷️  Cleaning up naming...")
        
        try:
            # Rename scheduler_manager_clean.py to scheduler_manager.py
            old_scheduler = self.workers_dir / "features" / "scheduler_manager_clean.py"
            new_scheduler = self.workers_dir / "schedulers" / "scheduler_manager.py"
            
            if old_scheduler.exists():
                shutil.move(str(old_scheduler), str(new_scheduler))
                self.moves_performed.append("🏷️  features/scheduler_manager_clean.py → schedulers/scheduler_manager.py")
                print("✅ Renamed scheduler manager")
            
        except Exception as e:
            self.errors.append(f"Naming cleanup: {str(e)}")
            print(f"❌ Error with naming cleanup: {str(e)}")

    def clean_up_empty_directories(self):
        """Remove empty directories."""
        print("\n🗑️  Cleaning up empty directories...")
        
        try:
            features_dir = self.workers_dir / "features"
            if features_dir.exists():
                # Check if features directory is empty or only has __init__.py
                remaining_files = [f for f in features_dir.iterdir() if f.name != "__init__.py"]
                if not remaining_files:
                    shutil.rmtree(features_dir)
                    print("✅ Removed empty features/ directory")
                else:
                    print(f"ℹ️  Features directory not empty, contains: {[f.name for f in remaining_files]}")
            
        except Exception as e:
            self.errors.append(f"Directory cleanup: {str(e)}")
            print(f"❌ Error cleaning directories: {str(e)}")

    def create_init_files(self):
        """Create __init__.py files for new directories."""
        print("\n📄 Creating __init__.py files...")
        
        init_files = {
            "coordination/__init__.py": '"""High-level coordination and orchestration components."""\n',
            "testing/__init__.py": '"""Testing utilities and comprehensive test suites."""\n',
            "docs/__init__.py": '"""Documentation and guides."""\n',
            "models/adapters/__init__.py": '"""Model adapter management (LoRA, textual inversions, etc.)."""\n'
        }
        
        for file_path, content in init_files.items():
            try:
                full_path = self.workers_dir / file_path
                full_path.write_text(content)
                print(f"✅ Created: {file_path}")
            except Exception as e:
                self.errors.append(f"Init file {file_path}: {str(e)}")
                print(f"❌ Error creating {file_path}: {str(e)}")

    def update_imports_preview(self):
        """Preview what import updates would be needed."""
        print("\n🔄 Import updates needed (manual step):")
        
        import_updates = [
            "features.controlnet_worker → inference.controlnet_worker",
            "features.lora_worker → inference.lora_worker", 
            "features.batch_manager → inference.batch_manager",
            "features.upscaler_worker → postprocessing.upscaler_worker",
            "features.vae_manager → models.vae_manager",
            "models.lora_manager → models.adapters.lora_manager",
            "ml_worker_direct → coordination.ml_worker_direct",
        ]
        
        for update in import_updates:
            print(f"  📝 {update}")
        
        print("\n💡 After restructuring, update these imports in your C# and Python code!")

    def print_summary(self):
        """Print restructuring summary."""
        print("\n" + "="*60)
        print("📊 RESTRUCTURING SUMMARY")
        print("="*60)
        
        print(f"✅ Moves performed: {len(self.moves_performed)}")
        if self.moves_performed:
            for move in self.moves_performed:
                print(f"   • {move}")
        
        print(f"\n✅ Consolidations performed: {len(self.consolidations_performed)}")
        if self.consolidations_performed:
            for consolidation in self.consolidations_performed:
                print(f"   • {consolidation}")
        
        print(f"\n❌ Errors encountered: {len(self.errors)}")
        if self.errors:
            for error in self.errors:
                print(f"   • {error}")
        
        print(f"\n📦 Backup location: {self.backup_dir}")
        print("\n🎉 Restructuring completed!")

    def run_restructure(self):
        """Run the complete restructuring process."""
        print("🔧 Workers Folder Restructuring")
        print("="*60)
        print("Improving organization, clarity, and separation of concerns...")
        print()
        
        # Create backup first
        self.create_backup()
        
        # Perform restructuring steps
        self.create_new_directories()
        self.consolidate_lora_management()
        self.consolidate_batch_processing() 
        self.consolidate_vae_management()
        self.move_workers_to_correct_categories()
        self.move_documentation()
        self.clean_up_naming()
        self.create_init_files()
        self.clean_up_empty_directories()
        self.update_imports_preview()
        
        # Print summary
        self.print_summary()

def main():
    """Main restructuring function."""
    workers_dir = Path(__file__).parent / "src" / "Workers"
    
    if not workers_dir.exists():
        print(f"❌ Workers directory not found: {workers_dir}")
        return
    
    print(f"📍 Workers directory: {workers_dir}")
    print()
    
    # Ask for confirmation
    response = input("⚠️  This will restructure the Workers folder. Continue? (y/N): ")
    if response.lower() != 'y':
        print("❌ Restructuring cancelled by user")
        return
    
    # Run restructuring
    restructurer = WorkersRestructurer(workers_dir)
    restructurer.run_restructure()

if __name__ == "__main__":
    main()