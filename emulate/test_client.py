"""
Node Test Client
Client for testing Node Manager API and functionality
"""

import asyncio
import aiohttp
import json
from typing import Dict, List, Optional, Any
import structlog
from datetime import datetime

logger = structlog.get_logger(__name__)


class NodeTestClient:
    """
    Test client for interacting with Node Manager API
    """
    
    def __init__(self, base_url: str = "http://localhost:8013"):
        self.base_url = base_url.rstrip('/')
        self.session: Optional[aiohttp.ClientSession] = None
        
    async def __aenter__(self):
        self.session = aiohttp.ClientSession()
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        if self.session:
            await self.session.close()
    
    async def _request(self, method: str, endpoint: str, **kwargs) -> Dict[str, Any]:
        """Make HTTP request to node manager"""
        if not self.session:
            raise RuntimeError("Client not initialized. Use 'async with' context manager.")
        
        url = f"{self.base_url}{endpoint}"
        
        try:
            async with self.session.request(method, url, **kwargs) as response:
                response.raise_for_status()
                return await response.json()
        except aiohttp.ClientError as e:
            logger.error("HTTP request failed", 
                        method=method, 
                        url=url, 
                        error=str(e))
            raise
    
    # ---- Health and Status ----
    
    async def get_health(self) -> Dict[str, Any]:
        """Get node health status"""
        return await self._request('GET', '/health')
    
    async def get_status(self) -> Dict[str, Any]:
        """Get detailed node status"""
        return await self._request('GET', '/status')
    
    async def get_metrics(self) -> Dict[str, Any]:
        """Get node performance metrics"""
        return await self._request('GET', '/metrics')
    
    # ---- Worker Management ----
    
    async def list_workers(self) -> Dict[str, Any]:
        """List all workers"""
        return await self._request('GET', '/workers')
    
    async def get_worker(self, worker_id: str) -> Dict[str, Any]:
        """Get specific worker information"""
        return await self._request('GET', f'/workers/{worker_id}')
    
    async def start_worker(self, worker_type: str, config: Optional[Dict] = None) -> Dict[str, Any]:
        """Start a new worker"""
        payload = {'worker_type': worker_type}
        if config:
            payload['config'] = config
        return await self._request('POST', '/workers', json=payload)
    
    async def stop_worker(self, worker_id: str) -> Dict[str, Any]:
        """Stop a specific worker"""
        return await self._request('DELETE', f'/workers/{worker_id}')
    
    # ---- Task Management ----
    
    async def submit_task(self, task_type: str, task_data: Dict[str, Any], 
                         priority: str = 'normal') -> Dict[str, Any]:
        """Submit a task for execution"""
        payload = {
            'task_type': task_type,
            'task_data': task_data,
            'priority': priority
        }
        return await self._request('POST', '/tasks', json=payload)
    
    async def get_task(self, task_id: str) -> Dict[str, Any]:
        """Get task status and results"""
        return await self._request('GET', f'/tasks/{task_id}')
    
    async def list_tasks(self, status: Optional[str] = None) -> Dict[str, Any]:
        """List tasks, optionally filtered by status"""
        params = {'status': status} if status else {}
        return await self._request('GET', '/tasks', params=params)
    
    async def cancel_task(self, task_id: str) -> Dict[str, Any]:
        """Cancel a running task"""
        return await self._request('DELETE', f'/tasks/{task_id}')
    
    # ---- Resource Information ----
    
    async def get_resources(self) -> Dict[str, Any]:
        """Get system resource information"""
        return await self._request('GET', '/resources')
    
    async def get_gpu_info(self) -> Dict[str, Any]:
        """Get GPU information"""
        return await self._request('GET', '/resources/gpu')
    
    # ---- Configuration ----
    
    async def get_config(self) -> Dict[str, Any]:
        """Get current configuration"""
        return await self._request('GET', '/config')
    
    async def update_config(self, config: Dict[str, Any]) -> Dict[str, Any]:
        """Update configuration"""
        return await self._request('PUT', '/config', json=config)
    
    # ---- Test Utilities ----
    
    async def run_health_check(self) -> Dict[str, Any]:
        """Run comprehensive health check"""
        results = {}
        
        try:
            results['health'] = await self.get_health()
            results['status'] = await self.get_status()
            results['workers'] = await self.list_workers()
            results['resources'] = await self.get_resources()
            results['overall_health'] = 'healthy'
        except Exception as e:
            results['error'] = str(e)
            results['overall_health'] = 'unhealthy'
        
        return results
    
    async def submit_test_task(self, task_type: str = 'text_generation') -> Dict[str, Any]:
        """Submit a test task and wait for completion"""
        test_data = {
            'text_generation': {
                'prompt': 'Write a short test message',
                'max_tokens': 50
            },
            'data_processing': {
                'data': [1, 2, 3, 4, 5],
                'operation': 'sum'
            },
            'text_to_speech': {
                'text': 'Hello world',
                'voice': 'en-US'
            }
        }
        
        task_data = test_data.get(task_type, {'test': True})
        
        # Submit task
        submit_result = await self.submit_task(task_type, task_data)
        task_id = submit_result.get('task_id')
        
        if not task_id:
            return {'error': 'No task_id returned'}
        
        # Poll for completion
        max_wait = 30  # seconds
        wait_time = 0
        
        while wait_time < max_wait:
            task_status = await self.get_task(task_id)
            status = task_status.get('status')
            
            if status in ['completed', 'failed', 'cancelled']:
                return {
                    'task_id': task_id,
                    'final_status': status,
                    'wait_time': wait_time,
                    'result': task_status
                }
            
            await asyncio.sleep(1)
            wait_time += 1
        
        return {
            'task_id': task_id,
            'error': 'Task did not complete within timeout',
            'wait_time': wait_time
        }
    
    async def stress_test(self, num_tasks: int = 10, 
                         task_types: Optional[List[str]] = None) -> Dict[str, Any]:
        """Run a stress test with multiple concurrent tasks"""
        if task_types is None:
            task_types = ['text_generation', 'data_processing']
        
        logger.info("Starting stress test", num_tasks=num_tasks)
        start_time = datetime.now()
        
        # Submit all tasks concurrently
        submit_tasks = []
        for i in range(num_tasks):
            task_type = task_types[i % len(task_types)]
            submit_tasks.append(self.submit_test_task(task_type))
        
        # Wait for all to complete
        results = await asyncio.gather(*submit_tasks, return_exceptions=True)
        
        end_time = datetime.now()
        duration = (end_time - start_time).total_seconds()        
        # Analyze results
        successful = 0
        failed = 0
        errors = []
        
        for result in results:
            if isinstance(result, Exception):
                failed += 1
                errors.append(str(result))
            elif isinstance(result, dict) and result.get('final_status') == 'completed':
                successful += 1
            else:
                failed += 1
                if isinstance(result, dict):
                    errors.append(result.get('error', 'Unknown error'))
                else:
                    errors.append('Unknown error')
        
        return {
            'total_tasks': num_tasks,
            'successful': successful,
            'failed': failed,
            'success_rate': successful / num_tasks,
            'duration_seconds': duration,
            'tasks_per_second': num_tasks / duration,
            'errors': errors[:5]  # First 5 errors
        }
