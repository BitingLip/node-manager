"""
LoRA Worker - Phase 2 Days 15-16 Implementation
Advanced LoRA (Low-Rank Adaptation) adapter management for SDXL pipelines.

This worker provides comprehensive LoRA support including:
- LoRA model loading from safetensors/pt/ckpt files
- Dynamic weight application and adjustment
- Multiple LoRA adapter stacking
- Memory-efficient LoRA management
- Adapter cleanup and optimization

LoRA Integration Features:
- Safetensors format support (primary)
- PyTorch checkpoint support
- Automatic adapter detection
- Weight blending optimization
- Memory overhead monitoring
- Batch LoRA processing
"""

import asyncio
import logging
import torch
from typing import Dict, List, Optional, Any, Union, Tuple
from pathlib import Path
import json
import time
from dataclasses import dataclass, field
import safetensors.torch
import gc

# Set up logging
logger = logging.getLogger(__name__)

@dataclass
class LoRAConfiguration:
    """Configuration for a single LoRA adapter."""
    name: str
    path: str
    weight: float = 1.0
    adapter_name: Optional[str] = None
    enabled: bool = True
    
    # Metadata
    file_format: Optional[str] = None  # 'safetensors', 'pt', 'ckpt'
    file_size_mb: Optional[float] = None
    load_time_ms: Optional[float] = None
    memory_usage_mb: Optional[float] = None

@dataclass
class LoRAStackConfiguration:
    """Configuration for multiple LoRA adapters (stack)."""
    adapters: List[LoRAConfiguration] = field(default_factory=list)
    global_weight_multiplier: float = 1.0
    blend_mode: str = "additive"  # 'additive', 'weighted_average'
    max_adapters: int = 8
    memory_limit_mb: Optional[float] = None
    
    def add_adapter(self, adapter: LoRAConfiguration) -> bool:
        """Add a LoRA adapter to the stack."""
        if len(self.adapters) >= self.max_adapters:
            logger.warning(f"Cannot add adapter '{adapter.name}': stack limit reached ({self.max_adapters})")
            return False
        
        # Check for duplicate names
        existing_names = [a.name for a in self.adapters]
        if adapter.name in existing_names:
            logger.warning(f"Adapter '{adapter.name}' already exists in stack")
            return False
        
        self.adapters.append(adapter)
        logger.info(f"Added LoRA adapter '{adapter.name}' with weight {adapter.weight}")
        return True
    
    def remove_adapter(self, name: str) -> bool:
        """Remove a LoRA adapter from the stack."""
        original_count = len(self.adapters)
        self.adapters = [a for a in self.adapters if a.name != name]
        
        if len(self.adapters) < original_count:
            logger.info(f"Removed LoRA adapter '{name}' from stack")
            return True
        else:
            logger.warning(f"LoRA adapter '{name}' not found in stack")
            return False
    
    def get_adapter_names(self) -> List[str]:
        """Get list of adapter names in the stack."""
        return [a.name for a in self.adapters if a.enabled]
    
    def get_adapter_weights(self) -> List[float]:
        """Get list of adapter weights in the stack."""
        return [a.weight * self.global_weight_multiplier for a in self.adapters if a.enabled]

class LoRAWorker:
    """
    Advanced LoRA Worker for SDXL pipeline integration.
    
    Manages LoRA adapter loading, application, and optimization for enhanced
    SDXL generation with custom model adaptations.
    """
    
    def __init__(self, config: Optional[Dict] = None):
        """Initialize the LoRA Worker."""
        self.config = config or {}
        
        # LoRA management
        self.loaded_adapters: Dict[str, Any] = {}
        self.adapter_metadata: Dict[str, LoRAConfiguration] = {}
        self.current_stack: Optional[LoRAStackConfiguration] = None
        
        # Memory tracking for individual adapters
        self.memory_usage: Dict[str, float] = {}
        
        # File discovery cache
        self.discovered_files: Dict[str, Path] = {}
        
        # Paths and directories
        self.lora_dirs = self._get_lora_directories()
        self.cache_dir = Path(self.config.get("cache_dir", "cache/lora"))
        self.cache_dir.mkdir(parents=True, exist_ok=True)
        
        # Performance settings
        self.max_memory_mb = self.config.get("memory_limit_mb", self.config.get("max_memory_mb", 4096))  # Support both naming conventions
        self.enable_caching = self.config.get("enable_caching", True)
        self.cache_cleanup_threshold = self.config.get("cache_cleanup_threshold", 0.8)
        
        # Supported file formats
        self.supported_formats = ['.safetensors', '.pt', '.ckpt', '.bin']
        
        # Statistics and performance tracking
        self.stats = {
            "adapters_loaded": 0,
            "total_load_time_ms": 0.0,
            "memory_usage_mb": 0.0,
            "cache_hits": 0,
            "cache_misses": 0,
            "total_loads": 0
        }
        
        # Alias for compatibility
        self.performance_stats = self.stats
        
        logger.info(f"LoRA Worker initialized - Cache: {self.enable_caching}, Max Memory: {self.max_memory_mb}MB")
    
    def _get_lora_directories(self) -> List[Path]:
        """Get list of directories to search for LoRA files."""
        directories = []
        
        # Default LoRA directories
        default_dirs = [
            "models/lora",
            "models/LoRA", 
            "lora",
            "LoRA"
        ]
        
        for dir_path in default_dirs:
            path = Path(dir_path)
            if path.exists():
                directories.append(path)
                logger.debug(f"Found LoRA directory: {path}")
        
        # Custom directories from config
        custom_dirs = self.config.get("lora_directories", [])
        for dir_path in custom_dirs:
            path = Path(dir_path)
            if path.exists():
                directories.append(path)
                logger.debug(f"Added custom LoRA directory: {path}")
        
        if not directories:
            logger.warning("No LoRA directories found - creating default directory")
            default_path = Path("models/lora")
            default_path.mkdir(parents=True, exist_ok=True)
            directories.append(default_path)
        
        logger.info(f"LoRA Worker will search in {len(directories)} directories")
        return directories
    
    async def discover_lora_files(self) -> Dict[str, Path]:
        """Discover available LoRA files in configured directories."""
        discovered_files = {}
        
        for directory in self.lora_dirs:
            if not directory.exists():
                continue
            
            logger.debug(f"Searching directory: {directory}")
            
            # Search for LoRA files recursively
            for format_ext in self.supported_formats:
                for file_path in directory.rglob(f"*{format_ext}"):
                    if file_path.is_file():
                        # Use filename without extension as identifier
                        file_name = file_path.stem
                        
                        # Handle duplicate names by prefixing with parent directory
                        if file_name in discovered_files:
                            parent_name = file_path.parent.name
                            file_name = f"{parent_name}_{file_name}"
                        
                        discovered_files[file_name] = file_path
                        logger.debug(f"Discovered LoRA: {file_name} -> {file_path}")
        
        logger.info(f"Discovered {len(discovered_files)} LoRA files")
        return discovered_files
    
    def _resolve_lora_path(self, lora_identifier: str) -> Path:
        """
        Resolve LoRA identifier to actual file path.
        
        Args:
            lora_identifier: LoRA name, path, or identifier
            
        Returns:
            Path to the LoRA file
            
        Raises:
            FileNotFoundError: If LoRA file cannot be found
        """
        # If it's already a valid path, return it
        path = Path(lora_identifier)
        if path.exists() and path.is_file():
            return path
        
        # Search in LoRA directories
        for directory in self.lora_dirs:
            # Try exact name with different extensions
            for ext in self.supported_formats:
                candidate = directory / f"{lora_identifier}{ext}"
                if candidate.exists():
                    return candidate
                
                # Try in subdirectories
                for subdir in directory.iterdir():
                    if subdir.is_dir():
                        candidate = subdir / f"{lora_identifier}{ext}"
                        if candidate.exists():
                            return candidate
        
        raise FileNotFoundError(f"LoRA file not found: {lora_identifier}")
    
    def _detect_file_format(self, file_path: Path) -> str:
        """Detect the format of a LoRA file."""
        suffix = file_path.suffix.lower()
        
        if suffix == '.safetensors':
            return 'safetensors'
        elif suffix in ['.pt', '.pth']:
            return 'pt'
        elif suffix == '.ckpt':
            return 'ckpt'
        elif suffix == '.bin':
            return 'bin'
        else:
            logger.warning(f"Unknown LoRA file format: {suffix}")
            return 'unknown'
    
    async def load_lora_adapter(self, lora_config: LoRAConfiguration) -> bool:
        """
        Load a single LoRA adapter.
        
        Args:
            lora_config: LoRA configuration
            
        Returns:
            True if loading successful, False otherwise
        """
        start_time = time.time()
        
        try:
            # Resolve file path
            file_path = self._resolve_lora_path(lora_config.path)
            lora_config.file_format = self._detect_file_format(file_path)
            lora_config.file_size_mb = file_path.stat().st_size / (1024 * 1024)
            
            logger.info(f"Loading LoRA adapter: {lora_config.name} ({lora_config.file_format}, {lora_config.file_size_mb:.1f}MB)")
            
            # Check memory constraints
            if self._check_memory_constraints(lora_config.file_size_mb):
                await self._cleanup_cache_if_needed()
            
            # Load based on file format
            if lora_config.file_format == 'safetensors':
                adapter_data = self._load_safetensors(file_path)
            elif lora_config.file_format in ['pt', 'ckpt', 'bin']:
                adapter_data = self._load_pytorch(file_path)
            else:
                raise ValueError(f"Unsupported LoRA format: {lora_config.file_format}")
            
            # Store adapter data
            self.loaded_adapters[lora_config.name] = adapter_data
            self.adapter_metadata[lora_config.name] = lora_config
            
            # Calculate load time and memory usage
            load_time_ms = (time.time() - start_time) * 1000
            lora_config.load_time_ms = load_time_ms
            lora_config.memory_usage_mb = self._estimate_memory_usage(adapter_data)
            
            # Update statistics
            self.stats["adapters_loaded"] += 1
            self.stats["total_load_time_ms"] += load_time_ms
            self.stats["memory_usage_mb"] += lora_config.memory_usage_mb
            
            logger.info(f"✅ LoRA adapter '{lora_config.name}' loaded successfully "
                       f"({load_time_ms:.1f}ms, {lora_config.memory_usage_mb:.1f}MB)")
            
            return True
            
        except Exception as e:
            logger.error(f"❌ Failed to load LoRA adapter '{lora_config.name}': {e}")
            return False
    
    def _load_safetensors(self, file_path: Path) -> Dict[str, torch.Tensor]:
        """Load LoRA weights from safetensors file."""
        try:
            weights = safetensors.torch.load_file(str(file_path))
            logger.debug(f"Loaded {len(weights)} tensors from safetensors file")
            return weights
        except Exception as e:
            raise RuntimeError(f"Failed to load safetensors file: {e}")
    
    def _load_pytorch(self, file_path: Path) -> Dict[str, torch.Tensor]:
        """Load LoRA weights from PyTorch file."""
        try:
            checkpoint = torch.load(str(file_path), map_location='cpu')
            
            # Handle different checkpoint structures
            if isinstance(checkpoint, dict):
                if 'state_dict' in checkpoint:
                    weights = checkpoint['state_dict']
                elif 'model' in checkpoint:
                    weights = checkpoint['model']
                else:
                    weights = checkpoint
            else:
                weights = checkpoint
            
            logger.debug(f"Loaded {len(weights)} tensors from PyTorch file")
            return weights
            
        except Exception as e:
            raise RuntimeError(f"Failed to load PyTorch file: {e}")
    
    def _estimate_memory_usage(self, adapter_data: Dict[str, torch.Tensor]) -> float:
        """Estimate memory usage of loaded adapter data in MB."""
        total_bytes = 0
        
        for tensor in adapter_data.values():
            if isinstance(tensor, torch.Tensor):
                total_bytes += tensor.numel() * tensor.element_size()
        
        return total_bytes / (1024 * 1024)  # Convert to MB
    
    def _check_memory_constraints(self, additional_mb: float) -> bool:
        """Check if loading additional data would exceed memory limits."""
        current_usage = self.stats["memory_usage_mb"]
        projected_usage = current_usage + additional_mb
        
        if projected_usage > self.max_memory_mb:
            logger.warning(f"Memory constraint check: {projected_usage:.1f}MB > {self.max_memory_mb}MB limit")
            return True  # Need cleanup
        
        return False
    
    async def _cleanup_cache_if_needed(self) -> None:
        """Clean up cached adapters if memory usage is too high."""
        if not self.enable_caching:
            return
        
        current_usage_ratio = self.stats["memory_usage_mb"] / self.max_memory_mb
        
        if current_usage_ratio > self.cache_cleanup_threshold:
            logger.info(f"Starting cache cleanup - current usage: {current_usage_ratio:.1%}")
            
            # Remove least recently used adapters
            # For now, remove oldest loaded adapters
            adapters_to_remove = []
            target_usage = self.max_memory_mb * 0.5  # Target 50% usage
            
            for name, metadata in self.adapter_metadata.items():
                if self.stats["memory_usage_mb"] <= target_usage:
                    break
                adapters_to_remove.append(name)
            
            for name in adapters_to_remove:
                await self.unload_adapter(name)
            
            # Force garbage collection
            gc.collect()
            
            logger.info(f"Cache cleanup complete - removed {len(adapters_to_remove)} adapters")
    
    async def apply_lora_to_pipeline(self, pipeline: Any, lora_name: str, weight: float = 1.0) -> bool:
        """
        Apply a single LoRA adapter to a diffusion pipeline.
        
        Args:
            pipeline: Diffusion pipeline object
            lora_name: Name of the LoRA adapter
            weight: Weight to apply (default: 1.0)
            
        Returns:
            True if application successful, False otherwise
        """
        try:
            # Ensure adapter is loaded
            if lora_name not in self.loaded_adapters:
                # Try to load it first
                lora_config = LoRAConfiguration(name=lora_name, path=lora_name, weight=weight)
                if not await self.load_lora_adapter(lora_config):
                    return False
            
            # Get adapter file path for pipeline loading
            lora_path = self._resolve_lora_path(lora_name)
            
            # Apply to pipeline using diffusers LoRA loading
            if hasattr(pipeline, 'load_lora_weights'):
                pipeline.load_lora_weights(str(lora_path))
                logger.info(f"✅ LoRA weights loaded into pipeline: {lora_name}")
                
                # Set adapter weight if supported
                if hasattr(pipeline, 'set_adapters'):
                    pipeline.set_adapters([lora_name], adapter_weights=[weight])
                    logger.info(f"✅ LoRA adapter weight set: {weight}")
                
                return True
            else:
                logger.error("Pipeline does not support LoRA loading")
                return False
                
        except Exception as e:
            logger.error(f"❌ Failed to apply LoRA '{lora_name}' to pipeline: {e}")
            return False
    
    async def apply_lora_stack(self, pipeline: Any, stack_config: LoRAStackConfiguration) -> bool:
        """
        Apply multiple LoRA adapters to a diffusion pipeline.
        
        Args:
            pipeline: Diffusion pipeline object
            stack_config: Configuration for LoRA stack
            
        Returns:
            True if all adapters applied successfully, False otherwise
        """
        try:
            logger.info(f"Applying LoRA stack with {len(stack_config.adapters)} adapters")
            
            adapter_names = []
            adapter_weights = []
            failed_adapters = []
            
            # Load and prepare all adapters
            for adapter in stack_config.adapters:
                if not adapter.enabled:
                    continue
                
                # Ensure adapter is loaded
                if adapter.name not in self.loaded_adapters:
                    if not await self.load_lora_adapter(adapter):
                        failed_adapters.append(adapter.name)
                        continue
                
                try:
                    # Get adapter path
                    lora_path = self._resolve_lora_path(adapter.path)
                    
                    # Load weights into pipeline
                    if hasattr(pipeline, 'load_lora_weights'):
                        pipeline.load_lora_weights(str(lora_path), adapter_name=adapter.adapter_name or adapter.name)
                        
                        adapter_names.append(adapter.adapter_name or adapter.name)
                        final_weight = adapter.weight * stack_config.global_weight_multiplier
                        adapter_weights.append(final_weight)
                        
                        logger.info(f"✅ LoRA '{adapter.name}' prepared with weight {final_weight}")
                    else:
                        logger.error("Pipeline does not support LoRA loading")
                        failed_adapters.append(adapter.name)
                        
                except Exception as e:
                    logger.error(f"❌ Failed to prepare LoRA '{adapter.name}': {e}")
                    failed_adapters.append(adapter.name)
            
            # Apply all adapters at once if supported
            if adapter_names and hasattr(pipeline, 'set_adapters'):
                try:
                    pipeline.set_adapters(adapter_names, adapter_weights=adapter_weights)
                    logger.info(f"✅ LoRA stack applied: {len(adapter_names)} adapters")
                    
                    # Store current stack
                    self.current_stack = stack_config
                    
                    return len(failed_adapters) == 0
                    
                except Exception as e:
                    logger.error(f"❌ Failed to apply LoRA stack: {e}")
                    return False
            else:
                logger.warning("No adapters to apply or pipeline doesn't support multi-adapter mode")
                return False
                
        except Exception as e:
            logger.error(f"❌ LoRA stack application failed: {e}")
            return False
    
    async def unload_adapter(self, adapter_name: str) -> bool:
        """
        Unload a LoRA adapter from memory.
        
        Args:
            adapter_name: Name of the adapter to unload
            
        Returns:
            True if unloading successful, False otherwise
        """
        try:
            if adapter_name in self.loaded_adapters:
                # Get memory usage before cleanup
                metadata = self.adapter_metadata.get(adapter_name)
                memory_to_free = metadata.memory_usage_mb if metadata and metadata.memory_usage_mb else 0.0
                
                # Remove adapter data
                del self.loaded_adapters[adapter_name]
                
                if adapter_name in self.adapter_metadata:
                    del self.adapter_metadata[adapter_name]
                
                # Update statistics
                self.stats["memory_usage_mb"] -= memory_to_free
                
                logger.info(f"✅ LoRA adapter '{adapter_name}' unloaded ({memory_to_free:.1f}MB freed)")
                return True
            else:
                logger.warning(f"LoRA adapter '{adapter_name}' not found in loaded adapters")
                return False
                
        except Exception as e:
            logger.error(f"❌ Failed to unload LoRA adapter '{adapter_name}': {e}")
            return False
    
    async def unload_lora_adapter(self, adapter_name: str) -> bool:
        """Alias for unload_adapter for compatibility."""
        return await self.unload_adapter(adapter_name)
    
    async def clear_all_adapters(self) -> None:
        """Clear all loaded LoRA adapters from memory."""
        try:
            adapter_count = len(self.loaded_adapters)
            memory_freed = self.stats["memory_usage_mb"]
            
            # Clear all adapter data
            self.loaded_adapters.clear()
            self.adapter_metadata.clear()
            self.current_stack = None
            
            # Reset memory statistics
            self.stats["memory_usage_mb"] = 0
            
            # Force garbage collection
            gc.collect()
            
            logger.info(f"✅ All LoRA adapters cleared: {adapter_count} adapters, {memory_freed:.1f}MB freed")
            
        except Exception as e:
            logger.error(f"❌ Failed to clear LoRA adapters: {e}")
    
    def get_loaded_adapters(self) -> Dict[str, Dict[str, Any]]:
        """Get information about currently loaded adapters."""
        adapter_info = {}
        
        for name, metadata in self.adapter_metadata.items():
            adapter_info[name] = {
                "name": metadata.name,
                "path": metadata.path,
                "weight": metadata.weight,
                "file_format": metadata.file_format,
                "file_size_mb": metadata.file_size_mb,
                "load_time_ms": metadata.load_time_ms,
                "memory_usage_mb": metadata.memory_usage_mb,
                "enabled": metadata.enabled
            }
        
        return adapter_info
    
    def get_memory_stats(self) -> Dict[str, Any]:
        """Get current memory usage statistics."""
        return {
            "total_adapters_loaded": len(self.loaded_adapters),
            "current_memory_mb": self.stats["memory_usage_mb"],
            "max_memory_mb": self.max_memory_mb,
            "memory_usage_ratio": self.stats["memory_usage_mb"] / self.max_memory_mb,
            "cache_enabled": self.enable_caching,
            "statistics": self.stats.copy()
        }
    
    async def cleanup(self) -> None:
        """Cleanup LoRA Worker resources."""
        try:
            await self.clear_all_adapters()
            logger.info("LoRA Worker cleanup completed")
        except Exception as e:
            logger.error(f"LoRA Worker cleanup failed: {e}")
    
    async def apply_to_pipeline(self, pipeline, adapter_names: List[str]) -> bool:
        """
        Apply loaded LoRA adapters to a diffusion pipeline.
        
        Args:
            pipeline: The diffusion pipeline to apply adapters to
            adapter_names: List of adapter names to apply
            
        Returns:
            True if successful, False otherwise
        """
        try:
            if not adapter_names:
                return True
            
            # Verify all adapters are loaded
            for name in adapter_names:
                if name not in self.loaded_adapters:
                    logger.error(f"Adapter {name} not loaded")
                    return False
            
            # Get adapter weights
            adapter_weights = []
            for name in adapter_names:
                metadata = self.adapter_metadata.get(name)
                if metadata:
                    adapter_weights.append(metadata.weight)
                else:
                    adapter_weights.append(1.0)
            
            # Apply to pipeline
            if hasattr(pipeline, 'set_adapters'):
                pipeline.set_adapters(adapter_names, adapter_weights)
            elif hasattr(pipeline, 'load_lora_weights'):
                for name in adapter_names:
                    metadata = self.adapter_metadata[name]
                    pipeline.load_lora_weights(metadata.path, name, weight=metadata.weight)
            else:
                logger.warning("Pipeline does not support LoRA adapters")
                return False
            
            logger.info(f"Applied {len(adapter_names)} LoRA adapters to pipeline")
            return True
            
        except Exception as e:
            logger.error(f"Failed to apply LoRA adapters to pipeline: {e}")
            return False
    
    async def apply_stack_to_pipeline(self, pipeline) -> bool:
        """
        Apply the current LoRA stack to a diffusion pipeline.
        
        Args:
            pipeline: The diffusion pipeline to apply stack to
            
        Returns:
            True if successful, False otherwise
        """
        try:
            if not self.current_stack:
                logger.warning("No LoRA stack configured")
                return True
            
            adapter_names = self.current_stack.get_adapter_names()
            if not adapter_names:
                return True
            
            return await self.apply_to_pipeline(pipeline, adapter_names)
            
        except Exception as e:
            logger.error(f"Failed to apply LoRA stack to pipeline: {e}")
            return False

# Factory function for easy instantiation
def create_lora_worker(config: Optional[Dict] = None) -> LoRAWorker:
    """Create a LoRA Worker instance with the given configuration."""
    return LoRAWorker(config)
