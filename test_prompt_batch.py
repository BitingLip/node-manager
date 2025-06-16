#!/usr/bin/env python3
"""
Task Prompter - Automated Task Generation for Node Manager

This script emulates the task-manager by sending tasks to the node manager
for deployment across workers. Generates tasks with consistent subject
but varying actions, scenes, and camera angles.
"""

import requests
import json
import time
import random
import itertools
import subprocess
import os
import psutil
from typing import List, Dict, Any
from datetime import datetime

# Node Manager API Configuration
NODE_MANAGER_URL = "http://localhost:8080"
TASK_INTERVAL = 2  # seconds between tasks (reduced for testing)
TOTAL_TASKS = 10  # reduced for testing (will create a full version later)

# Task Generation Components
SUBJECT = "a fantasy figure, dragon scale skin, lizard eyes, blue skin"
CLOTHING = "flowers, reptile"
QUALITY = "score_9, photography, very detailed, bokeh, depth of field, cartoon"
MODEL = "cyberrealistic_pony_v110"
NEGATIVE_PROMPT = "(worst quality:1.2), (low quality:1.2), (normal quality:1.2), lowres, bad anatomy, bad hands, extra limb, missing limbs, sexy, female"

# Variation Components (8 x 6 x 4 = 192 combinations, we'll pick random ones)
ACTIONS = [
    "standing stretched out, looking up, running",
    "looking up, amazed, surprised", 
]

SCENES = [
    "in a forest with very big trees, jungle, pond, sunlight coming through the leaves",
    "in a jungle waterfall, beautiful colors",
]

CAMERA_ANGLES = [
    "High angle",
    "view from side", 
    "close-up",
    "wide-angle shot"
]

class TaskPrompter:
    def __init__(self):
        self.session = requests.Session()
        self.task_count = 0
        self.successful_tasks = 0
        self.failed_tasks = 0
        
    def check_node_manager_status(self) -> bool:
        """Check if the node manager is running and accessible"""
        try:
            response = self.session.get(f"{NODE_MANAGER_URL}/api/status", timeout=5)
            if response.status_code == 200:
                status = response.json()
                print(f"✅ Node Manager is running")
                print(f"   - Workers active: {status.get('active_workers', 0)}")
                print(f"   - Workers registered: {status.get('registered_workers', 0)}")
                return True
            else:
                print(f"❌ Node Manager returned status {response.status_code}")
                return False
        except requests.exceptions.RequestException as e:
            print(f"❌ Cannot connect to Node Manager: {e}")
            return False
    
    def generate_task_combinations(self) -> List[Dict[str, str]]:
        """Generate random combinations for the requested number of tasks"""
        combinations = []
        for i in range(TOTAL_TASKS):
            combinations.append({
                "action": random.choice(ACTIONS),
                "scene": random.choice(SCENES),
                "camera": random.choice(CAMERA_ANGLES)
            })
        return combinations
    
    def create_prompt(self, combination: Dict[str, str]) -> str:
        """Create a complete prompt from the combination"""
        return f"{QUALITY}, {SUBJECT} in {CLOTHING}, {combination['action']}, {combination['scene']}, {combination['camera']}"
    
    def create_task_payload(self, task_id: int, combination: Dict[str, str]) -> Dict[str, Any]:
        """Create the complete task payload for the node manager"""
        prompt = self.create_prompt(combination)
        
        # Create unique task ID with timestamp to prevent duplicates
        unique_task_id = f"task_{task_id:03d}_{int(time.time())}_{random.randint(100, 999)}"
        
        return {
            "prompt": prompt,
            "negative_prompt": NEGATIVE_PROMPT,
            "model": MODEL,
            "task_id": unique_task_id,
            "type": "text_to_image",
            "priority": random.choice(["normal", "high"]) if random.random() < 0.2 else "normal",
            "width": 640,
            "height": 968,  # Portrait orientation for full body shots
            "steps": 15,
            "cfg_scale": 7.0,
            "seed": random.randint(1, 2147483647),
            "sampler": "DPM++ 2M Karras",
            "metadata": {
                "action": combination["action"],
                "scene": combination["scene"], 
                "camera_angle": combination["camera"],
                "subject": SUBJECT,
                "clothing": CLOTHING,
                "generated_at": datetime.now().isoformat()
            }
        }
    
    def submit_task(self, task_payload: Dict[str, Any]) -> bool:
        """Submit a task to the node manager"""
        try:
            response = self.session.post(
                f"{NODE_MANAGER_URL}/api/tasks/submit",
                json=task_payload,
                timeout=10
            )
            
            if response.status_code == 200:
                result = response.json()
                print(f"✅ Task {task_payload['task_id']} submitted successfully")
                print(f"   📝 Prompt: {task_payload['prompt'][:100]}...")
                print(f"   🎯 Action: {task_payload['metadata']['action']}")
                print(f"   🏞️  Scene: {task_payload['metadata']['scene']}")
                print(f"   📸 Camera: {task_payload['metadata']['camera_angle']}")
                print(f"   🆔 API Task ID: {result.get('task_id', 'N/A')}")
                self.successful_tasks += 1
                return True
            else:
                print(f"❌ Failed to submit task {task_payload['task_id']}: {response.status_code}")
                try:
                    error_details = response.json()
                    print(f"   Error: {error_details.get('error', 'Unknown error')}")
                except:
                    print(f"   Response: {response.text}")
                self.failed_tasks += 1
                return False
                
        except requests.exceptions.RequestException as e:
            print(f"❌ Network error submitting task {task_payload['task_id']}: {e}")
            self.failed_tasks += 1
            return False
    
    def print_progress(self):
        """Print current progress"""
        total_submitted = self.successful_tasks + self.failed_tasks
        success_rate = (self.successful_tasks / total_submitted * 100) if total_submitted > 0 else 0
        print(f"\n📊 Progress: {total_submitted}/{TOTAL_TASKS} tasks submitted")
        print(f"✅ Success: {self.successful_tasks} ({success_rate:.1f}%)")
        print(f"❌ Failed: {self.failed_tasks}")
        print("-" * 60)
    
    def run(self):
        """Main execution loop"""
        print("🎬 Task Prompter - Automated Task Generation")
        print("=" * 60)
        print(f"Target: {TOTAL_TASKS} tasks every {TASK_INTERVAL} seconds")
        print(f"Subject: {SUBJECT} in {CLOTHING}")
        print(f"Model: {MODEL}")
        print(f"Node Manager: {NODE_MANAGER_URL}")
        print("=" * 60)
        
        # Check node manager status
        if not self.check_node_manager_status():
            print("❌ Cannot proceed - Node Manager is not accessible")
            return
        
        # Generate task combinations
        print(f"\n🎯 Generating {TOTAL_TASKS} task combinations...")
        combinations = self.generate_task_combinations()
        print(f"✅ Generated {len(combinations)} unique combinations")
        
        # Display sample combinations
        print("\n📋 Sample combinations:")
        for i, combo in enumerate(combinations[:3]):
            print(f"  {i+1}. {combo['action']} | {combo['scene']} | {combo['camera']}")
        if len(combinations) > 3:
            print("  ...")
        
        print(f"\n🚀 Starting task submission in 3 seconds...")
        time.sleep(3)
        
        # Submit tasks
        start_time = time.time()
        for i, combination in enumerate(combinations):
            self.task_count = i + 1
            
            print(f"\n🔄 Submitting task {self.task_count}/{TOTAL_TASKS}")
            
            # Create and submit task
            task_payload = self.create_task_payload(self.task_count, combination)
            self.submit_task(task_payload)
            
            # Print progress every 5 tasks
            if self.task_count % 5 == 0:
                self.print_progress()
            
            # Wait before next task (except for the last one)
            if self.task_count < TOTAL_TASKS:
                print(f"⏳ Waiting {TASK_INTERVAL} seconds before next task...")
                time.sleep(TASK_INTERVAL)
        
        # Final summary
        end_time = time.time()
        total_time = end_time - start_time
        
        print("\n" + "=" * 60)
        print("🎉 Task Prompter Completed!")
        print("=" * 60)
        print(f"📊 Final Statistics:")
        print(f"   Total tasks: {TOTAL_TASKS}")
        print(f"   Successful: {self.successful_tasks}")
        print(f"   Failed: {self.failed_tasks}")
        print(f"   Success rate: {(self.successful_tasks / TOTAL_TASKS * 100):.1f}%")
        print(f"   Total time: {total_time:.1f} seconds")
        print(f"   Average time per task: {(total_time / TOTAL_TASKS):.1f} seconds")
        
        if self.failed_tasks > 0:
            print(f"\n⚠️  {self.failed_tasks} tasks failed - check node manager logs")
        else:
            print(f"\n✅ All tasks submitted successfully!")

def main():
    """Main entry point"""
    try:
        prompter = TaskPrompter()
        prompter.run()
    except KeyboardInterrupt:
        print("\n\n🛑 Task submission interrupted by user")
        print("Press Ctrl+C again to exit completely")
    except Exception as e:
        print(f"\n❌ Unexpected error: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()
