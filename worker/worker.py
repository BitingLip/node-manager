#!/usr/bin/env python3
"""
Worker - Main orchestrator for GPU worker processes
Coordinates communication and action processing threads
"""
import sys
import time
import threading
import queue
import argparse
from typing import Dict, Any, Optional
from pathlib import Path

# Add the worker directory to Python path
worker_dir = Path(__file__).parent
sys.path.insert(0, str(worker_dir))

# Import core components
from .core.communication import Communication
from .core.memory import Memory
from .core.processing import Processing
from .core.hardware import Hardware
from .core.config import Config
from .core.logger import Logger


class Worker:
    """Main Worker class that orchestrates all core components"""
    
    def __init__(self, device_id: int, worker_id: Optional[str] = None, enable_external_services: bool = True, shared_queues: Optional[Dict[str, Any]] = None):
        """Initialize worker with all core components
        
        Args:
            device_id: GPU device ID to use            worker_id: Custom worker ID (defaults to worker_{device_id})
            enable_external_services: Whether to enable communication services
            shared_queues: Dictionary of shared multiprocessing queues for direct communication
        """
        self.device_id = device_id
        self.worker_id = worker_id or f"worker_{device_id}"
        self.enable_external_services = enable_external_services
        self.shared_queues = shared_queues
        self.running = False
        
        # Initialize logger first so it can be used for warnings
        self.logger = Logger(self.worker_id, device_id)
          # Thread-safe queues for inter-thread communication
        if shared_queues:
            # With worker-specific queues, we use internal queues for thread communication
            # and let the communication module handle the worker-specific shared queues
            self.instruction_queue = queue.Queue()
            self.result_queue = queue.Queue()
            self.status_queue = queue.Queue()
            self.shared_queues = shared_queues
        else:
            # Use local thread queues for standalone mode
            self.instruction_queue = queue.Queue()
            self.result_queue = queue.Queue()
            self.status_queue = queue.Queue()
            self.shared_queues = None
        
        # Control flags
        self.shutdown_event = threading.Event()
        
        # Initialize core components
        self.config = Config()
        self.hardware = Hardware(device_id=device_id, logger=self.logger)
        self.memory = Memory(device_id=device_id, logger=self.logger, hardware=self.hardware)
        self.processing = Processing(
            device_id=device_id, 
            memory=self.memory, 
            hardware=self.hardware,
            logger=self.logger,
            config=self.config.config_data  # Pass config data for OpenCLIP initialization
        )
        self.communication = Communication(
            worker_id=self.worker_id,
            instruction_queue=self.instruction_queue,
            result_queue=self.result_queue,
            status_queue=self.status_queue,
            logger=self.logger,
            shared_queues=self.shared_queues
        )
        
        # Thread references
        self.communication_thread = None
        self.action_thread = None
        
        self.logger.info(f"Worker {self.worker_id} initialized for GPU {device_id}")
    
    def start(self):
        """Start all worker threads"""
        try:
            self.logger.info("Starting worker threads...")
            self.running = True
            
            # Hardware monitoring is now on-demand only (no background thread)
              # Start communication thread (for external services OR shared queues)
            if self.enable_external_services or self.shared_queues:
                self.communication_thread = threading.Thread(
                    target=self._communication_loop,
                    name=f"Communication-{self.worker_id}",
                    daemon=True
                )
                self.communication_thread.start()
                if self.shared_queues:
                    self.logger.info("Using shared queues for direct communication with node manager")
                else:
                    self.logger.info("Using external services for communication")
            
            # Start action processing thread (always enabled for local processing)
            self.action_thread = threading.Thread(
                target=self._action_loop,
                name=f"Action-{self.worker_id}",
                daemon=True
            )
            self.action_thread.start()
            
            if self.enable_external_services:
                self.logger.info("All worker threads started successfully")
            else:
                self.logger.info("Worker started in standalone mode (no external services)")
                
        except Exception as e:
            self.logger.error(f"Failed to start worker: {e}")
            self.shutdown()
            raise
    
    def shutdown(self):
        """Gracefully shutdown all worker threads"""
        self.logger.info("Initiating worker shutdown...")
        self.running = False
        self.shutdown_event.set()
        
        # Hardware monitoring is now on-demand only (no background thread to stop)
        # Wait for threads to finish (only join threads that were started)
        threads = []
        if self.communication_thread:
            threads.append(self.communication_thread)
        if self.action_thread:
            threads.append(self.action_thread)
            
        for thread in threads:
            if thread and thread.is_alive():
                thread.join(timeout=5.0)
        # Cleanup components
        self.memory.force_vram_cleanup()
        self.logger.info("Worker shutdown complete")

    def _communication_loop(self):
        """Communication thread main loop"""
        self.logger.info("Communication thread started")
        
        # Register with node manager on startup
        if not self.communication.register_with_node_manager():
            self.logger.error("Failed to register with node manager - will retry during loop")
        
        try:
            # Retry registration periodically if not connected
            registration_retry_interval = 30  # seconds
            last_registration_attempt = 0
            
            while self.running and not self.shutdown_event.is_set():
                # Try to re-register if not connected and enough time has passed
                current_time = time.time()
                if (not self.communication.connected and 
                    current_time - last_registration_attempt > registration_retry_interval):
                    self.logger.info("Attempting to re-register with node manager...")
                    self.communication.register_with_node_manager()
                    last_registration_attempt = current_time
                
                # Handle incoming instructions from node-manager
                self.communication.handle_incoming_messages()
                
                # Process any results to send
                try:
                    result = self.result_queue.get_nowait()
                    self.communication.send_result(result)
                except queue.Empty:                    pass
                
                # Process status updates to send
                try:
                    status = self.status_queue.get_nowait()
                    self.communication.send_status(status)
                except queue.Empty:
                    pass
                
                # Small delay to prevent busy waiting
                time.sleep(0.1)
                
        except Exception as e:
            self.logger.error(f"Communication thread error: {e}")
        finally:
            self.logger.info("Communication thread stopped")

    def _action_loop(self):
        """Action processing thread main loop"""
        self.logger.info("Action thread started")        
        try:
            while self.running and not self.shutdown_event.is_set():
                try:
                    # Get instruction with timeout
                    instruction = self.instruction_queue.get(timeout=1.0)
                    
                    # Validate instruction
                    if instruction is None:
                        self.logger.warning("Received None instruction - skipping")
                        continue
                    
                    # Debug: Log what we received
                    self.logger.info(f"Received instruction from queue: {instruction}")
                    self.logger.debug(f"Instruction type: {type(instruction)}")
                    
                    # If using shared queues, check if message is for this worker
                    if self.shared_queues and isinstance(instruction, dict):
                        target_worker = instruction.get('worker_id')
                        if target_worker and target_worker != self.worker_id:
                            # Put message back for correct worker
                            self.logger.info(f"Message for {target_worker}, putting back (I am {self.worker_id})")
                            self.instruction_queue.put(instruction)
                            continue
                        elif target_worker == self.worker_id:
                            self.logger.info(f"Processing message for {self.worker_id}")                            # Extract and restructure the instruction data
                            instruction_data = instruction.get('data', {})
                            if instruction_data and instruction_data.get('action'):
                                task_config = instruction_data.get('task_config', {})
                                # Get task_id from instruction_data, NOT from task_config
                                task_id = instruction_data.get('task_id')
                                self.logger.info(f"DEBUG: instruction_data keys: {list(instruction_data.keys())}")
                                self.logger.info(f"DEBUG: task_config keys: {list(task_config.keys())}")
                                self.logger.info(f"DEBUG: extracted task_id: {task_id}")
                                instruction = {
                                    'action': instruction_data.get('action'),
                                    'id': task_id,  # Get task_id from instruction_data
                                    'params': task_config,
                                    'received_at': time.time()
                                }
                                self.logger.info(f"Restructured instruction: {instruction}")
                            else:
                                self.logger.warning(f"Invalid instruction data: {instruction_data}")
                                continue
                    
                    # Process the instruction
                    result = self._process_instruction(instruction)
                    
                    # Queue result for communication thread
                    self.result_queue.put(result)
                    
                except queue.Empty:
                    # No instruction received, send heartbeat during idle time
                    if self.shared_queues:
                        self.communication.send_heartbeat()
                    continue
                except Exception as e:
                    self.logger.error(f"Action processing error: {e}")
                    # Send error result
                    error_result = {
                        'success': False,
                        'error': str(e),
                        'timestamp': time.time()
                    }
                    self.result_queue.put(error_result)
                
        except Exception as e:
            self.logger.error(f"Action thread error: {e}")
        finally:
            self.logger.info("Action thread stopped")
    def _process_instruction(self, instruction: Dict[str, Any]) -> Dict[str, Any]:
        """Process an instruction and return result"""
        action = instruction.get('action')
        params = instruction.get('params', {})
        instruction_id = instruction.get('id')
        
        self.logger.info(f"Processing instruction: {action}")
        
        try:
            # Add shutdown handling
            if action == 'shutdown':
                self.logger.info("Shutdown instruction received")
                self.shutdown()
                return {
                    'success': True,
                    'message': 'Worker shutdown initiated',
                    'instruction_id': instruction_id
                }
            
            # Memory-related actions
            elif action == 'load_model_to_ram':
                return self.memory.load_model_to_ram(params)
            elif action == 'clear_ram':
                return self.memory.clear_ram()
            elif action == 'load_model_from_ram_to_vram':
                return self.memory.load_model_from_ram_to_vram(params)
            elif action == 'clear_vram':
                return self.memory.clear_vram()
            elif action == 'clean_vram':
                return self.memory.clean_vram_residuals()
            
            # Inference-related actions
            elif action == 'run_inference':
                return self.processing.run_inference(params)
            elif action == 'start_inference':
                return self.processing.start_inference(params)
            elif action == 'stop_inference':
                return self.processing.stop_inference()
            elif action == 'get_inference_status':
                return self.processing.get_inference_status()
            # Communication-related actions
            elif action == 'send_status':
                status = self._get_current_status()
                self.status_queue.put(status)
                return {'success': True, 'status': status}
            elif action == 'send_result':
                # This is handled automatically, but we can force it
                return {'success': True, 'message': 'Result queued for sending'}
            
            # Combined task action (run inference with model loading if needed)
            elif action == 'run_task':
                # Pass the task config and task_id from the restructured instruction
                task_data = instruction.get('task_config', {}).copy()  # Get task config from task_config field
                task_data['task_id'] = instruction.get('id')      # Get task_id from id field
                self.logger.info(f"Extracted task_data: {task_data}")
                return self._run_complete_task(task_data)
            
            else:
                return {
                    'success': False,
                    'error': f'Unknown action: {action}',
                    'instruction_id': instruction_id
                }
                
        except Exception as e:
            self.logger.error(f"Error processing instruction {action}: {e}")
            return {
                'success': False,
                'error': str(e),
                'action': action,
                'instruction_id': instruction_id
            }
    
    def _run_complete_task(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Run a complete inference task with proper status reporting"""
        self.logger.info(f"_run_complete_task called with params: {params}")
        
        # Ensure task_id consistency
        task_id = params.get('task_id')
        if not task_id:
            task_id = f'task_{int(time.time())}'
            params['task_id'] = task_id
            
        try:
            # Send 'accepted' status to node manager
            self._send_status_update('accepted', task_id)
            
            # Extract model information
            model_info = params.get('model')
            model_name = params.get('model_name')
            
            self.logger.info(f"Model info: {model_info}")
            self.logger.info(f"Model name: {model_name}")
            
            # If no model info but we have model_name, create model info
            if not model_info and model_name:
                model_info = {'name': model_name}
                self.logger.info(f"Created model info from model_name: {model_info}")
            
            # Convert database field names to memory manager expected format
            if model_info and 'model_name' in model_info:
                converted_model_info = {
                    'name': model_info.get('model_name'),
                    'path': model_info.get('model_path'),
                    'size_mb': model_info.get('size_mb'),
                    'last_used': model_info.get('last_used'),
                    'usage_count': model_info.get('usage_count')
                }
                model_info = converted_model_info
                self.logger.info(f"Converted model info to memory manager format: {model_info}")
            
            inference_config = params.get('config', params)
            self.logger.info(f"Running complete task: {task_id}")
            
            # Send 'processing_started' status to node manager
            self._send_status_update('processing_started', task_id)
            
            # Check if model needs to be loaded
            current_model = self.memory.get_current_model()
            required_model = model_info.get('name') if model_info else None
            
            self.logger.info(f"Current model: {current_model}")
            self.logger.info(f"Required model: {required_model}")
            
            if required_model and current_model != required_model:
                self.logger.info(f"Loading required model: {required_model}")
                
                # Clear VRAM if different model is loaded
                if current_model:
                    self.logger.info(f"Clearing current model: {current_model}")
                    self.memory.clear_vram()
                
                # Load new model
                self.logger.info("Starting model load to RAM...")
                if model_info:
                    load_result = self.memory.load_model_to_ram(model_info)
                    self.logger.info(f"RAM load result: {load_result}")
                    if not load_result.get('success'):
                        error_msg = f"Failed to load model to RAM: {load_result}"
                        self.logger.error(error_msg)
                        self._send_status_update('error', task_id, error_msg)
                        return load_result
                else:
                    error_msg = "No model_info provided"
                    self.logger.error(error_msg)
                    self._send_status_update('error', task_id, error_msg)
                    return {'success': False, 'error': error_msg}
                
                self.logger.info("Starting model load to VRAM...")
                vram_result = self.memory.load_model_from_ram_to_vram({})
                self.logger.info(f"VRAM load result: {vram_result}")
                if not vram_result.get('success'):
                    error_msg = f"Failed to load model to VRAM: {vram_result}"
                    self.logger.error(error_msg)
                    self._send_status_update('error', task_id, error_msg)
                    return vram_result
            
            # Run inference
            inference_params = {
                'task_id': task_id,
                'config': inference_config
            }
            start_time = time.time()
            result = self.processing.run_inference(inference_params)
            processing_time = time.time() - start_time
            
            # Add processing time to result
            result['processing_time'] = processing_time
            # Clean VRAM residuals after inference (but keep model loaded)
            self.memory.clean_vram_residuals()
            
            # Send completed status first, then ready status
            self._send_status_update('completed', task_id)
            self._send_status_update('ready', task_id)
            
            return result
            
        except Exception as e:
            error_msg = f"Complete task failed: {e}"
            self.logger.error(error_msg)
            self._send_status_update('error', task_id, error_msg)
            return {
                'success': False,
                'error': str(e),
                'task_id': task_id
            }

    def _send_status_update(self, status: str, task_id: Optional[str] = None, error: Optional[str] = None):
        """Send status update to node manager"""
        status_data = {
            'status': status,
            'timestamp': time.time()
        }
        
        if task_id:
            status_data['task_id'] = task_id
        if error:
            status_data['error'] = error
            
        self.communication.send_status(status_data)
        self.logger.info(f"Sent status update: {status} for task {task_id}")
    
    def _get_current_status(self) -> Dict[str, Any]:
        """Get current worker status"""
        return {
            'worker_id': self.worker_id,
            'device_id': self.device_id,
            'current_model': self.memory.get_current_model(),
            'memory_status': self.memory.get_memory_status(),
            'hardware_metrics': self.hardware.get_current_metrics(),
            'processing_status': self.processing.get_status(),
            'timestamp': time.time()
        }
    
    def run(self):
        """Main run method for the worker"""
        try:
            self.start()
            
            # Keep main thread alive
            while self.running:
                time.sleep(1.0)
                
        except KeyboardInterrupt:
            self.logger.info("Worker interrupted by user")
        except Exception as e:
            self.logger.error(f"Worker error: {e}")
        finally:
            self.shutdown()


def main(device_id=None, shared_queues=None):
    """Main function for standalone worker execution or process spawning"""
    if device_id is None:
        # Parse command line arguments for standalone execution
        parser = argparse.ArgumentParser(description='GPU Worker Process')
        parser.add_argument('device_id', type=int, help='GPU device ID')
        parser.add_argument('--worker-id', type=str, help='Custom worker ID')
        parser.add_argument('--test', action='store_true', help='Run in test mode (no external services)')
        
        args = parser.parse_args()
        device_id = args.device_id
        worker_id = args.worker_id
        test_mode = args.test
    else:
        # Called from WorkerManager with shared queues
        worker_id = None
        test_mode = False
    
    print(f"Starting GPU Worker for device {device_id}")
    print("=" * 60)
    
    if test_mode:
        print("TEST MODE: Running without external services")
        print("=" * 60)
        # Create worker with external services disabled
        worker = Worker(device_id=device_id, worker_id=worker_id, enable_external_services=False)
        worker.start()  # Start the worker to test initialization
        print(f"Worker {worker.worker_id} started successfully in test mode")
        print("Test mode complete - worker ready for integration")
        worker.shutdown()
    else:
        # Determine if we should use shared queues or external services
        if shared_queues:
            # Running as spawned process with shared queues
            worker = Worker(
                device_id=device_id, 
                worker_id=worker_id, 
                enable_external_services=True,
                shared_queues=shared_queues
            )
        else:
            # Running standalone with external services
            worker = Worker(device_id=device_id, worker_id=worker_id, enable_external_services=True)
        
        worker.run()


if __name__ == "__main__":
    main()
