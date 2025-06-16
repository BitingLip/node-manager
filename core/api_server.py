#!/usr/bin/env python3
"""
API Server - REST API endpoints for external communication
"""
import time
import threading
from typing import Dict, Any, Optional
from flask import Flask, request, jsonify


class APIServer:
    """Flask-based API server for external communication"""
    
    def __init__(self, config: Dict[str, Any], logger, node_manager=None):
        self.config = config
        self.logger = logger
        self.node_manager = node_manager
        
        # Flask app setup
        self.app = Flask(__name__)
        self.host = config.get("host", "localhost")
        self.port = config.get("port", 8080)
        
        # Server state
        self.running = False
        self.server_thread: Optional[threading.Thread] = None
        
        # Setup routes
        self._setup_routes()
        
        self.logger.info(f"APIServer initialized on {self.host}:{self.port}")
        
    def _setup_routes(self):
        """Setup Flask API routes"""
        
        @self.app.route('/api/tasks/submit', methods=['POST'])
        def submit_task():
            """Submit a new task via API"""
            try:
                data = request.get_json()
                
                if 'prompt' not in data:
                    return jsonify({'error': 'prompt required'}), 400
                
                if self.node_manager:
                    from core.task_manager import TaskConfig
                    
                    task_config = TaskConfig(
                        prompt=data['prompt'],
                        negative_prompt=data.get('negative_prompt', ''),
                        width=data.get('width', 832),
                        height=data.get('height', 1216),
                        steps=data.get('steps', 15),
                        guidance_scale=data.get('cfg_scale', 7.0),
                        seed=data.get('seed'),
                        task_id=data.get('task_id', ''),
                        model_name=data.get('model_name', data.get('model', 'cyberrealistic_pony_v110'))
                    )
                    
                    task_id = self.node_manager.submit_task(task_config)
                    
                    return jsonify({
                        'success': True,
                        'task_id': task_id,
                        'status': 'queued'
                    }), 200
                else:
                    task_id = f"task_{int(time.time() * 1000)}"
                    return jsonify({
                        'success': True,
                        'task_id': task_id,
                        'status': 'queued',
                        'note': 'Task accepted but node manager not connected'
                    }), 200
                
            except Exception as e:
                self.logger.error(f"Task submission error: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/api/tasks/<task_id>/status', methods=['GET'])
        def get_task_status(task_id):
            """Get status of a specific task"""
            try:
                if self.node_manager and self.node_manager.task_manager:
                    task_status = self.node_manager.task_manager.get_task_status(task_id)
                    
                    if task_status:
                        return jsonify({
                            'success': True,
                            'task_id': task_id,
                            'status': task_status.get('status', 'unknown'),
                            'details': task_status
                        }), 200
                    else:
                        return jsonify({
                            'success': False,
                            'error': f'Task {task_id} not found'
                        }), 404
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Task manager not available'
                    }), 503
                    
            except Exception as e:
                self.logger.error(f"Task status error: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/api/tasks/<task_id>/cancel', methods=['POST'])
        def cancel_task(task_id):
            """Cancel a specific task"""
            try:
                if self.node_manager and self.node_manager.task_manager:
                    success = self.node_manager.task_manager.cancel_task(task_id)
                    
                    if success:
                        return jsonify({
                            'success': True,
                            'task_id': task_id,
                            'status': 'cancelled'
                        }), 200
                    else:
                        return jsonify({
                            'success': False,
                            'error': f'Failed to cancel task {task_id}'
                        }), 400
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Task manager not available'
                    }), 503
                    
            except Exception as e:
                self.logger.error(f"Task cancellation error: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/api/workers/register', methods=['POST'])
        def register_worker():
            """Register a new worker"""
            try:
                data = request.get_json()
                
                worker_id = data.get('worker_id')
                device_id = data.get('device_id')
                capabilities = data.get('capabilities', {})
                
                if not worker_id or device_id is None:
                    return jsonify({'error': 'worker_id and device_id required'}), 400
                
                if self.node_manager and self.node_manager.worker_manager:
                    success = self.node_manager.worker_manager.register_worker(
                        worker_id, device_id, capabilities
                    )
                    
                    if success:
                        return jsonify({
                            'success': True,
                            'worker_id': worker_id,
                            'status': 'registered'
                        }), 200
                    else:
                        return jsonify({
                            'success': False,
                            'error': f'Failed to register worker {worker_id}'
                        }), 400
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Worker manager not available'
                    }), 503
                    
            except Exception as e:
                self.logger.error(f"Worker registration error: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/api/workers/<worker_id>/status', methods=['POST'])
        def update_worker_status(worker_id):
            """Update worker status"""
            try:
                data = request.get_json()
                
                status = data.get('status')
                current_task = data.get('current_task')
                
                if not status:
                    return jsonify({'error': 'status required'}), 400
                
                if self.node_manager and self.node_manager.worker_manager:
                    self.node_manager.worker_manager.update_worker_status(
                        worker_id, status, current_task
                    )
                    
                    return jsonify({
                        'success': True,
                        'worker_id': worker_id,
                        'status': status
                    }), 200
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Worker manager not available'
                    }), 503
                    
            except Exception as e:
                self.logger.error(f"Worker status update error: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/api/workers/<worker_id>/messages', methods=['GET'])
        def get_worker_messages(worker_id):
            """Get messages for a specific worker"""
            try:
                if self.node_manager and hasattr(self.node_manager, 'communication'):
                    messages = self.node_manager.communication.worker_messages.get(worker_id, [])
                    
                    return jsonify({
                        'success': True,
                        'worker_id': worker_id,
                        'messages': messages
                    }), 200
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Communication system not available'
                    }), 503
                    
            except Exception as e:
                self.logger.error(f"Worker messages error: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/api/workers/<worker_id>/results', methods=['POST'])
        def submit_worker_results(worker_id):
            """Submit results from a worker"""
            try:
                data = request.get_json()
                
                task_id = data.get('task_id')
                result = data.get('result', {})
                
                if not task_id:
                    return jsonify({'error': 'task_id required'}), 400
                
                if self.node_manager and self.node_manager.task_manager:
                    self.node_manager.task_manager.handle_task_completion(
                        task_id, result, worker_id
                    )
                    
                    return jsonify({
                        'success': True,
                        'task_id': task_id,
                        'worker_id': worker_id
                    }), 200
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Task manager not available'
                    }), 503
                    
            except Exception as e:
                self.logger.error(f"Worker results error: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/api/status', methods=['GET'])
        def get_status():
            """Get overall system status"""
            try:
                if self.node_manager:
                    status = self.node_manager.get_status()
                    return jsonify({
                        'success': True,
                        'status': status,
                        'timestamp': time.time()
                    }), 200
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Node manager not available'
                    }), 503
                    
            except Exception as e:
                self.logger.error(f"Status error: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/api/health', methods=['GET'])
        def health_check():
            """Health check endpoint"""
            return jsonify({
                'success': True,
                'status': 'healthy',
                'timestamp': time.time()
            }), 200
        
        @self.app.route('/api/workers', methods=['GET'])
        def list_workers():
            """List all workers"""
            try:
                if self.node_manager and self.node_manager.worker_manager:
                    stats = self.node_manager.worker_manager.get_worker_statistics()
                    
                    return jsonify({
                        'success': True,
                        'workers': stats,
                        'timestamp': time.time()
                    }), 200
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Worker manager not available'
                    }), 503
                    
            except Exception as e:
                self.logger.error(f"List workers error: {e}")
                return jsonify({'error': str(e)}), 500
        
        @self.app.route('/api/tasks', methods=['GET'])
        def list_tasks():
            """List all tasks"""
            try:
                if self.node_manager and self.node_manager.task_manager:
                    stats = self.node_manager.task_manager.get_statistics()
                    
                    return jsonify({
                        'success': True,
                        'tasks': stats,
                        'timestamp': time.time()
                    }), 200
                else:
                    return jsonify({
                        'success': False,
                        'error': 'Task manager not available'
                    }), 503
                    
            except Exception as e:
                self.logger.error(f"List tasks error: {e}")
                return jsonify({'error': str(e)}), 500
    
    def start_server(self):
        """Start the API server"""
        if self.running:
            self.logger.warning("API server already running")
            return
            
        try:
            self.running = True
            
            # Start server in a separate thread
            self.server_thread = threading.Thread(
                target=self._run_server,
                daemon=True
            )
            self.server_thread.start()
            
            self.logger.info(f"API server started on {self.host}:{self.port}")
            
        except Exception as e:
            self.logger.error(f"Failed to start API server: {e}")
            self.running = False
    
    def _run_server(self):
        """Run the Flask server"""
        try:
            self.app.run(
                host=self.host,
                port=self.port,
                debug=False,
                threaded=True,
                use_reloader=False
            )
        except Exception as e:
            self.logger.error(f"API server error: {e}")
            self.running = False
    
    def stop_server(self):
        """Stop the API server"""
        try:
            self.running = False
            
            if self.server_thread and self.server_thread.is_alive():
                # Flask doesn't have a graceful shutdown mechanism
                # In production, you'd use a WSGI server like gunicorn
                self.logger.info("Stopping API server...")
                
            self.logger.info("API server stopped")
            
        except Exception as e:
            self.logger.error(f"Error stopping API server: {e}")
    
    def is_running(self) -> bool:
        """Check if server is running"""
        return self.running and bool(self.server_thread and self.server_thread.is_alive())
    
    def get_server_info(self) -> Dict[str, Any]:
        """Get server information"""
        return {
            'host': self.host,
            'port': self.port,
            'running': self.is_running(),
            'routes': [
                '/api/tasks/submit',
                '/api/tasks/<task_id>/status',
                '/api/tasks/<task_id>/cancel',
                '/api/workers/register',
                '/api/workers/<worker_id>/status',
                '/api/workers/<worker_id>/messages',
                '/api/workers/<worker_id>/results',
                '/api/status',
                '/api/health',
                '/api/workers',
                '/api/tasks'
            ]
        }
