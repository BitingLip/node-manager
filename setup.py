#!/usr/bin/env python3
"""
Node Manager Setup Script
Automates the setup process for the BitingLip Node Manager
"""

import os
import sys
import subprocess
import argparse
import json
from pathlib import Path
import psycopg2
from psycopg2.extensions import ISOLATION_LEVEL_AUTOCOMMIT


def check_python_version():
    """Check if Python version is compatible"""
    if sys.version_info < (3, 10):
        print("Error: Python 3.10 or higher is required")
        sys.exit(1)
    print(f"✓ Python {sys.version.split()[0]} detected")


def install_dependencies():
    """Install Python dependencies"""
    print("Installing dependencies...")
    try:
        subprocess.check_call([sys.executable, "-m", "pip", "install", "-r", "requirements.txt"])
        print("✓ Dependencies installed successfully")
    except subprocess.CalledProcessError as e:
        print(f"Error installing dependencies: {e}")
        sys.exit(1)


def check_postgresql():
    """Check if PostgreSQL is available"""
    try:
        # Try to connect to default postgres database
        conn = psycopg2.connect(
            host="localhost",
            port=5432,
            database="postgres",
            user="postgres"
        )
        conn.close()
        print("✓ PostgreSQL is accessible")
        return True
    except psycopg2.Error as e:
        print(f"Warning: PostgreSQL not accessible: {e}")
        return False


def create_database(db_name: str, user: str = "postgres", password: str = None):
    """Create database if it doesn't exist"""
    try:
        # Connect to default database
        conn = psycopg2.connect(
            host="localhost",
            port=5432,
            database="postgres",
            user=user,
            password=password
        )
        conn.set_isolation_level(ISOLATION_LEVEL_AUTOCOMMIT)
        
        cursor = conn.cursor()
        
        # Check if database exists
        cursor.execute("SELECT 1 FROM pg_database WHERE datname = %s", (db_name,))
        exists = cursor.fetchone()
        
        if not exists:
            cursor.execute(f'CREATE DATABASE "{db_name}"')
            print(f"✓ Database '{db_name}' created")
        else:
            print(f"✓ Database '{db_name}' already exists")
        
        cursor.close()
        conn.close()
        
        return True
        
    except psycopg2.Error as e:
        print(f"Error creating database: {e}")
        return False


def initialize_schema(db_name: str, user: str = "postgres", password: str = None):
    """Initialize database schema"""
    try:
        # Read schema file
        schema_path = Path(__file__).parent / "database" / "schema.sql"
        if not schema_path.exists():
            print("Error: schema.sql not found")
            return False
        
        with open(schema_path, 'r') as f:
            schema_sql = f.read()
        
        # Connect to target database
        conn = psycopg2.connect(
            host="localhost",
            port=5432,
            database=db_name,
            user=user,
            password=password
        )
        
        cursor = conn.cursor()
        cursor.execute(schema_sql)
        conn.commit()
        
        cursor.close()
        conn.close()
        
        print("✓ Database schema initialized")
        return True
        
    except Exception as e:
        print(f"Error initializing schema: {e}")
        return False


def create_config_file(config_path: str, **config_options):
    """Create node configuration file"""
    config = {
        "node_id": config_options.get("node_id", "node-001"),
        "cluster_manager_url": config_options.get("cluster_url", "http://localhost:8000"),
        "api_port": config_options.get("api_port", 8080),
        "max_workers": config_options.get("max_workers", 4),
        "database": {
            "host": "localhost",
            "port": 5432,
            "database": config_options.get("db_name", "bitinglip_nodes"),
            "user": config_options.get("db_user", "postgres"),
            "password": config_options.get("db_password", "")
        },
        "resources": {
            "max_cpu_usage": 80,
            "max_memory_usage": 85,
            "reserve_memory_mb": 2048
        },
        "workers": {
            "llm": {
                "max_instances": 2,
                "gpu_memory_gb": 4,
                "models": []
            },
            "stable_diffusion": {
                "max_instances": 1,
                "gpu_memory_gb": 8,
                "models": []
            },
            "tts": {
                "max_instances": 1,
                "gpu_memory_gb": 2,
                "models": []
            }
        },
        "monitoring": {
            "check_interval": 60,
            "metrics_retention_hours": 24
        }
    }
    
    with open(config_path, 'w') as f:
        json.dump(config, f, indent=2)
    
    print(f"✓ Configuration file created: {config_path}")


def create_env_file():
    """Create environment file"""
    env_content = """# BitingLip Node Manager Environment Variables

# Node Configuration
NODE_ID=node-001
CLUSTER_MANAGER_URL=http://localhost:8000
API_PORT=8080

# Database Configuration
DB_HOST=localhost
DB_PORT=5432
DB_NAME=bitinglip_nodes
DB_USER=postgres
DB_PASSWORD=

# Logging
LOG_LEVEL=INFO

# Security (generate your own keys!)
API_SECRET_KEY=your-secret-key-here
"""
    
    with open(".env", 'w') as f:
        f.write(env_content)
    
    print("✓ Environment file created: .env")


def main():
    """Main setup function"""
    parser = argparse.ArgumentParser(description="Setup BitingLip Node Manager")
    
    parser.add_argument("--node-id", default="node-001", help="Node identifier")
    parser.add_argument("--cluster-url", default="http://localhost:8000", help="Cluster manager URL")
    parser.add_argument("--api-port", type=int, default=8080, help="API server port")
    parser.add_argument("--db-name", default="bitinglip_nodes", help="Database name")
    parser.add_argument("--db-user", default="postgres", help="Database user")
    parser.add_argument("--db-password", help="Database password")
    parser.add_argument("--config-file", default="node_config.json", help="Configuration file path")
    parser.add_argument("--skip-db", action="store_true", help="Skip database setup")
    parser.add_argument("--skip-deps", action="store_true", help="Skip dependency installation")
    
    args = parser.parse_args()
    
    print("BitingLip Node Manager Setup")
    print("=" * 40)
    
    # Check Python version
    check_python_version()
    
    # Install dependencies
    if not args.skip_deps:
        install_dependencies()
    
    # Database setup
    if not args.skip_db:
        if check_postgresql():
            if create_database(args.db_name, args.db_user, args.db_password):
                initialize_schema(args.db_name, args.db_user, args.db_password)
        else:
            print("Skipping database setup (PostgreSQL not available)")
    
    # Create configuration files
    create_config_file(
        args.config_file,
        node_id=args.node_id,
        cluster_url=args.cluster_url,
        api_port=args.api_port,
        db_name=args.db_name,
        db_user=args.db_user,
        db_password=args.db_password or ""
    )
    
    create_env_file()
    
    print("\n" + "=" * 40)
    print("Setup completed successfully!")
    print("\nNext steps:")
    print(f"1. Review configuration in {args.config_file}")
    print("2. Update database credentials in .env file")
    print("3. Start the node manager:")
    print(f"   python main.py --config {args.config_file}")
    print("\nFor more information, see README.md")


if __name__ == "__main__":
    main()
