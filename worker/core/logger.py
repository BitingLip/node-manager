#!/usr/bin/env python3
"""
Logger - Centralized logging utilities for worker processes
"""
import logging
import sys
import time
from pathlib import Path
from typing import Optional


class ColoredFormatter(logging.Formatter):
    """Custom formatter with color codes for different log levels"""
    
    # ANSI color codes
    COLORS = {
        'DEBUG': '\033[36m',      # Cyan
        'INFO': '\033[36m',       # Cyan  
        'WARNING': '\033[33m',    # Yellow
        'ERROR': '\033[31m',      # Red
        'CRITICAL': '\033[35m',   # Magenta
        'RESET': '\033[0m'        # Reset to default
    }
    
    def __init__(self, fmt=None, datefmt=None, use_colors=True):
        super().__init__(fmt, datefmt)
        self.use_colors = use_colors and hasattr(sys.stderr, 'isatty') and sys.stderr.isatty()
    
    def format(self, record):
        if self.use_colors:
            # Save original levelname
            original_levelname = record.levelname
            
            # Apply color to levelname
            color = self.COLORS.get(record.levelname, self.COLORS['RESET'])
            record.levelname = f"{color}{record.levelname}{self.COLORS['RESET']}"
            
            # Format the message
            formatted = super().format(record)
            
            # Restore original levelname
            record.levelname = original_levelname
            
            return formatted
        else:
            return super().format(record)


class Logger:
    """Centralized logger for worker components"""
    
    def __init__(self, worker_id: str, device_id: int, log_level: str = "INFO"):
        self.worker_id = worker_id
        self.device_id = device_id
        
        # Create logs directory
        log_dir = Path("logs")
        log_dir.mkdir(exist_ok=True)
        
        # Setup logger
        self.logger = logging.getLogger(f"worker_{device_id}")
        self.logger.setLevel(getattr(logging, log_level.upper()))
        
        # Remove existing handlers
        for handler in self.logger.handlers[:]:
            self.logger.removeHandler(handler)
          # Create formatters
        colored_formatter = ColoredFormatter(
            f'%(asctime)s - {worker_id} - %(levelname)s - %(message)s',
            use_colors=True
        )
        file_formatter = logging.Formatter(
            f'%(asctime)s - {worker_id} - %(levelname)s - %(message)s'
        )
        
        # Console handler with colors
        console_handler = logging.StreamHandler()
        console_handler.setLevel(logging.INFO)
        console_handler.setFormatter(colored_formatter)
        self.logger.addHandler(console_handler)
        
        # File handler without colors
        log_file = log_dir / f"{worker_id}.log"
        file_handler = logging.FileHandler(log_file)
        file_handler.setLevel(logging.DEBUG)
        file_handler.setFormatter(file_formatter)
        self.logger.addHandler(file_handler)
        
        self.logger.info(f"Logger initialized for {worker_id}")
    
    def debug(self, message: str):
        """Log debug message"""
        self.logger.debug(message)
    
    def info(self, message: str):
        """Log info message"""
        self.logger.info(message)
    
    def warning(self, message: str):
        """Log warning message"""
        self.logger.warning(message)
    def error(self, message: str):
        """Log error message"""
        self.logger.error(message)
    
    def critical(self, message: str):
        """Log critical message"""
        self.logger.critical(message)
    
    def log_action(self, action: str, details: Optional[str] = None, duration: Optional[float] = None):
        """Log an action with optional details and duration"""
        message = f"Action: {action}"
        if details:
            message += f" - {details}"
        if duration:
            message += f" (took {duration:.2f}s)"
        self.info(message)
    
    def log_result(self, action: str, success: bool, details: Optional[str] = None):
        """Log the result of an action"""
        status = "SUCCESS" if success else "FAILED"
        message = f"{status}: {action}"
        if details:
            message += f" - {details}"
        
        if success:
            self.info(message)
        else:
            self.error(message)
    def log_memory_operation(self, operation: str, before_mb: int, after_mb: int):
        """Log memory operations with before/after usage"""
        delta = after_mb - before_mb
        delta_str = f"+{delta}" if delta > 0 else str(delta)
        self.info(f"Memory {operation}: {before_mb}MB -> {after_mb}MB ({delta_str}MB)")
    def log_hardware_metrics(self, metrics: dict):
        """Log hardware metrics"""
        gpu_usage = metrics.get('gpu_usage', 0)
        gpu_vram = metrics.get('gpu_vram', 0)
        cpu_usage = metrics.get('cpu_usage', 0)
        cpu_ram = metrics.get('cpu_ram', 0)
        
        self.debug(f"Hardware: GPU {gpu_usage}% VRAM {gpu_vram}MB | CPU {cpu_usage}% RAM {cpu_ram}MB")
    def log_task_start(self, task_id: str, task_type: str):
        """Log the start of a task"""
        self.info(f"Task started: {task_id} ({task_type})")
    
    def log_task_complete(self, task_id: str, duration: float, success: bool):
        """Log task completion"""
        status = "COMPLETE" if success else "FAILED"
        self.info(f"{status} Task: {task_id} (took {duration:.2f}s)")
    
    def log_communication(self, direction: str, message_type: str, details: Optional[str] = None):
        """Log communication events"""
        arrow = "→" if direction == "send" else "←"
        message = f"Comm {arrow} {message_type}"
        if details:
            message += f": {details}"
        self.debug(message)
