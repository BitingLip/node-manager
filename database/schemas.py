"""
Database Schemas
Data models and schemas for PostgreSQL database
"""

from dataclasses import dataclass
from datetime import datetime
from typing import Dict, List, Optional, Any
from enum import Enum


class NodeStatus(Enum):
    """Node status enumeration"""
    INITIALIZING = "initializing"
    READY = "ready"
    BUSY = "busy"
    ERROR = "error"
    OFFLINE = "offline"


@dataclass
class NodeRecord:
    """Node database record"""
    node_id: str
    hostname: str
    ip_address: str
    port: int
    status: NodeStatus
    capabilities: Dict[str, Any]
    resources: Dict[str, Any]
    last_heartbeat: datetime
    created_at: datetime
    updated_at: datetime


@dataclass 
class WorkerRecord:
    """Worker database record"""
    worker_id: str
    node_id: str
    worker_type: str
    status: str
    capabilities: Dict[str, Any]
    resource_allocation: Dict[str, Any]
    current_task_id: Optional[str]
    error_count: int
    last_heartbeat: datetime
    created_at: datetime
    updated_at: datetime


@dataclass
class TaskRecord:
    """Task database record"""
    task_id: str
    node_id: str
    worker_id: Optional[str]
    task_type: str
    task_data: Dict[str, Any]
    status: str
    priority: int
    result: Optional[Any]
    error: Optional[str]
    retry_count: int
    created_at: datetime
    started_at: Optional[datetime]
    completed_at: Optional[datetime]


@dataclass
class ResourceMetric:
    """Resource usage metric record"""
    node_id: str
    timestamp: datetime
    cpu_usage: float
    memory_usage: int
    memory_total: int
    gpu_memory_usage: Dict[str, int]
    gpu_memory_total: Dict[str, int]
    disk_usage: int
    disk_total: int
    network_rx: int
    network_tx: int
