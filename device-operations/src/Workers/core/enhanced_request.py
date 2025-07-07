"""
Enhanced Request Models - Phase 2 Implementation
Data models for enhanced SDXL requests with advanced features support.
"""

from dataclasses import dataclass, field
from typing import Dict, List, Optional, Any, Union
from enum import Enum

class SchedulerType(Enum):
    """Supported scheduler types."""
    DPM_SOLVER_MULTISTEP = "DPMSolverMultistepScheduler"
    DDIM = "DDIMScheduler"
    EULER_DISCRETE = "EulerDiscreteScheduler"
    EULER_ANCESTRAL_DISCRETE = "EulerAncestralDiscreteScheduler"
    DPM_SOLVER_SINGLESTEP = "DPMSolverSinglestepScheduler"
    KDPM2_DISCRETE = "KDPM2DiscreteScheduler"
    KDPM2_ANCESTRAL_DISCRETE = "KDPM2AncestralDiscreteScheduler"
    HEUN_DISCRETE = "HeunDiscreteScheduler"
    LMS_DISCRETE = "LMSDiscreteScheduler"
    UNIPC_MULTISTEP = "UniPCMultistepScheduler"

@dataclass
class LoRAConfiguration:
    """Configuration for a single LoRA adapter."""
    name: str
    weight: float = 1.0
    path: Optional[str] = None
    
    def __post_init__(self):
        # Clamp weight to valid range
        self.weight = max(-2.0, min(2.0, self.weight))

@dataclass
class LoRASettings:
    """LoRA configuration settings."""
    enabled: bool = False
    models: List[LoRAConfiguration] = field(default_factory=list)
    
    @classmethod
    def from_dict(cls, data: Dict) -> 'LoRASettings':
        """Create LoRASettings from dictionary."""
        if not data:
            return cls()
        
        models = []
        if 'models' in data:
            for model_data in data['models']:
                if isinstance(model_data, dict):
                    models.append(LoRAConfiguration(**model_data))
                elif isinstance(model_data, str):
                    models.append(LoRAConfiguration(name=model_data))
        
        return cls(
            enabled=data.get('enabled', False),
            models=models
        )

@dataclass
class ControlNetConfiguration:
    """Configuration for a single ControlNet."""
    model_type: str  # canny, depth, pose, etc.
    conditioning_image: str  # base64 or file path
    weight: float = 1.0
    guidance_start: float = 0.0
    guidance_end: float = 1.0
    model_path: Optional[str] = None
    
    def __post_init__(self):
        # Clamp values to valid ranges
        self.weight = max(0.0, min(2.0, self.weight))
        self.guidance_start = max(0.0, min(1.0, self.guidance_start))
        self.guidance_end = max(0.0, min(1.0, self.guidance_end))

@dataclass
class ControlNetSettings:
    """ControlNet configuration settings."""
    enabled: bool = False
    models: List[ControlNetConfiguration] = field(default_factory=list)
    
    @classmethod
    def from_dict(cls, data: Dict) -> 'ControlNetSettings':
        """Create ControlNetSettings from dictionary."""
        if not data:
            return cls()
        
        models = []
        if 'models' in data:
            for model_data in data['models']:
                if isinstance(model_data, dict):
                    models.append(ControlNetConfiguration(**model_data))
        
        return cls(
            enabled=data.get('enabled', False),
            models=models
        )

@dataclass
class ModelConfiguration:
    """Model configuration for SDXL generation."""
    base: str
    refiner: Optional[str] = None
    vae: Optional[str] = None
    
    @classmethod
    def from_dict(cls, data: Dict) -> 'ModelConfiguration':
        """Create ModelConfiguration from dictionary."""
        if isinstance(data, str):
            return cls(base=data)
        
        return cls(
            base=data.get('base', ''),
            refiner=data.get('refiner'),
            vae=data.get('vae')
        )

@dataclass
class PostProcessingStep:
    """Configuration for a post-processing step."""
    step_type: str  # upscale, enhance, etc.
    parameters: Dict[str, Any] = field(default_factory=dict)
    
    @classmethod
    def from_dict(cls, data: Dict) -> 'PostProcessingStep':
        """Create PostProcessingStep from dictionary."""
        return cls(
            step_type=data.get('type', data.get('step_type', '')),
            parameters=data.get('parameters', {})
        )

@dataclass
class EnhancedRequest:
    """Enhanced SDXL request with advanced features."""
    
    # Basic generation parameters
    prompt: str
    negative_prompt: str = ""
    width: int = 1024
    height: int = 1024
    num_inference_steps: int = 30
    guidance_scale: float = 7.5
    batch_size: int = 1
    seed: Optional[int] = None
    
    # Model configuration
    model: Optional[ModelConfiguration] = None
    
    # Scheduler configuration
    scheduler: str = "DPMSolverMultistepScheduler"
    scheduler_config: Dict[str, Any] = field(default_factory=dict)
    
    # Advanced features
    lora: Optional[LoRASettings] = None
    controlnet: Optional[ControlNetSettings] = None
    
    # Refiner settings
    refiner_strength: float = 0.3
    refiner_steps: int = 10
    
    # Post-processing
    post_processing: List[PostProcessingStep] = field(default_factory=list)
    
    # Output settings
    return_base64: bool = False
    save_images: bool = True
    
    # Session information
    session_id: str = ""
    message_type: str = "generate_sdxl_enhanced"
    
    def __post_init__(self):
        """Validate and normalize parameters after initialization."""
        # Ensure dimensions are multiples of 8 (SDXL requirement)
        self.width = (self.width // 8) * 8
        self.height = (self.height // 8) * 8
        
        # Clamp values to reasonable ranges
        self.width = max(512, min(2048, self.width))
        self.height = max(512, min(2048, self.height))
        self.num_inference_steps = max(1, min(150, self.num_inference_steps))
        self.guidance_scale = max(1.0, min(30.0, self.guidance_scale))
        self.batch_size = max(1, min(8, self.batch_size))
        self.refiner_strength = max(0.0, min(1.0, self.refiner_strength))
        self.refiner_steps = max(1, min(50, self.refiner_steps))
    
    @classmethod
    def from_dict(cls, data: Dict) -> 'EnhancedRequest':
        """
        Create EnhancedRequest from dictionary data.
        
        Handles both C# enhanced format and legacy format.
        """
        # Handle model configuration
        model_data = data.get('model', data.get('models'))
        model = None
        if model_data:
            model = ModelConfiguration.from_dict(model_data)
        elif 'base_model' in data:
            # Legacy format support
            model = ModelConfiguration(
                base=data['base_model'],
                refiner=data.get('refiner_model'),
                vae=data.get('vae_model')
            )
        
        # Handle LoRA configuration
        lora_data = data.get('lora', data.get('loras'))
        lora = None
        if lora_data:
            lora = LoRASettings.from_dict(lora_data)
        
        # Handle ControlNet configuration
        controlnet_data = data.get('controlnet', data.get('controlnets'))
        controlnet = None
        if controlnet_data:
            controlnet = ControlNetSettings.from_dict(controlnet_data)
        
        # Handle post-processing
        post_processing = []
        if 'post_processing' in data:
            for step_data in data['post_processing']:
                if isinstance(step_data, dict):
                    post_processing.append(PostProcessingStep.from_dict(step_data))
        
        # Extract scheduler configuration
        scheduler = data.get('scheduler', 'DPMSolverMultistepScheduler')
        scheduler_config = data.get('scheduler_config', {})
        
        # Handle legacy scheduler mapping
        legacy_scheduler_map = {
            'DPMSolverMultistep': 'DPMSolverMultistepScheduler',
            'DDIM': 'DDIMScheduler',
            'Euler': 'EulerDiscreteScheduler',
            'EulerA': 'EulerAncestralDiscreteScheduler'
        }
        if scheduler in legacy_scheduler_map:
            scheduler = legacy_scheduler_map[scheduler]
        
        return cls(
            # Basic parameters
            prompt=data.get('prompt', ''),
            negative_prompt=data.get('negative_prompt', ''),
            width=data.get('width', 1024),
            height=data.get('height', 1024),
            num_inference_steps=data.get('num_inference_steps', data.get('steps', 30)),
            guidance_scale=data.get('guidance_scale', data.get('cfg_scale', 7.5)),
            batch_size=data.get('batch_size', data.get('num_images', 1)),
            seed=data.get('seed'),
            
            # Model configuration
            model=model,
            
            # Scheduler
            scheduler=scheduler,
            scheduler_config=scheduler_config,
            
            # Advanced features
            lora=lora,
            controlnet=controlnet,
            
            # Refiner settings
            refiner_strength=data.get('refiner_strength', 0.3),
            refiner_steps=data.get('refiner_steps', 10),
            
            # Post-processing
            post_processing=post_processing,
            
            # Output settings
            return_base64=data.get('return_base64', False),
            save_images=data.get('save_images', True),
            
            # Session info
            session_id=data.get('session_id', ''),
            message_type=data.get('message_type', 'generate_sdxl_enhanced')
        )
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert EnhancedRequest to dictionary."""
        result = {
            'message_type': self.message_type,
            'session_id': self.session_id,
            'prompt': self.prompt,
            'negative_prompt': self.negative_prompt,
            'width': self.width,
            'height': self.height,
            'num_inference_steps': self.num_inference_steps,
            'guidance_scale': self.guidance_scale,
            'batch_size': self.batch_size,
            'seed': self.seed,
            'scheduler': self.scheduler,
            'scheduler_config': self.scheduler_config,
            'refiner_strength': self.refiner_strength,
            'refiner_steps': self.refiner_steps,
            'return_base64': self.return_base64,
            'save_images': self.save_images
        }
        
        # Add model configuration
        if self.model:
            result['model'] = {
                'base': self.model.base,
                'refiner': self.model.refiner,
                'vae': self.model.vae
            }
        
        # Add LoRA configuration
        if self.lora and self.lora.enabled:
            result['lora'] = {
                'enabled': self.lora.enabled,
                'models': [
                    {
                        'name': lora.name,
                        'weight': lora.weight,
                        'path': lora.path
                    }
                    for lora in self.lora.models
                ]
            }
        
        # Add ControlNet configuration
        if self.controlnet and self.controlnet.enabled:
            result['controlnet'] = {
                'enabled': self.controlnet.enabled,
                'models': [
                    {
                        'model_type': cn.model_type,
                        'conditioning_image': cn.conditioning_image,
                        'weight': cn.weight,
                        'guidance_start': cn.guidance_start,
                        'guidance_end': cn.guidance_end,
                        'model_path': cn.model_path
                    }
                    for cn in self.controlnet.models
                ]
            }
        
        # Add post-processing configuration
        if self.post_processing:
            result['post_processing'] = [
                {
                    'type': step.step_type,
                    'parameters': step.parameters
                }
                for step in self.post_processing
            ]
        
        return result
    
    def validate(self) -> List[str]:
        """
        Validate the request and return list of validation errors.
        
        Returns:
            List of validation error messages (empty if valid)
        """
        errors = []
        
        # Basic validation
        if not self.prompt.strip():
            errors.append("Prompt cannot be empty")
        
        if not self.model or not self.model.base:
            errors.append("Base model is required")
        
        # Dimension validation
        if self.width % 8 != 0 or self.height % 8 != 0:
            errors.append("Width and height must be multiples of 8")
        
        if self.width < 512 or self.height < 512:
            errors.append("Minimum resolution is 512x512")
        
        if self.width > 2048 or self.height > 2048:
            errors.append("Maximum resolution is 2048x2048")
        
        # Parameter range validation
        if self.num_inference_steps < 1 or self.num_inference_steps > 150:
            errors.append("Number of inference steps must be between 1 and 150")
        
        if self.guidance_scale < 1.0 or self.guidance_scale > 30.0:
            errors.append("Guidance scale must be between 1.0 and 30.0")
        
        if self.batch_size < 1 or self.batch_size > 8:
            errors.append("Batch size must be between 1 and 8")
        
        # LoRA validation
        if self.lora and self.lora.enabled:
            if not self.lora.models:
                errors.append("LoRA enabled but no models specified")
            
            for i, lora in enumerate(self.lora.models):
                if not lora.name:
                    errors.append(f"LoRA model {i+1} name is required")
                if lora.weight < -2.0 or lora.weight > 2.0:
                    errors.append(f"LoRA model {i+1} weight must be between -2.0 and 2.0")
        
        # ControlNet validation
        if self.controlnet and self.controlnet.enabled:
            if not self.controlnet.models:
                errors.append("ControlNet enabled but no models specified")
            
            for i, cn in enumerate(self.controlnet.models):
                if not cn.model_type:
                    errors.append(f"ControlNet model {i+1} type is required")
                if not cn.conditioning_image:
                    errors.append(f"ControlNet model {i+1} conditioning image is required")
        
        return errors
    
    def get_total_estimated_time(self) -> float:
        """
        Estimate total generation time in seconds.
        This is a rough estimate based on typical performance.
        """
        base_time = self.num_inference_steps * 0.1  # ~0.1s per step
        base_time *= self.batch_size  # Scale by batch size
        
        # Add time for features
        if self.lora and self.lora.enabled:
            base_time *= 1.1  # 10% overhead for LoRA
        
        if self.controlnet and self.controlnet.enabled:
            base_time *= 1.2  # 20% overhead for ControlNet
        
        if self.model and self.model.refiner:
            base_time += self.refiner_steps * 0.08  # Refiner time
        
        # Add post-processing time
        for step in self.post_processing:
            if step.step_type.lower() == 'upscale':
                base_time += 5.0  # ~5s for upscaling
        
        return base_time
