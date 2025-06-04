"""
Node Manager Main Entry Point
Main executable for starting the node manager service
"""

import asyncio
import signal
import sys
import argparse
import os
from pathlib import Path
import structlog

# Add the project root to the Python path
project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root))

from managers.node_manager import create_node_manager


def setup_logging(log_level: str = "INFO"):
    """Setup structured logging"""
    structlog.configure(
        processors=[
            structlog.stdlib.filter_by_level,
            structlog.stdlib.add_logger_name,
            structlog.stdlib.add_log_level,
            structlog.stdlib.PositionalArgumentsFormatter(),
            structlog.processors.TimeStamper(fmt="iso"),
            structlog.processors.StackInfoRenderer(),
            structlog.processors.format_exc_info,
            structlog.processors.UnicodeDecoder(),
            structlog.processors.JSONRenderer()
        ],
        context_class=dict,
        logger_factory=structlog.stdlib.LoggerFactory(),
        wrapper_class=structlog.stdlib.BoundLogger,
        cache_logger_on_first_use=True,
    )
    
    import logging
    logging.basicConfig(
        format="%(message)s",
        stream=sys.stdout,
        level=getattr(logging, log_level.upper())
    )


def parse_arguments():
    """Parse command line arguments"""
    parser = argparse.ArgumentParser(description="BitingLip Node Manager")
    
    parser.add_argument(
        "--config",
        type=str,
        help="Path to configuration file"
    )
    
    parser.add_argument(
        "--log-level",
        type=str,
        default="INFO",
        choices=["DEBUG", "INFO", "WARNING", "ERROR"],
        help="Logging level"
    )
    
    parser.add_argument(
        "--port",
        type=int,
        default=8080,
        help="API server port"
    )
    
    parser.add_argument(
        "--cluster-url",
        type=str,
        help="Cluster manager URL"
    )
    
    parser.add_argument(
        "--node-id",
        type=str,
        help="Node identifier"
    )
    
    return parser.parse_args()


async def main():
    """Main application entry point"""
    args = parse_arguments()
    
    # Setup logging
    setup_logging(args.log_level)
    logger = structlog.get_logger(__name__)
    
    logger.info("Starting BitingLip Node Manager", version="1.0.0")
    
    try:
        # Create node manager
        node_manager = create_node_manager(args.config)
        
        # Setup signal handlers for graceful shutdown
        def signal_handler(signum, frame):
            logger.info("Received shutdown signal", signal=signum)
            asyncio.create_task(node_manager.stop())
        
        signal.signal(signal.SIGTERM, signal_handler)
        signal.signal(signal.SIGINT, signal_handler)
        
        # Start node manager
        success = await node_manager.start()
        if not success:
            logger.error("Failed to start node manager")
            sys.exit(1)
        
        logger.info("Node Manager started successfully")
        
        # Keep running until shutdown signal
        try:
            while True:
                await asyncio.sleep(1)
        except asyncio.CancelledError:
            pass
            
    except Exception as e:
        logger.error("Failed to start node manager", error=str(e), exc_info=True)
        sys.exit(1)
    
    finally:
        logger.info("Node Manager shutting down")


if __name__ == "__main__":
    asyncio.run(main())
