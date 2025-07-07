"""
LoRA Management Module
======================

Handles loading, applying, and managing LoRA (Low-Rank Adaptation) models
for SDXL pipelines with dynamic weight adjustment and batch operations.
"""

import logging
import torch
from typing import Dict, Any, List, Optional, Tuple, Union
from pathlib import Path
from dataclasses import dataclass
from safetensors.torch import load_file

from diffusers.pipelines.stable_diffusion_xl.pipeline_stable_diffusion_xl import StableDiffusionXLPipeline
from diffusers.loaders.lora_pipeline import LoraLoaderMixin


@dataclass
class LoRAConfig:
    """Configuration for a LoRA model."""
    name: str
    path: str
    weight: float = 1.0
    adapter_name: Optional[str] = None
    is_loaded: bool = False


class LoRAManager:
    """
    Manages LoRA models for SDXL pipelines.
    
    Provides functionality to load, apply, and manage multiple LoRA models
    with dynamic weight adjustment and proper cleanup.
    """
    
    def __init__(self, lora_base_path: str = "./models/loras"):
        self.logger = logging.getLogger(__name__)
        self.lora_base_path = Path(lora_base_path)
        self.loaded_loras: Dict[str, LoRAConfig] = {}
        self.current_pipeline: Optional[StableDiffusionXLPipeline] = None
        
        # Ensure LoRA directory exists
        self.lora_base_path.mkdir(parents=True, exist_ok=True)
        
        # Supported file formats
        self.supported_formats = {".safetensors", ".pth", ".bin"}
    
    def set_pipeline(self, pipeline: StableDiffusionXLPipeline) -> None:
        """
        Set the pipeline to apply LoRAs to.
        
        Args:
            pipeline: SDXL pipeline instance
        """
        self.current_pipeline = pipeline
        self.logger.info("Pipeline set for LoRA management")
    
    def load_lora(self, name: str, path: Optional[str] = None, weight: float = 1.0, 
                  adapter_name: Optional[str] = None) -> bool:
        """
        Load a LoRA model.
        
        Args:
            name: LoRA identifier
            path: Path to LoRA file (optional, will search in base path)
            weight: LoRA weight/strength
            adapter_name: Custom adapter name (optional)
            
        Returns:
            True if loaded successfully
        """
        try:
            if not self.current_pipeline:
                raise ValueError("No pipeline set for LoRA loading")
            
            # Resolve LoRA path
            lora_path = self._resolve_lora_path(name, path)
            if not lora_path.exists():
                raise FileNotFoundError(f"LoRA file not found: {lora_path}")
            
            # Generate adapter name if not provided
            if adapter_name is None:
                adapter_name = f"lora_{name}_{len(self.loaded_loras)}"
            
            # Load LoRA into pipeline
            self.current_pipeline.load_lora_weights(
                str(lora_path.parent),
                weight_name=lora_path.name,
                adapter_name=adapter_name
            )
            
            # Store LoRA configuration
            lora_config = LoRAConfig(
                name=name,
                path=str(lora_path),
                weight=weight,
                adapter_name=adapter_name,
                is_loaded=True
            )
            self.loaded_loras[name] = lora_config
            
            self.logger.info(f"LoRA loaded: {name} with weight {weight}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to load LoRA {name}: {str(e)}")
            return False
    
    def apply_lora_weights(self, lora_weights: Dict[str, float]) -> bool:
        """
        Apply weights to loaded LoRAs.
        
        Args:
            lora_weights: Dictionary mapping LoRA names to weights
            
        Returns:
            True if weights applied successfully
        """
        try:
            if not self.current_pipeline:
                raise ValueError("No pipeline set")
            
            # Prepare adapter names and weights
            adapter_names = []
            adapter_weights = []
            
            for lora_name, weight in lora_weights.items():
                if lora_name in self.loaded_loras:
                    lora_config = self.loaded_loras[lora_name]
                    if lora_config.is_loaded:
                        adapter_names.append(lora_config.adapter_name)
                        adapter_weights.append(weight)
                        
                        # Update stored weight
                        lora_config.weight = weight
                else:
                    self.logger.warning(f"LoRA not loaded: {lora_name}")
            
            if adapter_names:
                # Set adapter weights
                self.current_pipeline.set_adapters(adapter_names, adapter_weights)
                self.logger.info(f"Applied weights to {len(adapter_names)} LoRAs")
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to apply LoRA weights: {str(e)}")
            return False
    
    def load_and_apply_loras(self, lora_configs: List[Dict[str, Any]]) -> bool:
        """
        Load and apply multiple LoRAs in batch.
        
        Args:
            lora_configs: List of LoRA configurations
            
        Returns:
            True if all LoRAs loaded and applied successfully
        """
        try:
            loaded_count = 0
            weights_to_apply = {}
            
            for config in lora_configs:
                name = config["name"]
                weight = config.get("weight", 1.0)
                path = config.get("path")
                adapter_name = config.get("adapter_name")
                
                if self.load_lora(name, path, weight, adapter_name):
                    weights_to_apply[name] = weight
                    loaded_count += 1
            
            if weights_to_apply:
                self.apply_lora_weights(weights_to_apply)
            
            self.logger.info(f"Loaded and applied {loaded_count}/{len(lora_configs)} LoRAs")
            return loaded_count == len(lora_configs)
            
        except Exception as e:
            self.logger.error(f"Failed to load and apply LoRAs: {str(e)}")
            return False
    
    def unload_lora(self, name: str) -> bool:
        """
        Unload a specific LoRA.
        
        Args:
            name: LoRA identifier
            
        Returns:
            True if unloaded successfully
        """
        try:
            if name not in self.loaded_loras:
                self.logger.warning(f"LoRA not loaded: {name}")
                return False
            
            lora_config = self.loaded_loras[name]
            
            if self.current_pipeline and lora_config.adapter_name:
                # Unload from pipeline
                self.current_pipeline.unload_lora_weights()
            
            # Remove from loaded LoRAs
            del self.loaded_loras[name]
            
            self.logger.info(f"LoRA unloaded: {name}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to unload LoRA {name}: {str(e)}")
            return False
    
    def unload_all_loras(self) -> bool:
        """
        Unload all LoRAs.
        
        Returns:
            True if all LoRAs unloaded successfully
        """
        try:
            if self.current_pipeline:
                # Unload all LoRA weights from pipeline
                self.current_pipeline.unload_lora_weights()
            
            # Clear loaded LoRAs
            unloaded_count = len(self.loaded_loras)
            self.loaded_loras.clear()
            
            self.logger.info(f"All {unloaded_count} LoRAs unloaded")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to unload all LoRAs: {str(e)}")
            return False
    
    def get_loaded_loras(self) -> List[Dict[str, Any]]:
        """
        Get information about loaded LoRAs.
        
        Returns:
            List of LoRA information dictionaries
        """
        return [
            {
                "name": config.name,
                "weight": config.weight,
                "adapter_name": config.adapter_name,
                "path": config.path,
                "is_loaded": config.is_loaded
            }
            for config in self.loaded_loras.values()
        ]
    
    def list_available_loras(self) -> List[str]:
        """
        List available LoRA files in the base directory.
        
        Returns:
            List of available LoRA names
        """
        loras = []
        
        for fmt in self.supported_formats:
            # Direct files in lora directory
            loras.extend([
                f.stem for f in self.lora_base_path.glob(f"*{fmt}")
            ])
            
            # Files in subdirectories
            for subdir in self.lora_base_path.iterdir():
                if subdir.is_dir():
                    loras.extend([
                        f"{subdir.name}/{f.stem}" for f in subdir.glob(f"*{fmt}")
                    ])
        
        return sorted(set(loras))
    
    def validate_lora_file(self, path: Union[str, Path]) -> bool:
        """
        Validate a LoRA file.
        
        Args:
            path: Path to LoRA file
            
        Returns:
            True if file is valid
        """
        try:
            file_path = Path(path)
            
            if not file_path.exists():
                return False
            
            if file_path.suffix not in self.supported_formats:
                return False
            
            # Try to load the file to validate format
            if file_path.suffix == ".safetensors":
                load_file(str(file_path))
            else:
                torch.load(str(file_path), map_location="cpu")
            
            return True
            
        except Exception as e:
            self.logger.warning(f"Invalid LoRA file {path}: {str(e)}")
            return False
    
    def get_lora_info(self, name: str) -> Optional[Dict[str, Any]]:
        """
        Get detailed information about a LoRA file.
        
        Args:
            name: LoRA identifier
            
        Returns:
            LoRA information dictionary or None if not found
        """
        try:
            lora_path = self._resolve_lora_path(name)
            
            if not lora_path.exists():
                return None
            
            # Load LoRA to get information
            if lora_path.suffix == ".safetensors":
                lora_data = load_file(str(lora_path))
            else:
                lora_data = torch.load(str(lora_path), map_location="cpu")
            
            # Calculate file size and parameter count
            file_size = lora_path.stat().st_size
            param_count = sum(tensor.numel() for tensor in lora_data.values())
            
            return {
                "name": name,
                "path": str(lora_path),
                "file_size_mb": file_size / (1024 * 1024),
                "parameter_count": param_count,
                "format": lora_path.suffix,
                "keys": list(lora_data.keys())[:10],  # First 10 keys
                "total_keys": len(lora_data.keys())
            }
            
        except Exception as e:
            self.logger.error(f"Failed to get LoRA info for {name}: {str(e)}")
            return None
    
    def _resolve_lora_path(self, name: str, custom_path: Optional[str] = None) -> Path:
        """
        Resolve the full path to a LoRA file.
        
        Args:
            name: LoRA name
            custom_path: Custom path if provided
            
        Returns:
            Path to LoRA file
        """
        if custom_path:
            return Path(custom_path)
        
        # Try to find in base directory with supported extensions
        for fmt in self.supported_formats:
            # Direct match
            direct_path = self.lora_base_path / f"{name}{fmt}"
            if direct_path.exists():
                return direct_path
            
            # Check subdirectories
            for subdir in self.lora_base_path.iterdir():
                if subdir.is_dir():
                    subdir_path = subdir / f"{name.split('/')[-1]}{fmt}"
                    if subdir_path.exists():
                        return subdir_path
        
        # If not found, return the most likely path for error reporting
        return self.lora_base_path / f"{name}.safetensors"
    
    def create_lora_preset(self, preset_name: str, lora_configs: List[Dict[str, Any]]) -> bool:
        """
        Create a LoRA preset for quick loading.
        
        Args:
            preset_name: Name of the preset
            lora_configs: List of LoRA configurations
            
        Returns:
            True if preset created successfully
        """
        try:
            preset_path = self.lora_base_path / f"{preset_name}_preset.json"
            
            import json
            with open(preset_path, 'w') as f:
                json.dump(lora_configs, f, indent=2)
            
            self.logger.info(f"LoRA preset created: {preset_name}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to create LoRA preset {preset_name}: {str(e)}")
            return False
    
    def load_lora_preset(self, preset_name: str) -> bool:
        """
        Load a LoRA preset.
        
        Args:
            preset_name: Name of the preset
            
        Returns:
            True if preset loaded successfully
        """
        try:
            preset_path = self.lora_base_path / f"{preset_name}_preset.json"
            
            if not preset_path.exists():
                self.logger.error(f"LoRA preset not found: {preset_name}")
                return False
            
            import json
            with open(preset_path, 'r') as f:
                lora_configs = json.load(f)
            
            return self.load_and_apply_loras(lora_configs)
            
        except Exception as e:
            self.logger.error(f"Failed to load LoRA preset {preset_name}: {str(e)}")
            return False
    
    def cleanup(self) -> None:
        """Clean up LoRA manager resources."""
        self.unload_all_loras()
        self.current_pipeline = None
        self.logger.info("LoRA manager cleaned up")
