#!/usr/bin/env python3
"""
Worker module for Node Manager
Contains the worker implementation that communicates with the new node manager
"""

# Import Worker class when this module is imported
try:
    from .worker import Worker
except ImportError:
    # Fallback for direct execution
    from worker import Worker

__all__ = ['Worker']