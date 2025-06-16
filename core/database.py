#!/usr/bin/env python3
"""
Database - Database interactions and management for Node Manager
"""
import time
from typing import Dict, Any, List, Optional
from datetime import datetime, timedelta

try:
    import psycopg2
    import psycopg2.extras
    POSTGRES_AVAILABLE = True
except ImportError:
    POSTGRES_AVAILABLE = False


class Database:
    """Database manager for node manager operations"""
    
    def __init__(self, config: Dict[str, Any], logger):
        # Config should be the database section directly
        self.config = config
        self.logger = logger
        self.connection = None
        self.connected = False
    
    def _create_database_if_not_exists(self) -> bool:
        """Create the database if it doesn't exist"""
        try:
            # Connect to the default 'postgres' database to create our target database
            temp_connection = psycopg2.connect(
                host=self.config.get("host", "localhost"),
                port=self.config.get("port", 5432),
                database="postgres",  # Connect to default postgres database
                user=self.config.get("user", "postgres"),
                password=self.config.get("password", "password"),
                connect_timeout=self.config.get("connection_timeout", 30)
            )
            temp_connection.autocommit = True
            
            target_db = self.config.get("name", "node_manager")
            
            with temp_connection.cursor() as cursor:
                # Check if database exists
                cursor.execute(
                    "SELECT 1 FROM pg_database WHERE datname = %s",
                    (target_db,)
                )
                
                if not cursor.fetchone():
                    # Database doesn't exist, create it
                    self.logger.info(f"Creating database '{target_db}'...")
                    cursor.execute(f'CREATE DATABASE "{target_db}"')
                    self.logger.info(f"Database '{target_db}' created successfully")
                else:
                    self.logger.info(f"Database '{target_db}' already exists")
            
            temp_connection.close()
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to create database: {e}")
            return False

    def connect(self) -> bool:
        """Connect to PostgreSQL database"""
        try:
            # First, try to create the database if it doesn't exist
            if not self._create_database_if_not_exists():
                self.logger.warning("Failed to create database, but will try to connect anyway")
            
            # Now connect to the target database
            self.connection = psycopg2.connect(
                host=self.config.get("host", "localhost"),
                port=self.config.get("port", 5432),
                database=self.config.get("name", "node_manager"),
                user=self.config.get("user", "postgres"),
                password=self.config.get("password", "password"),
                connect_timeout=self.config.get("connection_timeout", 30)
            )
            self.connection.autocommit = True
            self.connected = True
            
            # Initialize tables
            self._initialize_tables()
            
            self.logger.info("Database connected successfully")
            return True
            
        except psycopg2.OperationalError as e:
            if "does not exist" in str(e):
                self.logger.error(f"Database does not exist and could not be created: {e}")
            elif "authentication failed" in str(e):
                self.logger.error(f"Database authentication failed. Please check credentials: {e}")
            else:
                self.logger.error(f"Database connection failed: {e}")
            self.connected = False
            return False
        except Exception as e:
            self.logger.error(f"Database connection failed: {e}")
            self.connected = False
            return False
    
    def disconnect(self):
        """Disconnect from database"""
        if self.connection:
            try:
                self.connection.close()
                self.connected = False
                self.logger.info("Database disconnected")
            except Exception as e:
                self.logger.error(f"Error disconnecting from database: {e}")
    
    def _initialize_tables(self):
        """Initialize database tables"""
        tables = [
            """
            CREATE TABLE IF NOT EXISTS workers (
                worker_id VARCHAR(50) PRIMARY KEY,
                device_id INTEGER,
                status VARCHAR(20),
                current_model VARCHAR(255),
                vram_usage_mb INTEGER DEFAULT 0,
                last_activity TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                error_message TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
            """,            """
            CREATE TABLE IF NOT EXISTS tasks (
                task_id VARCHAR(100) PRIMARY KEY,
                prompt TEXT NOT NULL,
                negative_prompt TEXT,
                width INTEGER DEFAULT 832,
                height INTEGER DEFAULT 1216,
                steps INTEGER DEFAULT 15,
                guidance_scale FLOAT DEFAULT 7.0,
                seed INTEGER,
                status VARCHAR(20) DEFAULT 'queued',
                worker_id VARCHAR(50),
                model_name VARCHAR(255),
                submit_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                start_time TIMESTAMP,
                completion_time TIMESTAMP,
                output_path TEXT,
                error_message TEXT,
                processing_time_seconds FLOAT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (worker_id) REFERENCES workers(worker_id)
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS models (
                model_name VARCHAR(255) PRIMARY KEY,
                model_path TEXT NOT NULL,
                size_mb INTEGER,
                last_used TIMESTAMP,
                usage_count INTEGER DEFAULT 0,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS system_metrics (
                id SERIAL PRIMARY KEY,
                timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                total_ram_gb FLOAT,
                used_ram_gb FLOAT,
                available_ram_gb FLOAT,
                ram_percent FLOAT,
                active_tasks INTEGER DEFAULT 0,
                queued_tasks INTEGER DEFAULT 0,
                completed_tasks INTEGER DEFAULT 0
            )
            """,
            """
            CREATE TABLE IF NOT EXISTS worker_metrics (
                id SERIAL PRIMARY KEY,
                worker_id VARCHAR(50),
                timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                vram_used_mb INTEGER,
                vram_total_mb INTEGER,
                gpu_utilization_percent FLOAT,
                temperature_celsius FLOAT,
                power_usage_watts FLOAT,                FOREIGN KEY (worker_id) REFERENCES workers(worker_id)
            )
            """
        ]
        
        try:
            with self.connection.cursor() as cursor:
                for table_sql in tables:
                    cursor.execute(table_sql)
                    
                # Run schema migrations for existing tables
                self._run_schema_migrations(cursor)
                    
            self.logger.info("Database tables initialized")
        except Exception as e:
            self.logger.error(f"Failed to initialize tables: {e}")
    
    def _run_schema_migrations(self, cursor):
        """Run schema migrations for existing tables"""
        try:
            # Migration 1: Add updated_at column to tasks table if it doesn't exist
            cursor.execute("""
                SELECT column_name 
                FROM information_schema.columns 
                WHERE table_name = 'tasks' AND column_name = 'updated_at'
            """)
            
            if not cursor.fetchone():
                self.logger.info("Adding updated_at column to tasks table...")
                cursor.execute("""
                    ALTER TABLE tasks 
                    ADD COLUMN updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                """)
                self.logger.info("Added updated_at column to tasks table")
            
        except Exception as e:
            self.logger.error(f"Failed to run schema migrations: {e}")
    
    def register_worker(self, worker_id: str, device_id: int) -> bool:
        """Register a new worker"""
        if not self.connection or not self.connected:
            self.logger.error("Database connection is not available. Cannot register worker.")
            return False
        if not self.connection or not self.connected:
            self.logger.error("Database connection is not available. Cannot register worker.")
            return False
        try:
            with self.connection.cursor() as cursor:
                cursor.execute("""
                    INSERT INTO workers (worker_id, device_id, status, last_activity)
                    VALUES (%s, %s, 'starting', CURRENT_TIMESTAMP)
                    ON CONFLICT (worker_id) DO UPDATE SET
                        device_id = EXCLUDED.device_id,
                        status = 'starting',
                        last_activity = CURRENT_TIMESTAMP,
                        updated_at = CURRENT_TIMESTAMP
                """, (worker_id, device_id))
            
            self.logger.info(f"Worker {worker_id} registered")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to register worker {worker_id}: {e}")
            return False
    
    def update_worker_status(self, worker_id: str, status: str, current_model: Optional[str] = None, 
                           vram_usage_mb: int = 0, error_message: Optional[str] = None) -> bool:
        """Update worker status"""
        try:
            with self.connection.cursor() as cursor:
                cursor.execute("""
                    UPDATE workers SET
                        status = %s,
                        current_model = %s,
                        vram_usage_mb = %s,
                        error_message = %s,
                        last_activity = CURRENT_TIMESTAMP,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE worker_id = %s
                """, (status, current_model, vram_usage_mb, error_message, worker_id))
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to update worker status: {e}")
            return False
    
    def get_worker_status(self, worker_id: str = None) -> List[Dict[str, Any]]:
        """Get worker status (all workers if worker_id is None)"""
        try:
            with self.connection.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cursor:
                if worker_id:
                    cursor.execute("SELECT * FROM workers WHERE worker_id = %s", (worker_id,))
                    result = cursor.fetchone()
                    return [dict(result)] if result else []
                else:
                    cursor.execute("SELECT * FROM workers ORDER BY device_id")
                    return [dict(row) for row in cursor.fetchall()]
        
        except Exception as e:
            self.logger.error(f"Failed to get worker status: {e}")
            return []
    
    def store_task(self, task_config: Dict[str, Any]) -> bool:
        """Store a new task"""
        if not self.connection or not self.connected:
            self.logger.error("Database connection is not available. Cannot store task.")
            return False
        try:
            with self.connection.cursor() as cursor:
                cursor.execute("""
                    INSERT INTO tasks (
                        task_id, prompt, negative_prompt, width, height, steps,
                        guidance_scale, seed, status
                    ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
                """, (
                    task_config.get('task_id'),
                    task_config.get('prompt'),
                    task_config.get('negative_prompt'),
                    task_config.get('width', 832),
                    task_config.get('height', 1216),
                    task_config.get('steps', 15),
                    task_config.get('guidance_scale', 7.0),
                    task_config.get('seed'),
                    'queued'
                ))
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to store task: {e}")
            return False

    def update_task_status(self, task_id: str, status: str, worker_id: Optional[str] = None,
                          output_path: Optional[str] = None, error_message: Optional[str] = None,
                          processing_time: Optional[float] = None) -> bool:
        """Update task status with enhanced status transitions"""
        try:
            with self.connection.cursor() as cursor:
                update_fields = ["status = %s", "updated_at = CURRENT_TIMESTAMP"]
                values = [status]
                
                # Handle different status transitions
                if status == 'assigned' and worker_id:
                    update_fields.append("worker_id = %s")
                    values.append(worker_id)
                
                elif status == 'running' and worker_id:
                    update_fields.append("worker_id = %s")
                    update_fields.append("start_time = CURRENT_TIMESTAMP")
                    values.append(worker_id)
                
                elif status == 'processing' and worker_id:  # Keep for backward compatibility
                    update_fields.append("worker_id = %s")
                    update_fields.append("start_time = CURRENT_TIMESTAMP")
                    values.append(worker_id)
                
                elif status == 'completed':
                    update_fields.append("completion_time = CURRENT_TIMESTAMP")
                    if output_path:
                        update_fields.append("output_path = %s")
                        values.append(output_path)
                    if processing_time:
                        update_fields.append("processing_time_seconds = %s")
                        values.append(processing_time)
                
                elif status == 'failed':
                    update_fields.append("completion_time = CURRENT_TIMESTAMP")
                    if error_message:
                        update_fields.append("error_message = %s")
                        values.append(error_message)
                
                values.append(task_id)
                
                cursor.execute(f"""
                    UPDATE tasks SET {', '.join(update_fields)}
                    WHERE task_id = %s
                """, values)
                
                # Log the update for debugging
                affected_rows = cursor.rowcount
                if affected_rows > 0:
                    self.logger.debug(f"Task {task_id} status updated to {status}")
                else:
                    self.logger.warning(f"No task found with ID {task_id} to update")
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to update task status: {e}")
            return False
    
    def get_task_status(self, task_id: str = None) -> List[Dict[str, Any]]:
        """Get task status (all tasks if task_id is None)"""
        try:
            with self.connection.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cursor:
                if task_id:
                    cursor.execute("SELECT * FROM tasks WHERE task_id = %s", (task_id,))
                    result = cursor.fetchone()
                    return [dict(result)] if result else []
                else:
                    cursor.execute("""
                        SELECT * FROM tasks 
                        ORDER BY submit_time DESC 
                        LIMIT 100
                    """)
                    return [dict(row) for row in cursor.fetchall()]
        
        except Exception as e:
            self.logger.error(f"Failed to get task status: {e}")
            return []
    
    def store_model_info(self, model_name: str, model_path: str, size_mb: int) -> bool:
        """Store model information"""
        try:
            with self.connection.cursor() as cursor:
                cursor.execute("""
                    INSERT INTO models (model_name, model_path, size_mb)
                    VALUES (%s, %s, %s)
                    ON CONFLICT (model_name) DO UPDATE SET
                        model_path = EXCLUDED.model_path,
                        size_mb = EXCLUDED.size_mb
                """, (model_name, model_path, size_mb))
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to store model info: {e}")
            return False
    
    def get_model_info(self, model_name: str) -> Optional[Dict[str, Any]]:
        """Get model information by name"""
        try:
            with self.connection.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cursor:
                cursor.execute("""
                    SELECT model_name, model_path, size_mb, last_used, usage_count
                    FROM models 
                    WHERE model_name = %s
                """, (model_name,))
                
                row = cursor.fetchone()
                if row:
                    return dict(row)
                return None
                
        except Exception as e:
            self.logger.error(f"Error getting model info: {e}")
            return None
    
    def update_model_usage(self, model_name: str) -> bool:
        """Update model usage statistics"""
        try:
            with self.connection.cursor() as cursor:
                cursor.execute("""
                    UPDATE models SET
                        last_used = CURRENT_TIMESTAMP,
                        usage_count = usage_count + 1
                    WHERE model_name = %s
                """, (model_name,))
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to update model usage: {e}")
            return False
    
    def store_system_metrics(self, metrics: Dict[str, Any]) -> bool:
        """Store system metrics"""
        try:
            with self.connection.cursor() as cursor:
                cursor.execute("""
                    INSERT INTO system_metrics (
                        total_ram_gb, used_ram_gb, available_ram_gb, ram_percent,
                        active_tasks, queued_tasks, completed_tasks
                    ) VALUES (%s, %s, %s, %s, %s, %s, %s)
                """, (
                    metrics.get('total_ram_gb'),
                    metrics.get('used_ram_gb'),
                    metrics.get('available_ram_gb'),
                    metrics.get('ram_percent'),
                    metrics.get('active_tasks', 0),
                    metrics.get('queued_tasks', 0),
                    metrics.get('completed_tasks', 0)
                ))
            
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to store system metrics: {e}")
            return False
    
    def cleanup_old_records(self, days_to_keep: int = 7) -> bool:
        """Clean up old records"""
        if not self.connection or not self.connected:
            self.logger.error("Database connection is not available. Cannot cleanup old records.")
            return False
        try:
            cutoff_date = datetime.now() - timedelta(days=days_to_keep)
            
            with self.connection.cursor() as cursor:
                # Clean old system metrics
                cursor.execute("""
                    DELETE FROM system_metrics 
                    WHERE timestamp < %s
                """, (cutoff_date,))
                
                # Clean old worker metrics
                cursor.execute("""
                    DELETE FROM worker_metrics 
                    WHERE timestamp < %s
                """, (cutoff_date,))
                
                # Clean old completed tasks (keep failed tasks longer)
                cursor.execute("""
                    DELETE FROM tasks 
                    WHERE status = 'completed' AND completion_time < %s
                """, (cutoff_date,))
            
            self.logger.info("Old records cleaned up")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to cleanup old records: {e}")
            return False
    
    def create_task_record(self, task_info: Dict[str, Any]) -> bool:
        """Create task record and return success status"""
        try:
            task_id = task_info['task_id']
            
            query = """
            INSERT INTO tasks (
                task_id, prompt, negative_prompt, width, height, steps,
                guidance_scale, seed, status, model_name, submit_time
            ) VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, CURRENT_TIMESTAMP)
            """
            
            params = (
                task_id,
                task_info.get('prompt', ''),
                task_info.get('negative_prompt', ''),
                task_info.get('width', 832),
                task_info.get('height', 1216),
                task_info.get('steps', 15),
                task_info.get('guidance_scale', 7.0),
                task_info.get('seed'),
                'queued',
                task_info.get('model_name', 'cyberrealistic_pony_v110')
            )
            
            with self.connection.cursor() as cursor:
                cursor.execute(query, params)
            
            self.logger.info(f"Task {task_id} created in database")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to create task record: {e}")
            return False

    def get_pending_tasks(self) -> List[Dict[str, Any]]:
        """Get pending tasks from database"""
        try:
            query = """
            SELECT task_id, prompt, negative_prompt, width, height, steps,
                   guidance_scale, seed, model_name, submit_time, status
            FROM tasks 
            WHERE status IN ('queued', 'submitted', 'assigned') 
            ORDER BY submit_time ASC
            LIMIT 50
            """
            
            with self.connection.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cursor:
                cursor.execute(query)
                return [dict(row) for row in cursor.fetchall()]
                
        except Exception as e:
            self.logger.error(f"Failed to get pending tasks: {e}")
            return []

    def task_exists(self, task_id: str) -> bool:
        """Check if task exists in database"""
        try:
            query = "SELECT 1 FROM tasks WHERE task_id = %s LIMIT 1"
            
            with self.connection.cursor() as cursor:
                cursor.execute(query, (task_id,))
                return cursor.fetchone() is not None
                
        except Exception as e:
            self.logger.error(f"Failed to check task existence: {e}")
            return False
