"""
Message Queue
Local message queue for internal communication within the node manager
"""

import asyncio
from typing import Dict, List, Optional, Any, Callable
from datetime import datetime
import structlog

logger = structlog.get_logger(__name__)


class MessageQueue:
    """
    Local message queue for asynchronous communication
    Provides pub/sub functionality for internal communication
    """
    
    def __init__(self):
        """Initialize message queue"""
        self.queues = {}  # queue_name -> asyncio.Queue
        self.subscribers = {}  # topic -> list of callbacks
        self.message_history = []  # For debugging
        
        # Default queues
        self._create_default_queues()
        
        logger.info("MessageQueue initialized")
    
    def _create_default_queues(self):
        """Create default message queues"""
        # Task queue (high priority)
        self.queues['tasks'] = asyncio.Queue(maxsize=100)
        
        # Status updates queue
        self.queues['status'] = asyncio.Queue(maxsize=50)
        
        # Error queue
        self.queues['errors'] = asyncio.Queue(maxsize=50)
        
        # Heartbeat queue
        self.queues['heartbeat'] = asyncio.Queue(maxsize=10)
        
        # General communication queue
        self.queues['communication'] = asyncio.Queue(maxsize=100)
        
        logger.info("Created default message queues")

    async def put_message(self, queue_name: str, message: Dict[str, Any], priority: int = 0):
        """Put a message in a queue"""
        if queue_name not in self.queues:
            logger.error(f"Queue '{queue_name}' does not exist")
            return False
            
        try:
            # Add metadata to message
            enhanced_message = {
                'id': f"msg-{len(self.message_history)}",
                'timestamp': datetime.utcnow().isoformat(),
                'priority': priority,
                'queue': queue_name,
                'data': message
            }
            
            # Store in history for debugging
            self.message_history.append(enhanced_message)
            
            # Keep history manageable
            if len(self.message_history) > 1000:
                self.message_history = self.message_history[-500:]
            
            # Put message in queue (non-blocking)
            await self.queues[queue_name].put(enhanced_message)
            
            # Notify subscribers
            await self.publish(f"queue.{queue_name}.message", enhanced_message)
            
            logger.debug(f"Message added to queue '{queue_name}': {enhanced_message['id']}")
            return True
            
        except asyncio.QueueFull:
            logger.error(f"Queue '{queue_name}' is full")
            return False
        except Exception as e:
            logger.error(f"Error putting message in queue '{queue_name}': {e}")
            return False

    async def get_message(self, queue_name: str, timeout: Optional[float] = None) -> Optional[Dict[str, Any]]:
        """Get a message from a queue"""
        if queue_name not in self.queues:
            logger.error(f"Queue '{queue_name}' does not exist")
            return None
            
        try:
            if timeout:
                message = await asyncio.wait_for(
                    self.queues[queue_name].get(),
                    timeout=timeout
                )
            else:
                message = await self.queues[queue_name].get()
            
            logger.debug(f"Retrieved message from queue '{queue_name}': {message['id']}")
            return message
            
        except asyncio.TimeoutError:
            logger.debug(f"Timeout waiting for message from queue '{queue_name}'")
            return None
        except Exception as e:
            logger.error(f"Error getting message from queue '{queue_name}': {e}")
            return None

    def subscribe(self, topic: str, callback: Callable):
        """Subscribe to a topic for pub/sub messaging"""
        if topic not in self.subscribers:
            self.subscribers[topic] = []
        
        if callback not in self.subscribers[topic]:
            self.subscribers[topic].append(callback)
            logger.debug(f"Subscribed to topic '{topic}'")

    def unsubscribe(self, topic: str, callback: Callable):
        """Unsubscribe from a topic"""
        if topic in self.subscribers and callback in self.subscribers[topic]:
            self.subscribers[topic].remove(callback)
            logger.debug(f"Unsubscribed from topic '{topic}'")

    async def publish(self, topic: str, message: Dict[str, Any]):
        """Publish a message to all subscribers"""
        if topic not in self.subscribers:
            return
            
        for callback in self.subscribers[topic]:
            try:
                if asyncio.iscoroutinefunction(callback):
                    await callback(topic, message)
                else:
                    callback(topic, message)
            except Exception as e:
                logger.error(f"Error calling subscriber for topic '{topic}': {e}")

    def create_queue(self, queue_name: str, maxsize: int = 0):
        """Create a new queue"""
        if queue_name in self.queues:
            logger.warning(f"Queue '{queue_name}' already exists")
            return
            
        self.queues[queue_name] = asyncio.Queue(maxsize=maxsize)
        logger.info(f"Created queue '{queue_name}' with maxsize {maxsize}")

    def get_queue_status(self, queue_name: str) -> Dict[str, Any]:
        """Get status of a specific queue"""
        if queue_name not in self.queues:
            return {"error": f"Queue '{queue_name}' does not exist"}
            
        queue = self.queues[queue_name]
        return {
            "queue_name": queue_name,
            "size": queue.qsize(),
            "maxsize": queue.maxsize,
            "empty": queue.empty(),
            "full": queue.full()
        }

    def clear_queue(self, queue_name: str):
        """Clear all messages from a queue"""
        if queue_name not in self.queues:
            logger.error(f"Queue '{queue_name}' does not exist")
            return
            
        # Create new queue to replace the old one
        maxsize = self.queues[queue_name].maxsize
        self.queues[queue_name] = asyncio.Queue(maxsize=maxsize)
        logger.info(f"Cleared queue '{queue_name}'")

    def get_all_queue_status(self) -> Dict[str, Dict[str, Any]]:
        """Get status of all queues"""
        return {name: self.get_queue_status(name) for name in self.queues.keys()}

    def get_message_history(self, limit: int = 100) -> List[Dict[str, Any]]:
        """Get recent message history for debugging"""
        return self.message_history[-limit:]

    async def shutdown(self):
        """Shutdown the message queue system"""
        logger.info("Shutting down message queue system")
        
        # Clear all subscribers
        self.subscribers.clear()
        
        # Clear all queues
        for queue_name in list(self.queues.keys()):
            self.clear_queue(queue_name)
        
        logger.info("Message queue system shutdown complete")
