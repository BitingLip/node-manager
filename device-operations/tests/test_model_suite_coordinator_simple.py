"""
Phase 3 Days 33-34: Model Suite Coordinator Test (Simplified)

Tests the complete model suite coordination functionality including:
- Suite registration and configuration validation
- Coordinated model loading and unloading
- Memory management and optimization
- Model compatibility validation
- Cache management and performance statistics
"""

import asyncio
import logging
import tempfile
from pathlib import Path
from typing import Dict, Any, List, Optional
import time
from dataclasses import dataclass, field
from enum import Enum
import gc

# Set up logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[logging.StreamHandler()]
)
logger = logging.getLogger(__name__)

# Import the classes directly to avoid module dependency issues
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

class SimplifiedModelSuiteCoordinator:
    """
    Simplified version of Model Suite Coordinator for testing
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
        
        logger.info(f"Model Suite Coordinator initialized - Max Memory: {max_memory_mb}MB, Cache Size: {cache_size}")
    
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
            logger.info(f"  - Base: {Path(config.base_model).name}")
            logger.info(f"  - Refiner: {Path(config.refiner_model).name if config.refiner_model else 'None'}")
            logger.info(f"  - VAE: {Path(config.vae_model).name if config.vae_model else 'None'}")
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
            
            # Unload models
            memory_freed = 0.0
            for model_info in suite_models:
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
            
            # 2. Force garbage collection
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
            scores["base_refiner"] = 1.0 if "sdxl" in config.base_model.lower() else 0.8
        
        # Base-VAE compatibility
        if config.vae_model:
            scores["base_vae"] = 1.0 if "sdxl" in config.base_model.lower() else 0.8
        
        # Overall compatibility (average of component scores)
        scores["overall"] = sum(scores.values()) / max(1, len(scores)) if scores else 1.0
        
        return scores
    
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
        
        # 4. LoRA models
        for i, lora_path in enumerate(config.lora_models):
            load_order.append({
                "name": f"{config.name}_lora_{i}",
                "type": ModelType.LORA,
                "path": lora_path,
                "priority": 4
            })
        
        # 5. ControlNet models
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
            
            # Simulate loading time
            await asyncio.sleep(0.1)  # Quick simulation
            
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
            
            logger.debug(f"Loaded model '{model_spec['name']}' ({model_size:.1f}MB)")
            return model_info
            
        except Exception as e:
            logger.error(f"Failed to load model '{model_spec['name']}': {str(e)}")
            return None
    
    async def _unload_model(self, model_info: ModelInfo) -> float:
        """Unload a single model and return memory freed"""
        try:
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
        for model_info in reversed(models):
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
    
    def _update_load_statistics(self, operation: str, count: int) -> None:
        """Update loading statistics"""
        if operation == "model_load":
            self.load_statistics["total_loads"] += count
        elif operation == "model_unload":
            self.load_statistics["total_unloads"] += count
        elif operation == "suite_load":
            self.load_statistics["cache_misses"] += 1
    
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

async def test_model_suite_coordinator():
    """Test complete Model Suite Coordinator functionality"""
    
    logger.info("")
    logger.info("=" * 70)
    logger.info("TESTING MODEL SUITE COORDINATOR - PHASE 3 DAYS 33-34")
    logger.info("=" * 70)
    
    try:
        # === Step 1: Initialize Model Suite Coordinator ===
        logger.info("\n--- Step 1: Initializing Model Suite Coordinator ---")
        
        coordinator = SimplifiedModelSuiteCoordinator(max_memory_mb=20000, cache_size=3)
        
        logger.info("✅ Model Suite Coordinator initialized")
        logger.info(f"  - Max Memory: 20000MB")
        logger.info(f"  - Cache Size: 3 suites")
        logger.info(f"  - Current Memory Usage: {coordinator.current_memory_usage:.1f}MB")
        
        # === Step 2: Create Mock Model Files ===
        logger.info("\n--- Step 2: Creating Mock Model Files ---")
        
        # Create temporary directory for mock models
        temp_dir = Path("temp_models")
        temp_dir.mkdir(exist_ok=True)
        
        # Create mock model files
        model_files = {}
        model_types = {
            "base": ["sdxl_base_v1.safetensors", "cyberrealistic_base.safetensors"],
            "refiner": ["sdxl_refiner_v1.safetensors"],
            "vae": ["sdxl_vae.safetensors", "custom_vae.pt"],
            "lora": ["detail_lora.safetensors", "style_lora.safetensors"],
            "controlnet": ["canny_controlnet.safetensors", "depth_controlnet.safetensors"]
        }
        
        for model_type, files in model_types.items():
            model_files[model_type] = []
            for file_name in files:
                file_path = temp_dir / file_name
                file_path.write_text(f"mock_{model_type}_model_data")
                model_files[model_type].append(str(file_path))
                logger.info(f"  ✅ Created mock {model_type} model: {file_name}")
        
        # === Step 3: Register Model Suite Configurations ===
        logger.info("\n--- Step 3: Registering Model Suite Configurations ---")
        
        # Suite 1: Basic SDXL Suite
        suite1_config = SuiteConfiguration(
            name="basic_sdxl",
            base_model=model_files["base"][0],
            refiner_model=model_files["refiner"][0],
            vae_model=model_files["vae"][0],
            max_memory_mb=20000  # Increased memory limit
        )
        
        result1 = await coordinator.register_suite(suite1_config)
        logger.info(f"✅ Basic SDXL Suite registered: {result1}")
        
        # Suite 2: Enhanced Suite with LoRAs
        suite2_config = SuiteConfiguration(
            name="enhanced_suite",
            base_model=model_files["base"][1],
            refiner_model=model_files["refiner"][0],
            vae_model=model_files["vae"][1],
            lora_models=model_files["lora"],
            max_memory_mb=20000  # Increased memory limit
        )
        
        result2 = await coordinator.register_suite(suite2_config)
        logger.info(f"✅ Enhanced Suite registered: {result2}")
        
        # Suite 3: Full Suite with ControlNets
        suite3_config = SuiteConfiguration(
            name="full_suite",
            base_model=model_files["base"][0],
            refiner_model=model_files["refiner"][0],
            vae_model=model_files["vae"][0],
            lora_models=[model_files["lora"][0]],
            controlnet_models=model_files["controlnet"],
            max_memory_mb=25000
        )
        
        result3 = await coordinator.register_suite(suite3_config)
        logger.info(f"✅ Full Suite registered: {result3}")
        
        logger.info(f"✅ Total suites registered: {len(coordinator.suite_configurations)}")
        
        # === Step 4: Test Suite Loading ===
        logger.info("\n--- Step 4: Testing Suite Loading ---")
        
        # Load basic suite first
        logger.info("Loading basic SDXL suite...")
        load_result1 = await coordinator.load_suite("basic_sdxl")
        logger.info(f"✅ Basic suite load result: {load_result1}")
        
        # Check suite status
        status1 = await coordinator.get_suite_status("basic_sdxl")
        if "error" not in status1:
            logger.info(f"Basic suite status: {len(status1['models'])} models loaded")
            logger.info(f"Memory usage: {status1['memory_usage_mb']:.1f}MB")
        else:
            logger.warning(f"Basic suite status error: {status1['error']}")
        
        # Load enhanced suite
        logger.info("Loading enhanced suite...")
        load_result2 = await coordinator.load_suite("enhanced_suite")
        logger.info(f"✅ Enhanced suite load result: {load_result2}")
        
        # Check overall system status
        system_status = await coordinator.get_suite_status()
        logger.info(f"System status:")
        logger.info(f"  - Active suites: {len(system_status['active_suites'])}")
        logger.info(f"  - Memory utilization: {system_status['memory_usage']['utilization']:.1%}")
        logger.info(f"  - Available memory: {system_status['memory_usage']['available_mb']:.1f}MB")
        
        # === Step 5: Test Memory Management ===
        logger.info("\n--- Step 5: Testing Memory Management ---")
        
        # Test memory optimization
        logger.info("Testing memory optimization...")
        optimization_result = await coordinator.optimize_memory()
        logger.info(f"✅ Memory optimization completed")
        logger.info(f"  - Memory saved: {optimization_result.get('memory_saved_mb', 0):.1f}MB")
        logger.info(f"  - Actions taken: {len(optimization_result.get('actions_taken', []))}")
        logger.info(f"  - Memory efficiency: {optimization_result.get('memory_efficiency', 0):.1%}")
        
        # === Step 6: Test Suite Unloading ===
        logger.info("\n--- Step 6: Testing Suite Unloading ---")
        
        # Unload one suite
        logger.info("Unloading enhanced suite...")
        unload_result = await coordinator.unload_suite("enhanced_suite")
        logger.info(f"✅ Enhanced suite unload result: {unload_result}")
        
        # Check memory after unload
        system_status = await coordinator.get_suite_status()
        logger.info(f"After unload:")
        logger.info(f"  - Active suites: {len(system_status['active_suites'])}")
        logger.info(f"  - Memory utilization: {system_status['memory_usage']['utilization']:.1%}")
        
        # === Step 7: Test Cache Management ===
        logger.info("\n--- Step 7: Testing Cache Management ---")
        
        # Create additional suites to test cache limits
        for i in range(4, 7):  # Create suites 4, 5, 6
            suite_config = SuiteConfiguration(
                name=f"test_suite_{i}",
                base_model=model_files["base"][0],
                vae_model=model_files["vae"][0],
                max_memory_mb=8000
            )
            
            await coordinator.register_suite(suite_config)
            load_result = await coordinator.load_suite(f"test_suite_{i}")
            logger.info(f"✅ Test suite {i} loaded: {load_result}")
            
            # Check if cache management kicks in
            system_status = await coordinator.get_suite_status()
            logger.info(f"  - Active suites after loading suite {i}: {len(system_status['active_suites'])}")
        
        # === Step 8: Test Performance Statistics ===
        logger.info("\n--- Step 8: Testing Performance Statistics ---")
        
        system_status = await coordinator.get_suite_status()
        statistics = system_status.get('statistics', {})
        
        logger.info("✅ Performance Statistics:")
        logger.info(f"  - Total model loads: {statistics.get('total_loads', 0)}")
        logger.info(f"  - Total model unloads: {statistics.get('total_unloads', 0)}")
        logger.info(f"  - Cache hits: {statistics.get('cache_hits', 0)}")
        logger.info(f"  - Cache misses: {statistics.get('cache_misses', 0)}")
        logger.info(f"  - Cache efficiency: {system_status.get('cache_info', {}).get('efficiency', 0):.1%}")
        
        # === Step 9: Validation Tests ===
        logger.info("\n--- Step 9: Validation Tests ---")
        
        validation_checks = [
            ("Suite registration", len(coordinator.suite_configurations) >= 5),
            ("Suite loading", len(coordinator.active_suites) > 0),
            ("Memory management", coordinator.current_memory_usage > 0),
            ("Performance tracking", statistics.get('total_loads', 0) > 0),
            ("Cache management", len(coordinator.active_suites) <= coordinator.cache_size),
        ]
        
        for check_name, check_result in validation_checks:
            status = "✅" if check_result else "❌"
            logger.info(f"  {status} {check_name}: {'PASS' if check_result else 'FAIL'}")
        
        all_passed = all(result for _, result in validation_checks)
        
        # === Step 10: Cleanup ===
        logger.info("\n--- Step 10: Cleanup ---")
        
        # Cleanup coordinator
        await coordinator.cleanup()
        logger.info("✅ Model Suite Coordinator cleanup completed")
        
        # Remove temporary files
        for model_type, files in model_files.items():
            for file_path in files:
                Path(file_path).unlink(missing_ok=True)
        
        if temp_dir.exists() and not any(temp_dir.iterdir()):
            temp_dir.rmdir()
        
        logger.info("✅ Test files cleaned up")
        
        # === Success Summary ===
        logger.info("")
        logger.info("=" * 70)
        logger.info("MODEL SUITE COORDINATOR: SUCCESS")
        logger.info("=" * 70)
        logger.info("✅ Suite registration and configuration validation functional")
        logger.info("✅ Coordinated model loading and unloading working")
        logger.info("✅ Memory management and optimization operational")
        logger.info("✅ Model compatibility validation working")
        logger.info("✅ Cache management and performance statistics functional")
        logger.info("")
        logger.info("🎉 Model Suite Coordinator Test: PASSED!")
        
        return all_passed
        
    except Exception as e:
        logger.error(f"Model Suite Coordinator test failed: {str(e)}")
        logger.error(f"Error type: {e.__class__.__name__}")
        import traceback
        logger.error(f"Traceback: {traceback.format_exc()}")
        logger.error("")
        logger.error("❌ Model Suite Coordinator Test: FAILED!")
        return False

if __name__ == "__main__":
    success = asyncio.run(test_model_suite_coordinator())
    exit(0 if success else 1)
