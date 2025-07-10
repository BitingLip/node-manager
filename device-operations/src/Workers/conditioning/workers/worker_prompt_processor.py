"""
Prompt Processor Worker for SDXL Workers System
==============================================

Migrated from conditioning/prompt_processor.py
Advanced text prompt processing and conditioning for improved generation quality.
"""

import logging
import re
import torch
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass

logger = logging.getLogger(__name__)


@dataclass
class PromptSegment:
    """Represents a segment of a prompt with weight and metadata."""
    text: str
    weight: float = 1.0
    start_pos: int = 0
    end_pos: int = 0
    attention_type: str = "normal"  # normal, emphasis, de-emphasis


@dataclass
class ProcessedPrompt:
    """Processed prompt with segments and metadata."""
    original: str
    segments: List[PromptSegment]
    weighted_text: str
    total_length: int
    max_weight: float
    min_weight: float


class PromptProcessorWorker:
    """
    Advanced text prompt processing and conditioning worker.
    
    This worker handles prompt weighting, attention manipulation, and multi-prompt composition.
    """
    
    def __init__(self, config: Dict[str, Any]):
        self.config = config
        self.logger = logging.getLogger(__name__)
        self.max_prompt_length = config.get("max_prompt_length", 77)  # Standard CLIP limit
        self.weight_regex = re.compile(r'\(([^)]+):([0-9.]+)\)|\[([^]]+):([0-9.]+)\]|\(([^)]+)\)|\[([^]]+)\]')
        self.initialized = False
        
    async def initialize(self) -> bool:
        """Initialize prompt processor worker."""
        try:
            self.logger.info("Initializing prompt processor worker...")
            self.initialized = True
            self.logger.info("Prompt processor worker initialized successfully")
            return True
        except Exception as e:
            self.logger.error("Prompt processor worker initialization failed: %s", e)
            return False
    
    async def process_prompt(self, prompt_data: Dict[str, Any]) -> Dict[str, Any]:
        """Process a text prompt with weighting and conditioning."""
        try:
            prompt_text = prompt_data.get("prompt", "")
            negative_prompt = prompt_data.get("negative_prompt", "")
            
            # Process positive prompt
            processed_prompt = self.parse_weighted_prompt(prompt_text)
            
            # Process negative prompt if provided
            processed_negative = None
            if negative_prompt:
                processed_negative = self.parse_weighted_prompt(negative_prompt)
            
            return {
                "processed_prompt": {
                    "original": processed_prompt.original,
                    "weighted_text": processed_prompt.weighted_text,
                    "segments": [
                        {
                            "text": seg.text,
                            "weight": seg.weight,
                            "attention_type": seg.attention_type
                        }
                        for seg in processed_prompt.segments
                    ],
                    "total_length": processed_prompt.total_length,
                    "max_weight": processed_prompt.max_weight,
                    "min_weight": processed_prompt.min_weight
                },
                "processed_negative": {
                    "original": processed_negative.original,
                    "weighted_text": processed_negative.weighted_text,
                    "segments": [
                        {
                            "text": seg.text,
                            "weight": seg.weight,
                            "attention_type": seg.attention_type
                        }
                        for seg in processed_negative.segments
                    ],
                    "total_length": processed_negative.total_length,
                    "max_weight": processed_negative.max_weight,
                    "min_weight": processed_negative.min_weight
                } if processed_negative else None
            }
        except Exception as e:
            self.logger.error("Failed to process prompt: %s", e)
            return {"error": str(e)}
        
    def parse_weighted_prompt(self, prompt: str) -> ProcessedPrompt:
        """Parse a prompt with attention weights like (word:1.2) or [word:0.8]."""
        segments = []
        last_end = 0
        total_weight = 0.0
        max_weight = 1.0
        min_weight = 1.0
        
        # Find all weight patterns
        for match in self.weight_regex.finditer(prompt):
            start, end = match.span()
            
            # Add text before this match as normal weight
            if start > last_end:
                text = prompt[last_end:start].strip()
                if text:
                    segments.append(PromptSegment(
                        text=text,
                        weight=1.0,
                        start_pos=last_end,
                        end_pos=start,
                        attention_type="normal"
                    ))
            
            # Parse the weighted segment
            if match.group(1) and match.group(2):  # (text:weight)
                text = match.group(1)
                weight = float(match.group(2))
                attention_type = "emphasis" if weight > 1.0 else "de-emphasis"
            elif match.group(3) and match.group(4):  # [text:weight]
                text = match.group(3)
                weight = float(match.group(4))
                attention_type = "de-emphasis" if weight < 1.0 else "emphasis"
            elif match.group(5):  # (text) - default emphasis
                text = match.group(5)
                weight = 1.1
                attention_type = "emphasis"
            elif match.group(6):  # [text] - default de-emphasis
                text = match.group(6)
                weight = 0.9
                attention_type = "de-emphasis"
            else:
                continue
            
            segments.append(PromptSegment(
                text=text,
                weight=weight,
                start_pos=start,
                end_pos=end,
                attention_type=attention_type
            ))
            
            total_weight += weight
            max_weight = max(max_weight, weight)
            min_weight = min(min_weight, weight)
            
            last_end = end
        
        # Add remaining text
        if last_end < len(prompt):
            text = prompt[last_end:].strip()
            if text:
                segments.append(PromptSegment(
                    text=text,
                    weight=1.0,
                    start_pos=last_end,
                    end_pos=len(prompt),
                    attention_type="normal"
                ))
        
        # If no segments found, treat entire prompt as normal
        if not segments:
            segments.append(PromptSegment(
                text=prompt,
                weight=1.0,
                start_pos=0,
                end_pos=len(prompt),
                attention_type="normal"
            ))
        
        # Create weighted text representation
        weighted_text = self._create_weighted_text(segments)
        
        return ProcessedPrompt(
            original=prompt,
            segments=segments,
            weighted_text=weighted_text,
            total_length=len(prompt),
            max_weight=max_weight,
            min_weight=min_weight
        )
    
    def _create_weighted_text(self, segments: List[PromptSegment]) -> str:
        """Create a text representation with weights applied."""
        weighted_parts = []
        
        for segment in segments:
            if segment.weight == 1.0:
                weighted_parts.append(segment.text)
            elif segment.weight > 1.0:
                # Repeat text for emphasis
                repeat_count = int(segment.weight)
                weighted_parts.append(" ".join([segment.text] * repeat_count))
            else:
                # Reduce emphasis by context
                weighted_parts.append(f"[{segment.text}]")
        
        return " ".join(weighted_parts)
    
    def apply_attention_weights(
        self,
        embeddings: torch.Tensor,
        segments: List[PromptSegment],
        token_positions: List[Tuple[int, int]]
    ) -> torch.Tensor:
        """Apply attention weights to text embeddings."""
        if len(segments) != len(token_positions):
            logger.warning("Mismatch between segments and token positions")
            return embeddings
        
        weighted_embeddings = embeddings.clone()
        
        for segment, (start_token, end_token) in zip(segments, token_positions):
            if segment.weight != 1.0:
                # Apply weight to the embedding region
                weight_factor = torch.tensor(segment.weight, dtype=embeddings.dtype, device=embeddings.device)
                weighted_embeddings[:, start_token:end_token, :] *= weight_factor
        
        return weighted_embeddings
    
    def process_multi_prompts(
        self,
        prompts: List[str],
        weights: Optional[List[float]] = None
    ) -> ProcessedPrompt:
        """Process multiple prompts with optional weights."""
        if weights is None:
            weights = [1.0] * len(prompts)
        
        if len(prompts) != len(weights):
            raise ValueError("Number of prompts must match number of weights")
        
        # Process each prompt individually
        processed_prompts = [self.parse_weighted_prompt(prompt) for prompt in prompts]
        
        # Combine segments with multi-prompt weights
        combined_segments = []
        combined_text_parts = []
        
        for processed_prompt, weight in zip(processed_prompts, weights):
            for segment in processed_prompt.segments:
                # Apply multi-prompt weight to segment weight
                combined_weight = segment.weight * weight
                combined_segments.append(PromptSegment(
                    text=segment.text,
                    weight=combined_weight,
                    start_pos=segment.start_pos,
                    end_pos=segment.end_pos,
                    attention_type=segment.attention_type
                ))
            
            combined_text_parts.append(processed_prompt.weighted_text)
        
        combined_text = " | ".join(combined_text_parts)
        
        return ProcessedPrompt(
            original=" | ".join(prompts),
            segments=combined_segments,
            weighted_text=combined_text,
            total_length=len(combined_text),
            max_weight=max(segment.weight for segment in combined_segments),
            min_weight=min(segment.weight for segment in combined_segments)
        )
    
    def truncate_prompt(self, processed_prompt: ProcessedPrompt, max_length: int) -> ProcessedPrompt:
        """Truncate prompt to maximum length while preserving important segments."""
        if processed_prompt.total_length <= max_length:
            return processed_prompt
        
        # Sort segments by weight (keep high-weight segments)
        sorted_segments = sorted(processed_prompt.segments, key=lambda s: s.weight, reverse=True)
        
        truncated_segments = []
        current_length = 0
        
        for segment in sorted_segments:
            segment_length = len(segment.text)
            if current_length + segment_length <= max_length:
                truncated_segments.append(segment)
                current_length += segment_length
            else:
                # Partially include segment if possible
                remaining_length = max_length - current_length
                if remaining_length > 10:  # Minimum meaningful length
                    truncated_text = segment.text[:remaining_length].rstrip()
                    truncated_segments.append(PromptSegment(
                        text=truncated_text,
                        weight=segment.weight,
                        start_pos=segment.start_pos,
                        end_pos=segment.start_pos + len(truncated_text),
                        attention_type=segment.attention_type
                    ))
                break
        
        # Recreate processed prompt with truncated segments
        truncated_text = " ".join(segment.text for segment in truncated_segments)
        
        return ProcessedPrompt(
            original=processed_prompt.original,
            segments=truncated_segments,
            weighted_text=truncated_text,
            total_length=len(truncated_text),
            max_weight=processed_prompt.max_weight,
            min_weight=processed_prompt.min_weight
        )
    
    def extract_keywords(self, processed_prompt: ProcessedPrompt, min_weight: float = 1.0) -> List[str]:
        """Extract important keywords based on weights."""
        keywords = []
        
        for segment in processed_prompt.segments:
            if segment.weight >= min_weight:
                # Split segment into words and add important ones
                words = segment.text.split()
                keywords.extend(words)
        
        return list(set(keywords))  # Remove duplicates
    
    def get_attention_mask(
        self,
        processed_prompt: ProcessedPrompt,
        token_length: int
    ) -> torch.Tensor:
        """Generate attention mask based on prompt weights."""
        mask = torch.ones(token_length)
        
        # This is a simplified implementation
        # In practice, you'd need to map segments to token positions
        for i, segment in enumerate(processed_prompt.segments):
            if i < token_length:
                mask[i] = segment.weight
        
        return mask
    
    def validate_prompt(self, prompt: str) -> Dict[str, Any]:
        """Validate prompt for common issues."""
        issues = []
        
        # Check length
        if len(prompt) > self.max_prompt_length * 10:  # Rough estimate
            issues.append("Prompt may be too long for effective processing")
        
        # Check for unmatched brackets
        open_brackets = prompt.count('(') + prompt.count('[')
        close_brackets = prompt.count(')') + prompt.count(']')
        if open_brackets != close_brackets:
            issues.append("Unmatched brackets detected")
        
        # Check for extreme weights
        processed = self.parse_weighted_prompt(prompt)
        if processed.max_weight > 2.0:
            issues.append(f"Very high weight detected: {processed.max_weight}")
        if processed.min_weight < 0.1:
            issues.append(f"Very low weight detected: {processed.min_weight}")
        
        return {
            "valid": len(issues) == 0,
            "issues": issues,
            "processed": processed
        }
    
    async def get_status(self) -> Dict[str, Any]:
        """Get prompt processor worker status."""
        return {
            "initialized": self.initialized,
            "max_prompt_length": self.max_prompt_length
        }
    
    async def cleanup(self) -> None:
        """Clean up prompt processor worker resources."""
        try:
            self.logger.info("Cleaning up prompt processor worker...")
            self.initialized = False
            self.logger.info("Prompt processor worker cleanup complete")
        except Exception as e:
            self.logger.error("Prompt processor worker cleanup error: %s", e)


# Global prompt processor instance
prompt_processor = PromptProcessorWorker({})
