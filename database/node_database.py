"""
Node Database
PostgreSQL database integration for node-level data persistence
Handles local node state, worker tracking, and task history
"""

import psycopg2
import psycopg2.pool
from psycopg2.extras import RealDictCursor
import logging
from typing import Optional, List, Dict, Any
from datetime import datetime
import json
import structlog

logger = structlog.get_logger(__name__)


class NodeDatabase:
    """
    PostgreSQL database interface for node-level operations
    Manages local node state, worker registry, and task tracking
    """
    
    def __init__(self, connection_config: Dict[str, Any]):
        """Initialize database connection"""
        self.connection_config = connection_config
        self.connection_pool: Optional[psycopg2.pool.ThreadedConnectionPool] = None
        
        # Initialize connection pool
        self._create_connection_pool()
        
        # Initialize database schema
        self._initialize_schema()
        
        logger.info("NodeDatabase initialized")
    
    def _create_connection_pool(self):
        """Create PostgreSQL connection pool"""
        try:
            # Create connection pool with parameters from config
            self.connection_pool = psycopg2.pool.ThreadedConnectionPool(
                minconn=self.connection_config.get('min_connections', 1),
                maxconn=self.connection_config.get('max_connections', 10),
                host=self.connection_config['host'],
                port=self.connection_config['port'],
                database=self.connection_config['database'],
                user=self.connection_config['user'],
                password=self.connection_config['password'],
                cursor_factory=RealDictCursor
            )
            logger.info(f"Node database connection pool created: {self.connection_config.get('min_connections', 1)}-{self.connection_config.get('max_connections', 10)} connections")
        except Exception as e:
            logger.error(f"Failed to create node database connection pool: {e}")
            raise
    
    def _initialize_schema(self):
        """Initialize database schema if needed"""
        schema_sql = """
        -- Node status table
        CREATE TABLE IF NOT EXISTS node_status (
            node_id VARCHAR(255) PRIMARY KEY,
            hostname VARCHAR(255) NOT NULL,
            ip_address INET,
            port INTEGER,
            status VARCHAR(50) NOT NULL DEFAULT 'initializing',
            capabilities JSONB DEFAULT '{}',
            resources JSONB DEFAULT '{}',
            last_heartbeat TIMESTAMP DEFAULT NOW(),
            created_at TIMESTAMP DEFAULT NOW(),
            updated_at TIMESTAMP DEFAULT NOW()
        );

        -- Workers table  
        CREATE TABLE IF NOT EXISTS node_workers (
            worker_id VARCHAR(255) PRIMARY KEY,
            node_id VARCHAR(255) NOT NULL REFERENCES node_status(node_id),
            worker_type VARCHAR(100) NOT NULL,
            status VARCHAR(50) NOT NULL DEFAULT 'initializing',
            capabilities JSONB DEFAULT '{}',
            resource_allocation JSONB DEFAULT '{}',
            current_task_id VARCHAR(255),
            error_count INTEGER DEFAULT 0,
            last_heartbeat TIMESTAMP DEFAULT NOW(),
            created_at TIMESTAMP DEFAULT NOW(),
            updated_at TIMESTAMP DEFAULT NOW()
        );

        -- Task records table
        CREATE TABLE IF NOT EXISTS node_tasks (
            task_id VARCHAR(255) PRIMARY KEY,
            node_id VARCHAR(255) NOT NULL REFERENCES node_status(node_id),
            worker_id VARCHAR(255) REFERENCES node_workers(worker_id),
            task_type VARCHAR(100) NOT NULL,
            task_data JSONB NOT NULL,
            status VARCHAR(50) NOT NULL DEFAULT 'pending',
            priority INTEGER DEFAULT 0,
            result JSONB,
            error TEXT,
            retry_count INTEGER DEFAULT 0,
            created_at TIMESTAMP DEFAULT NOW(),
            started_at TIMESTAMP,
            completed_at TIMESTAMP
        );

        -- Resource metrics table
        CREATE TABLE IF NOT EXISTS node_resource_metrics (
            id SERIAL PRIMARY KEY,
            node_id VARCHAR(255) NOT NULL REFERENCES node_status(node_id),
            timestamp TIMESTAMP DEFAULT NOW(),
            cpu_usage FLOAT,
            memory_usage BIGINT,
            memory_total BIGINT,
            gpu_memory_usage JSONB DEFAULT '{}',
            gpu_memory_total JSONB DEFAULT '{}',
            disk_usage BIGINT,
            disk_total BIGINT,
            network_rx BIGINT DEFAULT 0,
            network_tx BIGINT DEFAULT 0
        );

        -- Create indices for performance
        CREATE INDEX IF NOT EXISTS idx_node_workers_node_id ON node_workers(node_id);
        CREATE INDEX IF NOT EXISTS idx_node_tasks_node_id ON node_tasks(node_id);
        CREATE INDEX IF NOT EXISTS idx_node_tasks_worker_id ON node_tasks(worker_id);
        CREATE INDEX IF NOT EXISTS idx_node_tasks_status ON node_tasks(status);
        CREATE INDEX IF NOT EXISTS idx_resource_metrics_node_id ON node_resource_metrics(node_id);
        CREATE INDEX IF NOT EXISTS idx_resource_metrics_timestamp ON node_resource_metrics(timestamp);
        """
        
        try:
            conn = self.get_connection()
            try:
                with conn.cursor() as cursor:
                    cursor.execute(schema_sql)
                    conn.commit()
                logger.info("Node database schema initialized successfully")
            finally:
                self.return_connection(conn)
        except Exception as e:
            logger.error(f"Failed to initialize node database schema: {e}")
            raise
      def get_connection(self):
        """Get database connection from pool"""
        try:
            return self.connection_pool.getconn()  # type: ignore
        except Exception as e:
            logger.error(f"Failed to get database connection: {e}")
            raise
    
    def return_connection(self, conn):
        """Return connection to pool"""
        try:
            self.connection_pool.putconn(conn)  # type: ignore
        except Exception as e:
            logger.error(f"Failed to return database connection: {e}")
    
    def execute_query(self, query: str, params: Optional[tuple] = None) -> List[Dict[str, Any]]:
        """Execute query and return results"""
        conn = None
        try:
            conn = self.get_connection()
            with conn.cursor() as cursor:
                cursor.execute(query, params)
                
                # Handle different query types
                if cursor.description:
                    results = cursor.fetchall()
                    return [dict(row) for row in results]
                else:
                    conn.commit()
                    return []
                    
        except Exception as e:
            if conn:
                conn.rollback()
            logger.error(f"Database query failed: {e}")
            logger.error(f"Query: {query}")
            logger.error(f"Params: {params}")
            raise
        finally:
            if conn:
                self.return_connection(conn)
    
    # Node management methods
    def update_node_status(self, node_id: str, status: str, details: Optional[Dict[str, Any]] = None):
        """Update node status in database"""
        query = """
        INSERT INTO node_status (node_id, hostname, status, capabilities, resources, updated_at)
        VALUES (%s, %s, %s, %s, %s, NOW())
        ON CONFLICT (node_id) 
        DO UPDATE SET 
            status = EXCLUDED.status,
            capabilities = EXCLUDED.capabilities,
            resources = EXCLUDED.resources,
            updated_at = NOW()
        """
        
        # Extract details if provided
        hostname = details.get('hostname', 'unknown') if details else 'unknown'
        capabilities = json.dumps(details.get('capabilities', {})) if details else '{}'
        resources = json.dumps(details.get('resources', {})) if details else '{}'
        
        self.execute_query(query, (node_id, hostname, status, capabilities, resources))
        logger.debug(f"Updated node status for {node_id}: {status}")
    
    def record_node_heartbeat(self, node_id: str):
        """Record node heartbeat"""
        query = """
        UPDATE node_status 
        SET last_heartbeat = NOW() 
        WHERE node_id = %s
        """
        self.execute_query(query, (node_id,))
    
    # Worker management methods
    def register_worker(self, worker_info: Dict[str, Any]) -> bool:
        """Register a worker in database"""
        query = """
        INSERT INTO node_workers (
            worker_id, node_id, worker_type, status, capabilities, 
            resource_allocation, created_at, updated_at
        ) VALUES (%s, %s, %s, %s, %s, %s, NOW(), NOW())
        ON CONFLICT (worker_id) 
        DO UPDATE SET 
            worker_type = EXCLUDED.worker_type,
            status = EXCLUDED.status,
            capabilities = EXCLUDED.capabilities,
            resource_allocation = EXCLUDED.resource_allocation,
            updated_at = NOW()
        """
        
        try:
            self.execute_query(query, (
                worker_info['worker_id'],
                worker_info['node_id'],
                worker_info['worker_type'],
                worker_info.get('status', 'initializing'),
                json.dumps(worker_info.get('capabilities', {})),
                json.dumps(worker_info.get('resource_allocation', {}))
            ))
            logger.debug(f"Registered worker {worker_info['worker_id']}")
            return True
        except Exception as e:
            logger.error(f"Failed to register worker {worker_info.get('worker_id')}: {e}")
            return False
    
    def update_worker_status(self, worker_id: str, status: str, details: Optional[Dict[str, Any]] = None):
        """Update worker status"""
        query = """
        UPDATE node_workers 
        SET status = %s, last_heartbeat = NOW(), updated_at = NOW()
        WHERE worker_id = %s
        """
        self.execute_query(query, (status, worker_id))
        
        # Update current task if provided
        if details and 'current_task_id' in details:
            task_query = """
            UPDATE node_workers 
            SET current_task_id = %s 
            WHERE worker_id = %s
            """
            self.execute_query(task_query, (details['current_task_id'], worker_id))
    
    def get_active_workers(self) -> List[Dict[str, Any]]:
        """Get list of active workers"""
        query = """
        SELECT * FROM node_workers 
        WHERE status IN ('ready', 'busy', 'idle')
        ORDER BY created_at
        """
        return self.execute_query(query)
    
    # Task management methods
    def create_task_record(self, task_info: Dict[str, Any]) -> str:
        """Create task record and return task ID"""
        query = """
        INSERT INTO node_tasks (
            task_id, node_id, task_type, task_data, status, priority, created_at
        ) VALUES (%s, %s, %s, %s, %s, %s, NOW())
        RETURNING task_id
        """
        
        result = self.execute_query(query, (
            task_info['task_id'],
            task_info['node_id'],
            task_info['task_type'],
            json.dumps(task_info['task_data']),
            task_info.get('status', 'pending'),
            task_info.get('priority', 0)
        ))
        
        return result[0]['task_id'] if result else task_info['task_id']
    
    def update_task_status(self, task_id: str, status: str, result: Optional[Any] = None):
        """Update task status and result"""
        if status == 'running':
            query = """
            UPDATE node_tasks 
            SET status = %s, started_at = NOW() 
            WHERE task_id = %s
            """
            self.execute_query(query, (status, task_id))
        elif status in ['completed', 'failed']:
            query = """
            UPDATE node_tasks 
            SET status = %s, completed_at = NOW(), result = %s 
            WHERE task_id = %s
            """
            result_json = json.dumps(result) if result is not None else None
            self.execute_query(query, (status, result_json, task_id))
        else:
            query = """
            UPDATE node_tasks 
            SET status = %s 
            WHERE task_id = %s
            """
            self.execute_query(query, (status, task_id))
    
    def get_task_history(self, limit: int = 100) -> List[Dict[str, Any]]:
        """Get task execution history"""
        query = """
        SELECT * FROM node_tasks 
        ORDER BY created_at DESC 
        LIMIT %s
        """
        return self.execute_query(query, (limit,))
    
    # Resource monitoring methods
    def record_resource_usage(self, resource_data: Dict[str, Any]):
        """Record resource usage metrics"""
        query = """
        INSERT INTO node_resource_metrics (
            node_id, cpu_usage, memory_usage, memory_total,
            gpu_memory_usage, gpu_memory_total, disk_usage, disk_total,
            network_rx, network_tx, timestamp
        ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, NOW())
        """
        
        self.execute_query(query, (
            resource_data['node_id'],
            resource_data.get('cpu_usage', 0.0),
            resource_data.get('memory_usage', 0),
            resource_data.get('memory_total', 0),
            json.dumps(resource_data.get('gpu_memory_usage', {})),
            json.dumps(resource_data.get('gpu_memory_total', {})),
            resource_data.get('disk_usage', 0),
            resource_data.get('disk_total', 0),
            resource_data.get('network_rx', 0),
            resource_data.get('network_tx', 0)
        ))
    
    def get_resource_history(self, hours: int = 24) -> List[Dict[str, Any]]:
        """Get resource usage history"""
        query = """
        SELECT * FROM node_resource_metrics 
        WHERE timestamp >= NOW() - INTERVAL '%s hours'
        ORDER BY timestamp DESC
        """
        return self.execute_query(query, (hours,))
    
    def cleanup_old_records(self, days: int = 7):
        """Clean up old records"""
        # Delete old completed/failed tasks
        task_cleanup_query = """
        DELETE FROM node_tasks 
        WHERE status IN ('completed', 'failed') 
        AND completed_at < NOW() - INTERVAL '%s days'
        """
        
        # Delete old resource metrics
        metrics_cleanup_query = """
        DELETE FROM node_resource_metrics 
        WHERE timestamp < NOW() - INTERVAL '%s days'
        """
        
        try:
            task_result = self.execute_query(task_cleanup_query, (days,))
            metrics_result = self.execute_query(metrics_cleanup_query, (days,))
            logger.info(f"Cleaned up old records older than {days} days")
        except Exception as e:
            logger.error(f"Failed to cleanup old records: {e}")
    
    def close(self):
        """Close database connections"""
        if self.connection_pool:
            self.connection_pool.closeall()
        logger.info("NodeDatabase connections closed")
