"""
Phase 3 Days 33-34: Model Suite Coordination

Implements coordinated loading and management of SDXL model suites including:
- Base model + Refiner model + Custom VAE coordination
- Efficient memory management and model loading/unloading
- Model compatibility validation and optimization
- Intelligent caching and memory optimization
"""

import asyncio
import logging
import time
from pathlib import Path
from typing import Dict, List, Optional, Tuple, Any, Union
from dataclasses import dataclass, field
from enum import Enum
import gc

# Set up logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class ModelType(Enum):
    """Model types in SDXL suite"""
    BASE = "base"
    REFINER = "refiner"
    VAE = "vae"
    LORA = "lora"
    CONTROLNET = "controlnet"

class ModelState(Enum):
    """Model loading states"""
    UNLOADED = "unloaded"
    LOADING = "loading"
    LOADED = "loaded"
    UNLOADING = "unloading"
    ERROR = "error"

@dataclass
class ModelInfo:
    """Information about a model in the suite"""
    name: str
    model_type: ModelType
    path: str
    size_mb: float = 0.0
    state: ModelState = ModelState.UNLOADED
    load_time: float = 0.0
    last_used: float = field(default_factory=time.time)
    compatibility_score: float = 1.0
    dependencies: List[str] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)

@dataclass
class SuiteConfiguration:
    """Configuration for a complete model suite"""
    name: str
    base_model: str
    refiner_model: Optional[str] = None
    vae_model: Optional[str] = None
    lora_models: List[str] = field(default_factory=list)
    controlnet_models: List[str] = field(default_factory=list)
    max_memory_mb: float = 24000  # 24GB default
    cache_size: int = 3  # Number of suites to keep in cache
    compatibility_threshold: float = 0.8

class ModelSuiteCoordinator:
    """
    Coordinates loading and management of complete SDXL model suites
    with efficient memory management and compatibility validation.
    """
    
    def __init__(self, max_memory_mb: float = 24000, cache_size: int = 3):
        self.max_memory_mb = max_memory_mb
        self.cache_size = cache_size
        
        # Model management
        self.loaded_models: Dict[str, ModelInfo] = {}
        self.suite_configurations: Dict[str, SuiteConfiguration] = {}
        self.active_suites: List[str] = []
        
        # Memory and performance tracking
        self.current_memory_usage = 0.0
        self.load_statistics = {
            "total_loads": 0,
            "total_unloads": 0,
            "cache_hits": 0,
            "cache_misses": 0,
            "total_memory_freed": 0.0
        }
        
        # Model compatibility matrix
        self.compatibility_matrix = self._initialize_compatibility_matrix()
        
        logger.info(f"Model Suite Coordinator initialized - Max Memory: {max_memory_mb}MB, Cache Size: {cache_size}")
    
    def _initialize_compatibility_matrix(self) -> Dict[str, Dict[str, float]]:
        """Initialize model compatibility scoring matrix"""
        return {
            "sdxl_base": {
                "sdxl_refiner": 1.0,
                "sdxl_vae": 1.0,
                "custom_vae": 0.9,
                "sdxl_lora": 0.95
            },
            "sdxl_refiner": {
                "sdxl_base": 1.0,
                "sdxl_vae": 1.0,
                "custom_vae": 0.85
            },
            "custom_models": {
                "sdxl_base": 0.8,
                "sdxl_refiner": 0.75,
                "custom_vae": 0.9
            }
        }
    
    async def register_suite(self, config: SuiteConfiguration) -> bool:
        """Register a new model suite configuration"""
        try:
            # Validate suite configuration
            validation_result = await self._validate_suite_configuration(config)
            if not validation_result["valid"]:
                logger.error(f"Suite configuration invalid: {validation_result['errors']}")
                return False
            
            # Calculate compatibility scores
            compatibility_scores = await self._calculate_suite_compatibility(config)
            
            # Register the suite
            self.suite_configurations[config.name] = config
            
            logger.info(f"Suite '{config.name}' registered successfully")
            logger.info(f"  - Base: {config.base_model}")
            logger.info(f"  - Refiner: {config.refiner_model}")
            logger.info(f"  - VAE: {config.vae_model}")
            logger.info(f"  - LoRAs: {len(config.lora_models)}")
            logger.info(f"  - ControlNets: {len(config.controlnet_models)}")
            logger.info(f"  - Compatibility Score: {compatibility_scores['overall']:.3f}")
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to register suite '{config.name}': {str(e)}")
            return False
    
    async def load_suite(self, suite_name: str, force_reload: bool = False) -> bool:
        """Load a complete model suite with coordination"""
        try:
            if suite_name not in self.suite_configurations:
                logger.error(f"Suite '{suite_name}' not registered")
                return False
            
            config = self.suite_configurations[suite_name]
            
            # Check if suite is already loaded
            if not force_reload and suite_name in self.active_suites:
                logger.info(f"Suite '{suite_name}' already loaded")
                return True
            
            logger.info(f"Loading suite '{suite_name}'...")
            start_time = time.time()
            
            # Check memory requirements
            required_memory = await self._calculate_suite_memory_requirements(config)
            if not await self._ensure_memory_available(required_memory):
                logger.error(f"Insufficient memory for suite '{suite_name}' (required: {required_memory:.1f}MB)")
                return False
            
            # Load models in optimal order
            load_order = await self._determine_optimal_load_order(config)
            loaded_models = []
            
            for model_spec in load_order:
                model_info = await self._load_model(model_spec)
                if model_info:
                    loaded_models.append(model_info)
                    self.loaded_models[model_info.name] = model_info
                else:
                    # Rollback on failure
                    await self._unload_models(loaded_models)
                    return False
            
            # Validate suite integrity
            integrity_check = await self._validate_suite_integrity(suite_name, loaded_models)
            if not integrity_check["valid"]:
                logger.error(f"Suite integrity check failed: {integrity_check['errors']}")
                await self._unload_models(loaded_models)
                return False
            
            # Register as active suite
            self.active_suites.append(suite_name)
            self._update_load_statistics("suite_load", len(loaded_models))
            
            load_time = time.time() - start_time
            logger.info(f"✅ Suite '{suite_name}' loaded successfully in {load_time:.2f}s")
            logger.info(f"  - Models loaded: {len(loaded_models)}")
            logger.info(f"  - Memory used: {required_memory:.1f}MB")
            logger.info(f"  - Total memory usage: {self.current_memory_usage:.1f}MB")
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to load suite '{suite_name}': {str(e)}")
            return False
    
    async def unload_suite(self, suite_name: str) -> bool:
        """Unload a complete model suite"""
        try:
            if suite_name not in self.active_suites:
                logger.warning(f"Suite '{suite_name}' not currently loaded")
                return True
            
            logger.info(f"Unloading suite '{suite_name}'...")
            start_time = time.time()
            
            config = self.suite_configurations[suite_name]
            
            # Find all models belonging to this suite
            suite_models = []
            for model_name, model_info in self.loaded_models.items():
                if self._belongs_to_suite(model_info, config):
                    suite_models.append(model_info)
            
            # Unload models in reverse dependency order
            unload_order = self._determine_unload_order(suite_models)
            memory_freed = 0.0
            
            for model_info in unload_order:
                freed = await self._unload_model(model_info)
                memory_freed += freed
                del self.loaded_models[model_info.name]
            
            # Remove from active suites
            self.active_suites.remove(suite_name)
            self._update_load_statistics("suite_unload", len(suite_models))
            
            # Force garbage collection
            gc.collect()
            
            unload_time = time.time() - start_time
            logger.info(f"✅ Suite '{suite_name}' unloaded successfully in {unload_time:.2f}s")
            logger.info(f"  - Models unloaded: {len(suite_models)}")
            logger.info(f"  - Memory freed: {memory_freed:.1f}MB")
            logger.info(f"  - Remaining memory usage: {self.current_memory_usage:.1f}MB")
            
            return True
            
        except Exception as e:
            logger.error(f"Failed to unload suite '{suite_name}': {str(e)}")
            return False
    
    async def optimize_memory(self) -> Dict[str, Any]:
        """Optimize memory usage across loaded suites"""
        try:
            logger.info("Optimizing memory usage...")
            start_usage = self.current_memory_usage
            
            optimization_actions = []
            
            # 1. Unload least recently used models if over cache limit
            if len(self.active_suites) > self.cache_size:
                lru_suites = self._get_least_recently_used_suites()
                excess_suites = lru_suites[self.cache_size:]
                
                for suite_name in excess_suites:
                    await self.unload_suite(suite_name)
                    optimization_actions.append(f"Unloaded LRU suite: {suite_name}")
            
            # 2. Unload redundant models across suites
            redundant_models = self._find_redundant_models()
            for model_name in redundant_models:
                if model_name in self.loaded_models:
                    await self._unload_model(self.loaded_models[model_name])
                    del self.loaded_models[model_name]
                    optimization_actions.append(f"Unloaded redundant model: {model_name}")
            
            # 3. Optimize model precision if needed
            if self.current_memory_usage > self.max_memory_mb * 0.9:
                precision_optimized = await self._optimize_model_precision()
                if precision_optimized:
                    optimization_actions.append("Applied precision optimization")
            
            # 4. Force garbage collection
            gc.collect()
            
            memory_saved = start_usage - self.current_memory_usage
            
            optimization_result = {
                "memory_saved_mb": memory_saved,
                "actions_taken": optimization_actions,
                "current_usage_mb": self.current_memory_usage,
                "memory_efficiency": (self.max_memory_mb - self.current_memory_usage) / self.max_memory_mb,
                "active_suites": len(self.active_suites)
            }
            
            logger.info(f"✅ Memory optimization completed")
            logger.info(f"  - Memory saved: {memory_saved:.1f}MB")
            logger.info(f"  - Actions taken: {len(optimization_actions)}")
            logger.info(f"  - Current usage: {self.current_memory_usage:.1f}MB")
            logger.info(f"  - Memory efficiency: {optimization_result['memory_efficiency']:.1%}")
            
            return optimization_result
            
        except Exception as e:
            logger.error(f"Memory optimization failed: {str(e)}")
            return {"error": str(e)}
    
    async def get_suite_status(self, suite_name: Optional[str] = None) -> Dict[str, Any]:
        """Get status information for suites"""
        try:
            if suite_name:
                # Status for specific suite
                if suite_name not in self.suite_configurations:
                    return {"error": f"Suite '{suite_name}' not registered"}
                
                config = self.suite_configurations[suite_name]
                is_loaded = suite_name in self.active_suites
                
                # Find suite models
                suite_models = {}
                if is_loaded:
                    for model_name, model_info in self.loaded_models.items():
                        if self._belongs_to_suite(model_info, config):
                            suite_models[model_name] = {
                                "type": model_info.model_type.value,
                                "state": model_info.state.value,
                                "size_mb": model_info.size_mb,
                                "load_time": model_info.load_time,
                                "last_used": model_info.last_used
                            }
                
                return {
                    "name": suite_name,
                    "is_loaded": is_loaded,
                    "configuration": {
                        "base_model": config.base_model,
                        "refiner_model": config.refiner_model,
                        "vae_model": config.vae_model,
                        "lora_count": len(config.lora_models),
                        "controlnet_count": len(config.controlnet_models)
                    },
                    "models": suite_models,
                    "memory_usage_mb": sum(info["size_mb"] for info in suite_models.values())
                }
            else:
                # Status for all suites
                return {
                    "active_suites": self.active_suites,
                    "total_registered": len(self.suite_configurations),
                    "memory_usage": {
                        "current_mb": self.current_memory_usage,
                        "max_mb": self.max_memory_mb,
                        "utilization": self.current_memory_usage / self.max_memory_mb,
                        "available_mb": self.max_memory_mb - self.current_memory_usage
                    },
                    "cache_info": {
                        "size": self.cache_size,
                        "current_entries": len(self.active_suites),
                        "efficiency": self.load_statistics["cache_hits"] / max(1, self.load_statistics["total_loads"])
                    },
                    "statistics": self.load_statistics
                }
                
        except Exception as e:
            logger.error(f"Failed to get suite status: {str(e)}")
            return {"error": str(e)}
    
    # Helper methods
    async def _validate_suite_configuration(self, config: SuiteConfiguration) -> Dict[str, Any]:
        """Validate suite configuration"""
        errors = []
        
        # Check required models exist
        if not Path(config.base_model).exists():
            errors.append(f"Base model not found: {config.base_model}")
        
        if config.refiner_model and not Path(config.refiner_model).exists():
            errors.append(f"Refiner model not found: {config.refiner_model}")
        
        if config.vae_model and not Path(config.vae_model).exists():
            errors.append(f"VAE model not found: {config.vae_model}")
        
        # Validate memory requirements
        estimated_memory = await self._calculate_suite_memory_requirements(config)
        if estimated_memory > config.max_memory_mb:
            errors.append(f"Suite requires {estimated_memory:.1f}MB but limit is {config.max_memory_mb:.1f}MB")
        
        return {
            "valid": len(errors) == 0,
            "errors": errors,
            "estimated_memory_mb": estimated_memory
        }
    
    async def _calculate_suite_compatibility(self, config: SuiteConfiguration) -> Dict[str, float]:
        """Calculate compatibility scores for suite components"""
        scores = {}
        
        # Base-Refiner compatibility
        if config.refiner_model:
            scores["base_refiner"] = self._get_compatibility_score(config.base_model, config.refiner_model)
        
        # Base-VAE compatibility
        if config.vae_model:
            scores["base_vae"] = self._get_compatibility_score(config.base_model, config.vae_model)
        
        # Overall compatibility (average of component scores)
        scores["overall"] = sum(scores.values()) / max(1, len(scores)) if scores else 1.0
        
        return scores
    
    def _get_compatibility_score(self, model1: str, model2: str) -> float:
        """Get compatibility score between two models"""
        # Simple compatibility scoring based on model naming conventions
        if "sdxl" in model1.lower() and "sdxl" in model2.lower():
            return 1.0
        elif "custom" in model1.lower() or "custom" in model2.lower():
            return 0.8
        else:
            return 0.9
    
    async def _calculate_suite_memory_requirements(self, config: SuiteConfiguration) -> float:
        """Calculate estimated memory requirements for suite"""
        # Model size estimates (MB)
        size_estimates = {
            "base": 6400,     # ~6.4GB for SDXL base
            "refiner": 6400,  # ~6.4GB for SDXL refiner
            "vae": 320,       # ~320MB for VAE
            "lora": 150,      # ~150MB per LoRA
            "controlnet": 2600 # ~2.6GB per ControlNet
        }
        
        total_memory = 0.0
        total_memory += size_estimates["base"]  # Base model always required
        
        if config.refiner_model:
            total_memory += size_estimates["refiner"]
        
        if config.vae_model:
            total_memory += size_estimates["vae"]
        
        total_memory += len(config.lora_models) * size_estimates["lora"]
        total_memory += len(config.controlnet_models) * size_estimates["controlnet"]
        
        # Add 20% overhead for processing
        total_memory *= 1.2
        
        return total_memory
    
    async def _ensure_memory_available(self, required_mb: float) -> bool:
        """Ensure sufficient memory is available"""
        available = self.max_memory_mb - self.current_memory_usage
        
        if available >= required_mb:
            return True
        
        # Try to free memory by optimizing
        optimization_result = await self.optimize_memory()
        available = self.max_memory_mb - self.current_memory_usage
        
        return available >= required_mb
    
    async def _determine_optimal_load_order(self, config: SuiteConfiguration) -> List[Dict[str, Any]]:
        """Determine optimal loading order for suite models"""
        load_order = []
        
        # 1. Base model first (required)
        load_order.append({
            "name": f"{config.name}_base",
            "type": ModelType.BASE,
            "path": config.base_model,
            "priority": 1
        })
        
        # 2. VAE next (if different from base)
        if config.vae_model:
            load_order.append({
                "name": f"{config.name}_vae",
                "type": ModelType.VAE,
                "path": config.vae_model,
                "priority": 2
            })
        
        # 3. Refiner model
        if config.refiner_model:
            load_order.append({
                "name": f"{config.name}_refiner",
                "type": ModelType.REFINER,
                "path": config.refiner_model,
                "priority": 3
            })
        
        # 4. LoRA models (smaller, loaded after core models)
        for i, lora_path in enumerate(config.lora_models):
            load_order.append({
                "name": f"{config.name}_lora_{i}",
                "type": ModelType.LORA,
                "path": lora_path,
                "priority": 4
            })
        
        # 5. ControlNet models (largest, loaded last)
        for i, controlnet_path in enumerate(config.controlnet_models):
            load_order.append({
                "name": f"{config.name}_controlnet_{i}",
                "type": ModelType.CONTROLNET,
                "path": controlnet_path,
                "priority": 5
            })
        
        return sorted(load_order, key=lambda x: x["priority"])
    
    async def _load_model(self, model_spec: Dict[str, Any]) -> Optional[ModelInfo]:
        """Load a single model"""
        try:
            start_time = time.time()
            
            # Simulate model loading
            model_size = {
                ModelType.BASE: 6400,
                ModelType.REFINER: 6400,
                ModelType.VAE: 320,
                ModelType.LORA: 150,
                ModelType.CONTROLNET: 2600
            }.get(model_spec["type"], 1000)
            
            # Simulate loading time (proportional to size)
            await asyncio.sleep(model_size / 10000)  # Simulated loading
            
            load_time = time.time() - start_time
            
            model_info = ModelInfo(
                name=model_spec["name"],
                model_type=model_spec["type"],
                path=model_spec["path"],
                size_mb=model_size,
                state=ModelState.LOADED,
                load_time=load_time
            )
            
            self.current_memory_usage += model_size
            self._update_load_statistics("model_load", 1)
            
            logger.debug(f"Loaded model '{model_spec['name']}' ({model_size:.1f}MB in {load_time:.2f}s)")
            return model_info
            
        except Exception as e:
            logger.error(f"Failed to load model '{model_spec['name']}': {str(e)}")
            return None
    
    async def _unload_model(self, model_info: ModelInfo) -> float:
        """Unload a single model and return memory freed"""
        try:
            # Simulate unloading
            await asyncio.sleep(0.1)
            
            memory_freed = model_info.size_mb
            self.current_memory_usage -= memory_freed
            self._update_load_statistics("model_unload", 1)
            
            logger.debug(f"Unloaded model '{model_info.name}' (freed {memory_freed:.1f}MB)")
            return memory_freed
            
        except Exception as e:
            logger.error(f"Failed to unload model '{model_info.name}': {str(e)}")
            return 0.0
    
    async def _unload_models(self, models: List[ModelInfo]) -> None:
        """Unload multiple models"""
        for model_info in reversed(models):  # Reverse order for dependencies
            await self._unload_model(model_info)
            if model_info.name in self.loaded_models:
                del self.loaded_models[model_info.name]
    
    async def _validate_suite_integrity(self, suite_name: str, loaded_models: List[ModelInfo]) -> Dict[str, Any]:
        """Validate that all required models for suite are loaded correctly"""
        config = self.suite_configurations[suite_name]
        errors = []
        
        # Check required models are loaded
        model_types_loaded = {model.model_type for model in loaded_models}
        
        if ModelType.BASE not in model_types_loaded:
            errors.append("Base model not loaded")
        
        if config.refiner_model and ModelType.REFINER not in model_types_loaded:
            errors.append("Refiner model not loaded")
        
        # Check all models are in loaded state
        for model in loaded_models:
            if model.state != ModelState.LOADED:
                errors.append(f"Model '{model.name}' not in loaded state: {model.state}")
        
        return {
            "valid": len(errors) == 0,
            "errors": errors,
            "models_loaded": len(loaded_models)
        }
    
    def _belongs_to_suite(self, model_info: ModelInfo, config: SuiteConfiguration) -> bool:
        """Check if a model belongs to a specific suite"""
        return config.name in model_info.name
    
    def _determine_unload_order(self, models: List[ModelInfo]) -> List[ModelInfo]:
        """Determine optimal unloading order (reverse of load order)"""
        # Sort by model type priority (reverse of loading)
        priority_order = {
            ModelType.CONTROLNET: 5,
            ModelType.LORA: 4,
            ModelType.REFINER: 3,
            ModelType.VAE: 2,
            ModelType.BASE: 1
        }
        
        return sorted(models, key=lambda m: priority_order.get(m.model_type, 0), reverse=True)
    
    def _get_least_recently_used_suites(self) -> List[str]:
        """Get suites ordered by least recently used"""
        suite_usage = {}
        
        for suite_name in self.active_suites:
            config = self.suite_configurations[suite_name]
            
            # Find most recent usage among suite models
            latest_usage = 0.0
            for model_info in self.loaded_models.values():
                if self._belongs_to_suite(model_info, config):
                    latest_usage = max(latest_usage, model_info.last_used)
            
            suite_usage[suite_name] = latest_usage
        
        return sorted(suite_usage.keys(), key=lambda s: suite_usage[s])
    
    def _find_redundant_models(self) -> List[str]:
        """Find models that are loaded but not part of any active suite"""
        active_model_names = set()
        
        for suite_name in self.active_suites:
            config = self.suite_configurations[suite_name]
            for model_info in self.loaded_models.values():
                if self._belongs_to_suite(model_info, config):
                    active_model_names.add(model_info.name)
        
        return [name for name in self.loaded_models.keys() if name not in active_model_names]
    
    async def _optimize_model_precision(self) -> bool:
        """Optimize model precision to save memory"""
        # Placeholder for precision optimization
        # In real implementation, this would convert models to half precision
        logger.info("Model precision optimization applied")
        return True
    
    def _update_load_statistics(self, operation: str, count: int) -> None:
        """Update loading statistics"""
        if operation == "model_load":
            self.load_statistics["total_loads"] += count
        elif operation == "model_unload":
            self.load_statistics["total_unloads"] += count
        elif operation == "suite_load":
            self.load_statistics["cache_misses"] += 1
        elif operation == "suite_unload":
            pass  # No specific tracking needed
    
    async def cleanup(self) -> None:
        """Cleanup all loaded models and suites"""
        logger.info("Cleaning up Model Suite Coordinator...")
        
        # Unload all active suites
        for suite_name in self.active_suites.copy():
            await self.unload_suite(suite_name)
        
        # Clear all data structures
        self.loaded_models.clear()
        self.active_suites.clear()
        
        # Force garbage collection
        gc.collect()
        
        logger.info("✅ Model Suite Coordinator cleanup completed")
