#!/usr/bin/env python3
"""
OpenCLIP Processor - Extended CLIP support for longer prompts

This module provides OpenCLIP integration for processing prompts up to 248+ tokens,
significantly extending beyond the standard 77-token CLIP limitation.
"""
import logging
from typing import List, Dict, Any, Optional, Tuple
from pathlib import Path
import re

# Try to import torch with error handling for AMD SMI issues
try:
    import torch
    TORCH_AVAILABLE = True
except (ImportError, KeyError, OSError) as e:
    TORCH_AVAILABLE = False
    torch = None
    if "libamd_smi" in str(e):
        logging.warning(f"PyTorch import failed due to AMD SMI library issue: {e}")
    else:
        logging.warning(f"PyTorch import failed: {e}")

try:
    import open_clip
    OPENCLIP_AVAILABLE = True
except ImportError:
    OPENCLIP_AVAILABLE = False
    logging.warning("OpenCLIP not available - falling back to standard CLIP processing")


class OpenCLIPProcessor:
    """
    OpenCLIP processor for handling extended prompt lengths
    Supports up to 248+ tokens depending on model choice
    """
    
    def __init__(self, 
                 model_name: str = "ViT-L-14", 
                 pretrained: str = "laion2b_s32b_b82k",
                 device: str = "cuda",
                 logger: Optional[logging.Logger] = None):
        """
        Initialize OpenCLIP processor
        
        Args:
            model_name: OpenCLIP model architecture
            pretrained: Pretrained weights identifier
            device: Device to load model on
            logger: Logger instance
        """
        self.logger = logger or logging.getLogger(__name__)
        self.device = device
        self.model_name = model_name
        self.pretrained = pretrained
        
        if not OPENCLIP_AVAILABLE:
            raise ImportError("OpenCLIP not available. Install with: pip install open-clip-torch")
        
        # Model and tokenizer
        self.model = None
        self.tokenizer = None
        self.preprocess = None
        self.max_tokens = 77  # Default fallback
        
        # Load model
        self._load_model()
        
    def _load_model(self):
        """Load OpenCLIP model and tokenizer"""
        try:
            self.logger.info(f"Loading OpenCLIP model: {self.model_name} with {self.pretrained}")
            
            # Create model and transforms
            self.model, _, self.preprocess = open_clip.create_model_and_transforms(
                self.model_name, 
                pretrained=self.pretrained,
                device=self.device
            )
            
            # Get tokenizer
            self.tokenizer = open_clip.get_tokenizer(self.model_name)
            
            # Set maximum context length
            self.max_tokens = self._get_max_context_length(self.model_name)
            
            self.logger.info(f"OpenCLIP model loaded successfully. Max tokens: {self.max_tokens}")
            
        except Exception as e:
            self.logger.error(f"Failed to load OpenCLIP model: {e}")
            raise
    
    def _get_max_context_length(self, model_name: str) -> int:
        """
        Get maximum context length for the model
        
        Args:
            model_name: Name of the OpenCLIP model
            
        Returns:
            Maximum number of tokens supported
        """
        context_lengths = {
            "ViT-B-32": 77,
            "ViT-B-16": 77,
            "ViT-L-14": 248,
            "ViT-H-14": 248,
            "ViT-g-14": 248,
            "convnext_base": 248,
            "convnext_large": 248,
            "convnext_large_d": 248,
            "convnext_large_d_320": 248,
            "convnext_xxlarge": 248
        }
        
        # Try to get from model config if available
        try:
            if self.model is not None and hasattr(self.model, 'text') and hasattr(self.model.text, 'context_length'):
                return self.model.text.context_length
        except Exception:
            pass
        
        return context_lengths.get(model_name, 248)  # Default to 248 for extended models
    
    def encode_prompt(self, prompt: str, truncate: bool = False) -> torch.Tensor:
        """
        Encode a text prompt using OpenCLIP
        
        Args:
            prompt: Text prompt to encode
            truncate: Whether to truncate if prompt exceeds max tokens
            
        Returns:
            Encoded prompt tensor
        """
        if not prompt.strip():
            # Return zero embedding for empty prompts
            output_dim = getattr(self.model.text, 'output_dim', 512) if self.model and hasattr(self.model, 'text') else 512
            return torch.zeros((1, output_dim), device=self.device)
        
        try:
            # Ensure tokenizer is loaded
            if self.tokenizer is None:
                raise RuntimeError("OpenCLIP tokenizer is not loaded. Check model initialization.")
            # Tokenize with extended context
            tokens = self.tokenizer(prompt, context_length=self.max_tokens)
            
            # Check if prompt fits within token limit
            actual_tokens = len([t for t in tokens[0] if t != 0])  # Count non-padding tokens
            
            if actual_tokens <= self.max_tokens:
                # Direct encoding - prompt fits
                if self.model is None:
                    raise RuntimeError("OpenCLIP model is not loaded. Cannot encode text.")
                with torch.no_grad():
                    tokens = tokens.to(self.device)
                    text_features = self.model.encode_text(tokens)
                    return text_features
            else:
                if truncate:
                    # Truncate and encode
                    self.logger.warning(f"Prompt truncated from {actual_tokens} to {self.max_tokens} tokens")
                    truncated_tokens = tokens[:, :self.max_tokens]
                    if self.model is None:
                        raise RuntimeError("OpenCLIP model is not loaded. Cannot encode text.")
                    with torch.no_grad():
                        truncated_tokens = truncated_tokens.to(self.device)
                        text_features = self.model.encode_text(truncated_tokens)
                        return text_features
                else:
                    # Use chunking strategy
                    self.logger.info(f"Using chunking for {actual_tokens} token prompt")
                    return self._chunk_and_encode(prompt)
                    
        except Exception as e:
            self.logger.error(f"Error encoding prompt: {e}")
            raise
    
    def _chunk_and_encode(self, prompt: str) -> torch.Tensor:
        """
        Chunk long prompts and combine embeddings
        
        Args:
            prompt: Long text prompt to process
            
        Returns:
            Combined embedding tensor
        """
        chunks = self._smart_chunk(prompt, self.max_tokens - 2)  # Reserve for special tokens
        embeddings = []
        
        self.logger.debug(f"Processing {len(chunks)} chunks")
        
        for i, chunk in enumerate(chunks):
            try:
                if self.tokenizer is None:
                    raise RuntimeError("OpenCLIP tokenizer is not loaded. Check model initialization.")
                tokens = self.tokenizer(chunk, context_length=self.max_tokens)
                if self.model is None:
                    raise RuntimeError("OpenCLIP model is not loaded. Cannot encode text.")
                with torch.no_grad():
                    tokens = tokens.to(self.device)
                    embedding = self.model.encode_text(tokens)
                    embeddings.append(embedding)
                    
            except Exception as e:
                self.logger.warning(f"Failed to encode chunk {i}: {e}")
                continue
        
        if not embeddings:
            raise ValueError("No chunks could be encoded successfully")
        
        # Combine embeddings using weighted average
        if len(embeddings) > 1:
            # Weight first chunks more heavily
            weights = torch.softmax(
                torch.tensor([1.0, 0.8, 0.6, 0.4][:len(embeddings)], device=self.device), 
                dim=0
            )
            
            # Weighted combination
            combined = torch.zeros_like(embeddings[0])
            for emb, weight in zip(embeddings, weights):
                combined += emb * weight
                
            return combined / weights.sum()
        else:
            return embeddings[0]
    
    def _smart_chunk(self, prompt: str, max_chunk_tokens: int) -> List[str]:
        """
        Intelligently chunk prompts at natural boundaries
        
        Args:
            prompt: Text to chunk
            max_chunk_tokens: Maximum tokens per chunk
            
        Returns:
            List of prompt chunks
        """
        # Split by major delimiters while preserving weights/parentheses
        parts = re.split(r'[,;](?![^()]*\))', prompt)
        chunks = []
        current_chunk = ""
        
        for part in parts:
            part = part.strip()
            if not part:
                continue
                
            test_chunk = f"{current_chunk}, {part}" if current_chunk else part
            
            # Estimate tokens (conservative estimate: 1.5 tokens per word)
            estimated_tokens = len(test_chunk.split()) * 1.5
            
            if estimated_tokens <= max_chunk_tokens:
                current_chunk = test_chunk
            else:
                if current_chunk:
                    chunks.append(current_chunk)
                    current_chunk = part
                else:
                    # Single part too long, add anyway and let tokenizer handle it
                    chunks.append(part)
        
        if current_chunk:
            chunks.append(current_chunk)
        
        return chunks[:4]  # Limit to 4 chunks for memory efficiency
    
    def get_model_info(self) -> Dict[str, Any]:
        """
        Get information about the loaded model
        
        Returns:
            Dictionary with model information
        """
        return {
            "model_name": self.model_name,
            "pretrained": self.pretrained,
            "max_tokens": self.max_tokens,
            "device": self.device,
            "available": OPENCLIP_AVAILABLE,
            "text_dim": getattr(self.model.text, 'output_dim', None) if self.model else None
        }
    
    def cleanup(self):
        """Clean up model resources"""
        if self.model is not None:
            del self.model
            self.model = None
        if torch.cuda.is_available():
            torch.cuda.empty_cache()
        self.logger.info("OpenCLIP processor cleaned up")


# Factory function for creating processor instances
def create_openclip_processor(config: Dict[str, Any], device: str, logger: Optional[logging.Logger] = None) -> OpenCLIPProcessor:
    """
    Factory function to create OpenCLIP processor from configuration
    
    Args:
        config: Configuration dictionary
        device: Device to load model on
        logger: Logger instance
        
    Returns:
        Configured OpenCLIPProcessor instance
    """
    clip_config = config.get('clip_processor', {})
    
    model_name = clip_config.get('model', 'ViT-L-14')
    pretrained = clip_config.get('pretrained', 'laion2b_s32b_b82k')
    
    return OpenCLIPProcessor(
        model_name=model_name,
        pretrained=pretrained,
        device=device,
        logger=logger
    )
