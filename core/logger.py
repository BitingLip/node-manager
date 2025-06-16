#!/usr/bin/env python3
"""
Logger - Centralized logging utilities for Node Manager components
"""
import logging
import sys
from pathlib import Path
from typing import Optional


class ColoredFormatter(logging.Formatter):
    """Custom formatter with color codes for different log levels"""
    
    # ANSI color codes
    COLORS = {
        'DEBUG': '\033[36m',    # Cyan
        'INFO': '\033[36m',     # Cyan
        'WARNING': '\033[33m',  # Yellow
        'ERROR': '\033[31m',    # Red
        'CRITICAL': '\033[35m', # Magenta
        'RESET': '\033[0m'      # Reset
    }
    
    def __init__(self, fmt=None, datefmt=None, use_colors=True):
        super().__init__(fmt, datefmt)
        self.use_colors = use_colors
    
    def format(self, record):
        if self.use_colors and record.levelname in self.COLORS:
            record.levelname = f"{self.COLORS[record.levelname]}{record.levelname}{self.COLORS['RESET']}"
        return super().format(record)


class Logger:
    """Centralized logger for Node Manager components"""
    
    def __init__(self, component_name: str, device_id: int = 0, log_level: str = "INFO"):
        self.component_name = component_name
        self.device_id = device_id
        
        # Create logger
        self.logger = logging.getLogger(f"{component_name}_{device_id}")
        self.logger.setLevel(getattr(logging, log_level.upper(), logging.INFO))
        
        # Prevent duplicate handlers
        if not self.logger.handlers:
            self._setup_handlers()
    
    def _setup_handlers(self):
        """Setup console and file handlers"""
        # Console handler with colors
        console_handler = logging.StreamHandler(sys.stdout)
        console_formatter = ColoredFormatter(
            fmt='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
            datefmt='%Y-%m-%d %H:%M:%S'
        )
        console_handler.setFormatter(console_formatter)
        self.logger.addHandler(console_handler)
        
        # File handler (if logs directory exists)
        logs_dir = Path("logs")
        if logs_dir.exists() or self._create_logs_dir():
            file_handler = logging.FileHandler(logs_dir / f"{self.component_name.lower()}_{self.device_id}.log")
            file_formatter = logging.Formatter(
                fmt='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
                datefmt='%Y-%m-%d %H:%M:%S'
            )
            file_handler.setFormatter(file_formatter)
            self.logger.addHandler(file_handler)
    
    def _create_logs_dir(self) -> bool:
        """Create logs directory if it doesn't exist"""
        try:
            Path("logs").mkdir(exist_ok=True)
            return True
        except Exception:
            return False
    
    def debug(self, message: str):
        self.logger.debug(message)
    
    def info(self, message: str):
        self.logger.info(message)
    
    def warning(self, message: str):
        self.logger.warning(message)

    def error(self, message: str):
        self.logger.error(message)
    
    def critical(self, message: str):
        self.logger.critical(message)
    
    def log_action(self, action: str, details: Optional[str] = None, duration: Optional[float] = None):
        """Log an action with optional details and duration"""
        message = f"Action: {action}"
        if details:
            message += f" - {details}"
        if duration is not None:
            message += f" (took {duration:.2f}s)"
        self.info(message)
    
    def log_result(self, action: str, success: bool, details: Optional[str] = None):
        """Log the result of an action"""
        status = "SUCCESS" if success else "FAILED"
        message = f"{action}: {status}"
        if details:
            message += f" - {details}"
        
        if success:
            self.info(message)
        else:
            self.error(message)
    
    def log_memory_operation(self, operation: str, before_mb: int, after_mb: int):
        """Log memory-related operations"""
        change = after_mb - before_mb
        direction = "increased" if change > 0 else "decreased"
        self.info(f"Memory {operation}: {before_mb}MB -> {after_mb}MB ({direction} by {abs(change)}MB)")
    
    def log_hardware_metrics(self, metrics: dict):
        """Log hardware metrics"""
        self.debug(f"Hardware metrics: {metrics}")
    
    def log_task_start(self, task_id: str, task_type: str):
        """Log task start"""
        self.info(f"Task started: {task_id} (type: {task_type})")
    
    def log_task_complete(self, task_id: str, duration: float, success: bool):
        """Log task completion"""
        status = "completed" if success else "failed"
        self.info(f"Task {status}: {task_id} (duration: {duration:.2f}s)")
