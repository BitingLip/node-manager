"""
Inference Workers
AI model inference workers for various model types
"""

# Import specific workers if available
try:
    from .llm_worker import LLMWorker
    from .sd_worker import StableDiffusionWorker
    from .tts_worker import TTSWorker
except ImportError as e:
    # Handle missing dependencies gracefully
    LLMWorker = None
    StableDiffusionWorker = None
    TTSWorker = None

__all__ = ['LLMWorker', 'StableDiffusionWorker', 'TTSWorker']
