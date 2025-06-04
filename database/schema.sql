-- Node Manager Database Schema
-- PostgreSQL schema for node-level data persistence

-- Nodes table - stores node information
CREATE TABLE IF NOT EXISTS nodes (
    node_id VARCHAR(50) PRIMARY KEY,
    hostname VARCHAR(255) NOT NULL,
    ip_address INET NOT NULL,
    port INTEGER NOT NULL DEFAULT 8080,
    status VARCHAR(20) NOT NULL DEFAULT 'initializing',
    capabilities JSONB DEFAULT '{}',
    resources JSONB DEFAULT '{}',
    last_heartbeat TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Workers table - stores worker process information
CREATE TABLE IF NOT EXISTS workers (
    worker_id VARCHAR(50) PRIMARY KEY,
    node_id VARCHAR(50) REFERENCES nodes(node_id) ON DELETE CASCADE,
    worker_type VARCHAR(50) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'starting',
    capabilities JSONB DEFAULT '{}',
    resource_allocation JSONB DEFAULT '{}',
    current_task_id VARCHAR(50),
    error_count INTEGER DEFAULT 0,
    total_tasks INTEGER DEFAULT 0,
    last_heartbeat TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Tasks table - stores task execution information
CREATE TABLE IF NOT EXISTS tasks (
    task_id VARCHAR(50) PRIMARY KEY,
    node_id VARCHAR(50) REFERENCES nodes(node_id) ON DELETE CASCADE,
    worker_id VARCHAR(50) REFERENCES workers(worker_id) ON DELETE SET NULL,
    task_type VARCHAR(50) NOT NULL,
    task_data JSONB NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'queued',
    priority INTEGER DEFAULT 0,
    result JSONB,
    error TEXT,
    retry_count INTEGER DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    started_at TIMESTAMP WITH TIME ZONE,
    completed_at TIMESTAMP WITH TIME ZONE
);

-- Resource metrics table - stores system resource usage
CREATE TABLE IF NOT EXISTS resource_metrics (
    id SERIAL PRIMARY KEY,
    node_id VARCHAR(50) REFERENCES nodes(node_id) ON DELETE CASCADE,
    timestamp TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    cpu_usage FLOAT NOT NULL,
    memory_usage BIGINT NOT NULL,
    memory_total BIGINT NOT NULL,
    gpu_memory_usage JSONB DEFAULT '{}',
    gpu_memory_total JSONB DEFAULT '{}',
    disk_usage BIGINT NOT NULL,
    disk_total BIGINT NOT NULL,
    network_rx BIGINT DEFAULT 0,
    network_tx BIGINT DEFAULT 0
);

-- Health checks table - stores health check results
CREATE TABLE IF NOT EXISTS health_checks (
    id SERIAL PRIMARY KEY,
    node_id VARCHAR(50) REFERENCES nodes(node_id) ON DELETE CASCADE,
    check_name VARCHAR(100) NOT NULL,
    status VARCHAR(20) NOT NULL,
    details JSONB DEFAULT '{}',
    timestamp TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_workers_node_id ON workers(node_id);
CREATE INDEX IF NOT EXISTS idx_workers_status ON workers(status);
CREATE INDEX IF NOT EXISTS idx_workers_type ON workers(worker_type);

CREATE INDEX IF NOT EXISTS idx_tasks_node_id ON tasks(node_id);
CREATE INDEX IF NOT EXISTS idx_tasks_worker_id ON tasks(worker_id);
CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON tasks(created_at);

CREATE INDEX IF NOT EXISTS idx_resource_metrics_node_id ON resource_metrics(node_id);
CREATE INDEX IF NOT EXISTS idx_resource_metrics_timestamp ON resource_metrics(timestamp);

CREATE INDEX IF NOT EXISTS idx_health_checks_node_id ON health_checks(node_id);
CREATE INDEX IF NOT EXISTS idx_health_checks_timestamp ON health_checks(timestamp);

-- Functions for automatic timestamp updates
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Triggers for automatic timestamp updates
CREATE TRIGGER update_nodes_updated_at BEFORE UPDATE ON nodes
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_workers_updated_at BEFORE UPDATE ON workers
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Cleanup function for old records
CREATE OR REPLACE FUNCTION cleanup_old_records(days_to_keep INTEGER DEFAULT 7)
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER := 0;
BEGIN
    -- Delete old completed tasks
    DELETE FROM tasks 
    WHERE status IN ('completed', 'failed', 'cancelled') 
    AND completed_at < NOW() - INTERVAL '1 day' * days_to_keep;
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    
    -- Delete old resource metrics
    DELETE FROM resource_metrics 
    WHERE timestamp < NOW() - INTERVAL '1 day' * days_to_keep;
    
    GET DIAGNOSTICS deleted_count = deleted_count + ROW_COUNT;
    
    -- Delete old health checks
    DELETE FROM health_checks 
    WHERE timestamp < NOW() - INTERVAL '1 day' * days_to_keep;
    
    GET DIAGNOSTICS deleted_count = deleted_count + ROW_COUNT;
    
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;
