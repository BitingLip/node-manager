#!/usr/bin/env python3
"""
Enhanced Protocol Orchestrator
=============================

CRITICAL COMPONENT: Phase 1, Day 3-4 Python Implementation
Handles the new "message_type" protocol from C# Enhanced Request Transformer

This orchestrator bridges the gap between:
- C# Enhanced Services (using "message_type" protocol)
- Existing Python Workers (using "action" protocol)

Features:
- Protocol translation: message_type → action
- Enhanced request parsing and validation
- Smart worker routing based on request complexity
- Comprehensive error handling and logging
- Backward compatibility with legacy workers

Communication: JSON over stdin/stdout with C# PyTorchWorkerService
"""

import sys
import json
import logging
import traceback
import asyncio
from typing import Dict, Any, Optional, List, Union
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

# Configure logging for enhanced orchestrator
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - ENHANCED-ORCHESTRATOR - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stderr),
        logging.FileHandler('logs/enhanced-orchestrator.log')
    ]
)
logger = logging.getLogger(__name__)


@dataclass
class EnhancedRequest:
    """Parsed enhanced request from C# transformer."""
    message_type: str
    session_id: str
    worker_type: str = "simple"
    
    # Model configuration (flat fields from C# transformation)
    model_base: Optional[str] = None
    model_refiner: Optional[str] = None
    model_vae: Optional[str] = None
    model_tokenizer: Optional[str] = None
    model_text_encoder: Optional[str] = None
    model_text_encoder2: Optional[str] = None
    
    # Generation parameters
    prompt: Optional[str] = None
    negative_prompt: Optional[str] = None
    width: int = 1024
    height: int = 1024
    steps: int = 30
    guidance_scale: float = 7.5
    batch_size: int = 1
    seed: Optional[int] = None
    
    # Advanced features
    scheduler_type: Optional[str] = None
    scheduler_beta_start: Optional[float] = None
    scheduler_beta_end: Optional[float] = None
    scheduler_steps: Optional[int] = None
    
    # LoRA configuration
    lora_names: List[str] = field(default_factory=list)
    lora_paths: List[str] = field(default_factory=list)
    lora_weights: List[float] = field(default_factory=list)
    lora_adapter_names: List[str] = field(default_factory=list)
    
    # ControlNet configuration
    controlnet_types: List[str] = field(default_factory=list)
    controlnet_images: List[str] = field(default_factory=list)
    controlnet_weights: List[float] = field(default_factory=list)
    
    # Textual Inversions
    textual_inversion_tokens: List[str] = field(default_factory=list)
    textual_inversion_paths: List[str] = field(default_factory=list)
    
    # Image-to-image
    init_image: Optional[str] = None
    inpaint_mask: Optional[str] = None
    denoising_strength: Optional[float] = None
    
    # Performance settings
    device: str = "gpu_0"
    device_ids: List[str] = field(default_factory=list)
    dtype: str = "fp16"
    xformers: bool = False
    attention_slicing: bool = True
    cpu_offload: bool = False
    sequential_offload: bool = False
    batch_processing: bool = True
    
    # Postprocessing
    auto_contrast: bool = False
    upscaler_type: Optional[str] = None
    upscaler_scale: float = 1.0
    face_restoration: bool = False
    
    # Original data for debugging
    raw_data: Dict[str, Any] = field(default_factory=dict)
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'EnhancedRequest':
        """Create EnhancedRequest from dictionary data."""
        # Extract required fields
        message_type = data.get("message_type", "inference_request")
        session_id = data.get("session_id", "test-session")
        worker_type = data.get("worker_type", "enhanced")
        
        # Create instance with basic fields
        request = cls(
            message_type=message_type,
            session_id=session_id,
            worker_type=worker_type,
            raw_data=data
        )
        
        # Set generation parameters
        request.prompt = data.get("prompt", "")
        request.negative_prompt = data.get("negative_prompt", "")
        request.width = data.get("width", 1024)
        request.height = data.get("height", 1024)
        request.steps = data.get("steps", data.get("num_inference_steps", 20))
        request.guidance_scale = data.get("guidance_scale", 7.5)
        request.batch_size = data.get("batch_size", 1)
        request.seed = data.get("seed")
        
        # Set scheduler
        request.scheduler_type = data.get("scheduler", data.get("scheduler_type"))
        
        # Set model configuration
        model_config = data.get("model", {})
        if isinstance(model_config, dict):
            request.model_base = model_config.get("base")
            request.model_refiner = model_config.get("refiner")
            request.model_vae = model_config.get("vae")
        else:
            request.model_base = data.get("model_base", model_config)
        
        # Set LoRA configuration
        lora_config = data.get("lora", {})
        if isinstance(lora_config, dict):
            if lora_config.get("enabled", False):
                request.lora_names = [m.get("name", "") for m in lora_config.get("models", [])]
                request.lora_weights = [m.get("weight", 1.0) for m in lora_config.get("models", [])]
        
        # Set ControlNet configuration
        controlnet_config = data.get("controlnet", {})
        if isinstance(controlnet_config, dict) and controlnet_config.get("enabled", False):
            request.controlnet_types = [controlnet_config.get("model_type", "")]
        
        return request
    
    @property
    def num_inference_steps(self) -> int:
        """Compatibility property for num_inference_steps."""
        return self.steps
    
    @property
    def scheduler(self) -> Optional[str]:
        """Compatibility property for scheduler."""
        return self.scheduler_type
    
    @property
    def model(self) -> Dict[str, Optional[str]]:
        """Model configuration as dictionary."""
        return {
            "base": self.model_base,
            "refiner": self.model_refiner,
            "vae": self.model_vae
        }
    
    @property
    def lora(self) -> Dict[str, Any]:
        """LoRA configuration as dictionary."""
        return {
            "enabled": len(self.lora_names) > 0,
            "models": [
                {"name": name, "weight": weight}
                for name, weight in zip(self.lora_names, self.lora_weights)
            ]
        }
    
    @property
    def controlnet(self) -> Dict[str, Any]:
        """ControlNet configuration as dictionary."""
        return {
            "enabled": len(self.controlnet_types) > 0,
            "model_type": self.controlnet_types[0] if self.controlnet_types else None
        }


@dataclass 
class EnhancedResponse:
    """Enhanced response format for C# services."""
    success: bool
    message: str = ""
    error: Optional[str] = None
    
    # Generation results
    images: List[Dict[str, Any]] = field(default_factory=list)
    
    # Performance metrics
    generation_time_seconds: float = 0.0
    preprocessing_time_seconds: float = 0.0
    postprocessing_time_seconds: float = 0.0
    memory_used_mb: float = 0.0
    seed_used: Optional[int] = None
    
    # Features actually used
    features_used: Dict[str, bool] = field(default_factory=dict)
    
    # Metadata
    session_id: Optional[str] = None
    worker_id: Optional[str] = None
    timestamp: datetime = field(default_factory=datetime.utcnow)


class EnhancedProtocolOrchestrator:
    """
    Main orchestrator for enhanced protocol handling.
    
    CRITICAL FUNCTION: Translates C# enhanced protocol to Python worker protocol
    """
    
    def __init__(self):
        self.worker_instances = {}
        self.supported_workers = {
            "simple": "SDXLWorker",
            "advanced": "EnhancedSDXLWorker",
            "specialized": "SpecializedSDXLWorker"  # Future extension
        }
        
        # Track active sessions
        self.active_sessions = {}
        
        # Feature capabilities mapping
        self.worker_capabilities = {
            "simple": {
                "lora_support": False,
                "controlnet_support": False,
                "refiner_support": False,
                "upscaling": False,
                "max_resolution": 1024
            },
            "advanced": {
                "lora_support": True,
                "controlnet_support": True,
                "refiner_support": True,
                "upscaling": True,
                "max_resolution": 2048
            }
        }
        
        logger.info("Enhanced Protocol Orchestrator initialized")
    
    def parse_enhanced_request(self, raw_data: Dict[str, Any]) -> EnhancedRequest:
        """
        Parse enhanced request from C# transformer.
        
        CRITICAL: Handles the new "message_type" field instead of "action"
        """
        try:
            # Validate required fields
            if "message_type" not in raw_data:
                raise ValueError("Missing required field: message_type")
            
            if "session_id" not in raw_data:
                raise ValueError("Missing required field: session_id")
            
            # Extract core fields
            message_type = raw_data["message_type"]
            session_id = raw_data["session_id"]
            worker_type = raw_data.get("worker_type", "simple")
            
            # Create enhanced request object
            request = EnhancedRequest(
                message_type=message_type,
                session_id=session_id,
                worker_type=worker_type,
                raw_data=raw_data
            )
            
            # Parse model configuration
            request.model_base = raw_data.get("model_base")
            request.model_refiner = raw_data.get("model_refiner")
            request.model_vae = raw_data.get("model_vae")
            request.model_tokenizer = raw_data.get("model_tokenizer")
            request.model_text_encoder = raw_data.get("model_text_encoder")
            request.model_text_encoder2 = raw_data.get("model_text_encoder2")
            
            # Parse generation parameters
            request.prompt = raw_data.get("prompt")
            request.negative_prompt = raw_data.get("negative_prompt")
            request.width = raw_data.get("width", 1024)
            request.height = raw_data.get("height", 1024)
            request.steps = raw_data.get("steps", 30)
            request.guidance_scale = raw_data.get("guidance_scale", 7.5)
            request.batch_size = raw_data.get("batch_size", 1)
            request.seed = raw_data.get("seed")
            
            # Parse scheduler configuration
            request.scheduler_type = raw_data.get("scheduler_type")
            request.scheduler_beta_start = raw_data.get("scheduler_beta_start")
            request.scheduler_beta_end = raw_data.get("scheduler_beta_end")
            request.scheduler_steps = raw_data.get("scheduler_steps")
            
            # Parse LoRA configuration
            request.lora_names = raw_data.get("lora_names", [])
            request.lora_paths = raw_data.get("lora_paths", [])
            request.lora_weights = raw_data.get("lora_weights", [])
            request.lora_adapter_names = raw_data.get("lora_adapter_names", [])
            
            # Parse ControlNet configuration
            request.controlnet_types = raw_data.get("controlnet_types", [])
            request.controlnet_images = raw_data.get("controlnet_images", [])
            request.controlnet_weights = raw_data.get("controlnet_weights", [])
            
            # Parse Textual Inversions
            request.textual_inversion_tokens = raw_data.get("textual_inversion_tokens", [])
            request.textual_inversion_paths = raw_data.get("textual_inversion_paths", [])
            
            # Parse image-to-image
            request.init_image = raw_data.get("init_image")
            request.inpaint_mask = raw_data.get("inpaint_mask")
            request.denoising_strength = raw_data.get("denoising_strength")
            
            # Parse performance settings
            request.device = raw_data.get("device", "gpu_0")
            request.device_ids = raw_data.get("device_ids", [])
            request.dtype = raw_data.get("dtype", "fp16")
            request.xformers = raw_data.get("xformers", False)
            request.attention_slicing = raw_data.get("attention_slicing", True)
            request.cpu_offload = raw_data.get("cpu_offload", False)
            request.sequential_offload = raw_data.get("sequential_offload", False)
            request.batch_processing = raw_data.get("batch_processing", True)
            
            # Parse postprocessing
            request.auto_contrast = raw_data.get("auto_contrast", False)
            request.upscaler_type = raw_data.get("upscaler_type")
            request.upscaler_scale = raw_data.get("upscaler_scale", 1.0)
            request.face_restoration = raw_data.get("face_restoration", False)
            
            logger.info(f"Parsed enhanced request: {message_type} for session {session_id}")
            logger.debug(f"Worker type: {worker_type}, Features: LoRA={len(request.lora_names)}, ControlNet={len(request.controlnet_types)}")
            
            return request
            
        except Exception as e:
            logger.error(f"Failed to parse enhanced request: {e}")
            logger.debug(f"Raw data: {raw_data}")
            raise ValueError(f"Request parsing failed: {str(e)}")
    
    def transform_to_legacy_protocol(self, request: EnhancedRequest) -> Dict[str, Any]:
        """
        Transform enhanced request to legacy worker protocol.
        
        CRITICAL TRANSFORMATION: message_type → action
        """
        try:
            # Map message_type to action for legacy workers
            action_mapping = {
                "generate_sdxl_enhanced": "generate",
                "load_model": "load_model",
                "initialize": "initialize",
                "get_status": "get_status",
                "cleanup": "cleanup",
                "validate_request": "validate"
            }
            
            action = action_mapping.get(request.message_type, request.message_type)
            
            # Build legacy command structure
            legacy_command: Dict[str, Any] = {
                "action": action,  # CRITICAL: action instead of message_type
                "session_id": request.session_id
            }
            
            # Add specific parameters based on action
            if action == "generate":
                legacy_command["prompt_submission"] = {
                    "model": {
                        "base": request.model_base,
                        "refiner": request.model_refiner,
                        "vae": request.model_vae,
                        "tokenizer": request.model_tokenizer,
                        "text_encoder": request.model_text_encoder,
                        "text_encoder2": request.model_text_encoder2
                    },
                    "scheduler": {
                        "type": request.scheduler_type,
                        "steps": request.scheduler_steps or request.steps,
                        "beta_start": request.scheduler_beta_start,
                        "beta_end": request.scheduler_beta_end
                    },
                    "hyperparameters": {
                        "width": request.width,
                        "height": request.height,
                        "guidance_scale": request.guidance_scale,
                        "batch_size": request.batch_size,
                        "seed": request.seed,
                        "negative_prompt": request.negative_prompt
                    },
                    "conditioning": {
                        "prompt": request.prompt,
                        "negative_prompt": request.negative_prompt,
                        "loras": [
                            {
                                "name": name,
                                "path": path,
                                "weight": weight,
                                "adapter_name": adapter
                            }
                            for name, path, weight, adapter in zip(
                                request.lora_names,
                                request.lora_paths,
                                request.lora_weights,
                                request.lora_adapter_names
                            )
                        ] if request.lora_names else [],
                        "controlnets": [
                            {
                                "type": ctype,
                                "image": image,
                                "weight": weight
                            }
                            for ctype, image, weight in zip(
                                request.controlnet_types,
                                request.controlnet_images,
                                request.controlnet_weights
                            )
                        ] if request.controlnet_types else [],
                        "textual_inversions": [
                            {
                                "token": token,
                                "path": path
                            }
                            for token, path in zip(
                                request.textual_inversion_tokens,
                                request.textual_inversion_paths
                            )
                        ] if request.textual_inversion_tokens else [],
                        "init_image": request.init_image,
                        "inpaint_mask": request.inpaint_mask
                    },
                    "performance": {
                        "device": request.device,
                        "device_ids": request.device_ids,
                        "dtype": request.dtype,
                        "xformers": request.xformers,
                        "attention_slicing": request.attention_slicing,
                        "cpu_offload": request.cpu_offload,
                        "sequential_offload": request.sequential_offload,
                        "batch_processing": request.batch_processing
                    },
                    "postprocessing": {
                        "auto_contrast": request.auto_contrast,
                        "upscaler": {
                            "type": request.upscaler_type,
                            "scale": request.upscaler_scale
                        } if request.upscaler_type else None,
                        "face_restoration": request.face_restoration
                    }
                }
            
            elif action == "load_model":
                legacy_command["model_config"] = {
                    "base": request.model_base,
                    "refiner": request.model_refiner,
                    "vae": request.model_vae
                }
                legacy_command["performance_config"] = {
                    "dtype": request.dtype,
                    "xformers": request.xformers,
                    "attention_slicing": request.attention_slicing,
                    "cpu_offload": request.cpu_offload
                }
            
            elif action == "initialize":
                legacy_command["device_id"] = int(request.device.split("_")[-1]) if "_" in request.device else 0
                legacy_command["enable_multi_gpu"] = len(request.device_ids) > 1
            
            logger.info(f"Transformed enhanced protocol to legacy: {request.message_type} → {action}")
            return legacy_command
            
        except Exception as e:
            logger.error(f"Protocol transformation failed: {e}")
            raise ValueError(f"Protocol transformation failed: {str(e)}")
    
    def get_appropriate_worker(self, request: EnhancedRequest) -> str:
        """
        Determine appropriate worker based on request complexity.
        
        Matches the C# WorkerTypeResolver logic.
        """
        try:
            # Check for advanced features that require advanced worker
            needs_advanced = (
                len(request.lora_names) > 0 or
                len(request.controlnet_types) > 0 or
                request.model_refiner is not None or
                request.init_image is not None or
                request.inpaint_mask is not None or
                request.upscaler_type is not None or
                request.face_restoration or
                max(request.width, request.height) > 1024
            )
            
            if needs_advanced:
                worker_type = "advanced"
                logger.info("Routing to advanced worker - complex features detected")
            else:
                worker_type = "simple"
                logger.info("Routing to simple worker - basic generation")
            
            # Override with explicit worker type from request if provided
            if request.worker_type and request.worker_type in self.supported_workers:
                worker_type = request.worker_type
                logger.info(f"Using explicit worker type: {worker_type}")
            
            return worker_type
            
        except Exception as e:
            logger.warning(f"Worker selection failed, defaulting to simple: {e}")
            return "simple"
    
    async def process_enhanced_request(self, request: EnhancedRequest) -> EnhancedResponse:
        """
        Process enhanced request through appropriate worker.
        
        Main orchestration logic.
        """
        start_time = datetime.utcnow()
        session_id = request.session_id
        
        try:
            logger.info(f"Processing enhanced request {request.message_type} for session {session_id}")
            
            # Store session
            self.active_sessions[session_id] = {
                "request": request,
                "start_time": start_time,
                "worker_type": None
            }
            
            # Determine appropriate worker
            worker_type = self.get_appropriate_worker(request)
            self.active_sessions[session_id]["worker_type"] = worker_type
            
            # Transform to legacy protocol
            legacy_command = self.transform_to_legacy_protocol(request)
            
            # Route to appropriate worker implementation
            if worker_type == "advanced":
                # Use the existing enhanced SDXL worker
                from ..legacy.enhanced_sdxl_worker import EnhancedSDXLWorker, process_command_enhanced
                
                if worker_type not in self.worker_instances:
                    self.worker_instances[worker_type] = EnhancedSDXLWorker()
                
                worker = self.worker_instances[worker_type]
                result = process_command_enhanced(worker, legacy_command)
                
            else:
                # Use simple worker - for now, delegate to advanced worker
                # TODO: Implement proper simple worker routing
                from ..legacy.enhanced_sdxl_worker import EnhancedSDXLWorker, process_command_enhanced
                
                if "simple" not in self.worker_instances:
                    self.worker_instances["simple"] = EnhancedSDXLWorker()
                
                worker = self.worker_instances["simple"]
                result = process_command_enhanced(worker, legacy_command)
            
            # Transform result to enhanced response
            response = self.transform_to_enhanced_response(result, request, start_time)
            
            # Clean up session
            if session_id in self.active_sessions:
                del self.active_sessions[session_id]
            
            logger.info(f"Enhanced request {session_id} completed successfully")
            return response
            
        except Exception as e:
            logger.error(f"Enhanced request processing failed for {session_id}: {e}")
            logger.debug(traceback.format_exc())
            
            # Clean up session
            if session_id in self.active_sessions:
                del self.active_sessions[session_id]
            
            # Return error response
            execution_time = (datetime.utcnow() - start_time).total_seconds()
            return EnhancedResponse(
                success=False,
                error=str(e),
                message="Enhanced request processing failed",
                generation_time_seconds=execution_time,
                session_id=session_id
            )
    
    def transform_to_enhanced_response(self, worker_result: Dict[str, Any], 
                                     request: EnhancedRequest, start_time: datetime) -> EnhancedResponse:
        """Transform worker result to enhanced response format."""
        try:
            execution_time = (datetime.utcnow() - start_time).total_seconds()
            
            if not worker_result.get("success", False):
                return EnhancedResponse(
                    success=False,
                    error=worker_result.get("error", "Unknown error"),
                    message="Worker processing failed",
                    generation_time_seconds=execution_time,
                    session_id=request.session_id
                )
            
            # Extract images
            images = []
            worker_images = worker_result.get("images", [])
            if isinstance(worker_images, list):
                for img in worker_images:
                    if isinstance(img, dict):
                        images.append({
                            "path": img.get("path", ""),
                            "filename": img.get("filename", ""),
                            "width": img.get("width", request.width),
                            "height": img.get("height", request.height),
                            "seed": img.get("seed", request.seed)
                        })
                    elif isinstance(img, str):
                        # Simple path string
                        images.append({
                            "path": img,
                            "filename": Path(img).name,
                            "width": request.width,
                            "height": request.height,
                            "seed": request.seed
                        })
            
            # Extract features used
            features_used = worker_result.get("features_used", {})
            if not features_used:
                # Infer from request
                features_used = {
                    "lora_support": len(request.lora_names) > 0,
                    "controlnet_support": len(request.controlnet_types) > 0,
                    "refiner_support": request.model_refiner is not None,
                    "img2img": request.init_image is not None,
                    "inpainting": request.inpaint_mask is not None,
                    "postprocessing": request.auto_contrast or request.upscaler_type is not None,
                    "custom_scheduler": request.scheduler_type is not None
                }
            
            # Create enhanced response
            response = EnhancedResponse(
                success=True,
                message="Enhanced generation completed successfully",
                images=images,
                generation_time_seconds=worker_result.get("generation_time_seconds", execution_time),
                preprocessing_time_seconds=worker_result.get("preprocessing_time_seconds", 0.0),
                postprocessing_time_seconds=worker_result.get("postprocessing_time_seconds", 0.0),
                memory_used_mb=worker_result.get("memory_used_mb", 0.0),
                seed_used=worker_result.get("seed_used", request.seed),
                features_used=features_used,
                session_id=request.session_id,
                worker_id=f"{request.worker_type}_worker"
            )
            
            return response
            
        except Exception as e:
            logger.error(f"Response transformation failed: {e}")
            return EnhancedResponse(
                success=False,
                error=f"Response transformation failed: {str(e)}",
                message="Failed to transform worker response",
                session_id=request.session_id
            )
    
    def get_status(self) -> Dict[str, Any]:
        """Get orchestrator status."""
        return {
            "success": True,
            "orchestrator": "EnhancedProtocolOrchestrator",
            "active_sessions": len(self.active_sessions),
            "supported_workers": list(self.supported_workers.keys()),
            "worker_instances": list(self.worker_instances.keys()),
            "capabilities": self.worker_capabilities
        }


def main():
    """
    Main orchestrator loop.
    
    Handles communication with C# PyTorchWorkerService.
    """
    logger.info("Enhanced Protocol Orchestrator starting...")
    
    orchestrator = EnhancedProtocolOrchestrator()
    
    try:
        # Main communication loop
        for line in sys.stdin:
            line = line.strip()
            if not line:
                continue
            
            try:
                # Parse JSON command from C# service
                raw_data = json.loads(line)
                logger.debug(f"Received command: {raw_data.get('message_type', 'unknown')}")
                
                # Handle special commands
                if raw_data.get("message_type") == "get_status":
                    response = orchestrator.get_status()
                    print(json.dumps(response), flush=True)
                    continue
                
                elif raw_data.get("message_type") == "stop":
                    logger.info("Stop command received")
                    response = {"success": True, "message": "Orchestrator stopping"}
                    print(json.dumps(response), flush=True)
                    break
                
                # Parse enhanced request
                request = orchestrator.parse_enhanced_request(raw_data)
                
                # Process request (synchronous for now)
                loop = asyncio.new_event_loop()
                asyncio.set_event_loop(loop)
                response = loop.run_until_complete(
                    orchestrator.process_enhanced_request(request)
                )
                loop.close()
                
                # Convert response to dictionary and send back
                response_dict = {
                    "success": response.success,
                    "message": response.message,
                    "error": response.error,
                    "images": response.images,
                    "generation_time_seconds": response.generation_time_seconds,
                    "preprocessing_time_seconds": response.preprocessing_time_seconds,
                    "postprocessing_time_seconds": response.postprocessing_time_seconds,
                    "memory_used_mb": response.memory_used_mb,
                    "seed_used": response.seed_used,
                    "features_used": response.features_used,
                    "session_id": response.session_id,
                    "worker_id": response.worker_id,
                    "timestamp": response.timestamp.isoformat()
                }
                
                print(json.dumps(response_dict), flush=True)
                
            except json.JSONDecodeError as e:
                error_response = {
                    "success": False,
                    "error": f"Invalid JSON format: {str(e)}",
                    "message": "JSON parsing failed"
                }
                print(json.dumps(error_response), flush=True)
                logger.error(f"JSON decode error: {e}")
            
            except Exception as e:
                error_response = {
                    "success": False,
                    "error": str(e),
                    "message": "Request processing failed"
                }
                print(json.dumps(error_response), flush=True)
                logger.error(f"Request processing error: {e}")
                logger.debug(traceback.format_exc())
    
    except KeyboardInterrupt:
        logger.info("Enhanced Protocol Orchestrator interrupted by user")
    
    except Exception as e:
        logger.error(f"Enhanced Protocol Orchestrator fatal error: {e}")
        logger.debug(traceback.format_exc())
    
    finally:
        logger.info("Enhanced Protocol Orchestrator stopped")


if __name__ == "__main__":
    main()
