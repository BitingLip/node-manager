"""
Inference Workers
AI model inference workers for various model types
"""

from .base_inference_worker import BaseInferenceWorker, InferenceRequest, InferenceResponse

# Import specific workers if available
try:
    from .llm_worker import LLMWorker
except ImportError:
    LLMWorker = None

try:
    from .sd_worker import StableDiffusionWorker
except ImportError:
    StableDiffusionWorker = None

try:
    from .tts_worker import TTSWorker
except ImportError:
    TTSWorker = None

try:
    from .worker_factory import WorkerFactory
except ImportError:
    WorkerFactory = None

__all__ = [
    "BaseInferenceWorker",
    "InferenceRequest", 
    "InferenceResponse",
    "LLMWorker",
    "StableDiffusionWorker",
    "TTSWorker",
    "WorkerFactory"
]
