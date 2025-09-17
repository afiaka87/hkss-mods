# Data Export Bus Usage Guide

A comprehensive guide for integrating and using the Data Export Bus with Hollow Knight: Silksong.
All examples are proof-of-concept and not functional but hopefully cover the full span of possible usecases for `data-export-bus`

For installation instructions, see the [main README](../README.md#installation) in the parent directory.

For complete API documentation including all endpoints, message formats, and protocol specifications, see the [API Reference](API.md).

## Table of Contents

- [Common Use Cases](#common-use-cases)
- [Quick Start Examples](#quick-start-examples)
- [Use Cases for Speedrunners](#use-cases-for-speedrunners)
- [Use Cases for OBS/Streaming](#use-cases-for-obsstreaming)
- [Python Integration](#python-integration)
- [JavaScript/Node.js Integration](#javascriptnodejs-integration)
- [Advanced Use Cases](#advanced-use-cases)
- [Performance Optimization](#performance-optimization)
- [Troubleshooting](#troubleshooting)

---

## Common Use Cases

### 1. Real-time Health/MP Display

**Problem**: Display current health and MP on stream overlay.

**Solution**:
```javascript
// OBS Browser Source
const updateInterval = 100; // 10Hz updates

async function updateStats() {
  const response = await fetch('http://localhost:8080/api/metrics');
  const metrics = await response.json();

  const playerData = metrics.find(m => m.EventType === 'player_update');
  if (playerData) {
    document.getElementById('health').textContent =
      `${playerData.Data.health_current}/${playerData.Data.health_max}`;
    document.getElementById('mp').textContent =
      `${playerData.Data.mp_current}/${playerData.Data.mp_max}`;
  }
}

setInterval(updateStats, updateInterval);
```

### 2. Death Counter

**Problem**: Track total deaths during a play session.

**Solution**:
```python
import asyncio
import websockets
import json

deaths = 0

async def track_deaths():
    global deaths
    async with websockets.connect('ws://localhost:9091') as ws:
        async for message in ws:
            data = json.loads(message)
            if data['EventType'] == 'player_death':
                deaths += 1
                print(f"Death #{deaths} at {data['Data']['location']}")
                # Update overlay file
                with open('death_count.txt', 'w') as f:
                    f.write(str(deaths))

asyncio.run(track_deaths())
```

### 3. Boss Fight Timer

**Problem**: Automatically time boss encounters.

**Solution**:
```javascript
let bossStartTime = null;
const ws = new WebSocket('ws://localhost:9091');

ws.onmessage = (event) => {
  const metric = JSON.parse(event.data);

  if (metric.EventType === 'boss_encounter') {
    if (metric.Data.phase === 'start') {
      bossStartTime = Date.now();
      console.log(`Boss fight started: ${metric.Data.boss_name}`);
    } else if (metric.Data.phase === 'defeated' && bossStartTime) {
      const duration = (Date.now() - bossStartTime) / 1000;
      console.log(`Boss defeated in ${duration.toFixed(2)} seconds`);
      bossStartTime = null;
    }
  }
};
```

---

## Quick Start Examples

### Basic HTTP Polling

```bash
# Get current game state every second
while true; do
  curl -s http://localhost:8080/api/metrics | jq '.[] | select(.EventType == "player_update")'
  sleep 1
done
```

### WebSocket Streaming with Processing

```python
import websocket
import json
from datetime import datetime

def on_message(ws, message):
    data = json.loads(message)
    timestamp = datetime.now().strftime('%H:%M:%S')

    # Process different event types
    if data['EventType'] == 'combat_damage':
        print(f"[{timestamp}] Damage: {data['Data']['damage']} from {data['Data']['source']}")
    elif data['EventType'] == 'scene_transition':
        print(f"[{timestamp}] Moved to: {data['Data']['to_scene']}")

ws = websocket.WebSocketApp("ws://localhost:9091", on_message=on_message)
ws.run_forever()
```

### File Export Analysis

```python
import json
from pathlib import Path

# Read NDJSON export files
export_dir = Path("DataExport")
for file in export_dir.glob("*.ndjson"):
    with open(file) as f:
        for line in f:
            event = json.loads(line)
            # Analyze events
            if event['EventType'] == 'combat_stats':
                print(f"Total damage: {event['Data']['total_damage_dealt']}")
```

---

## Use Cases for Speedrunners

### LiveSplit Auto-Splitter

**Setup**: Configure LiveSplit Server to listen on port 16834, then bridge the Data Export Bus:

```python
import socket
import asyncio

async def bridge_to_livesplit():
    # Connect to Data Export Bus TCP
    reader, writer = await asyncio.open_connection('localhost', 9090)

    # Connect to LiveSplit Server
    livesplit = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    livesplit.connect(('localhost', 16834))

    while True:
        data = await reader.readline()
        command = data.decode().strip()

        if command == 'split':
            livesplit.send(b'split\r\n')
        elif command == 'reset':
            livesplit.send(b'reset\r\n')
        elif command.startswith('time:'):
            # Update game time
            time_val = command.split(':')[1]
            livesplit.send(f'setgametime {time_val}\r\n'.encode())

asyncio.run(bridge_to_livesplit())
```

### Segment Time Analysis

**Problem**: Analyze time spent in each room/segment.

```python
import requests
from collections import defaultdict
from datetime import datetime

segment_times = defaultdict(float)
scene_start = {}

def analyze_segments():
    while True:
        metrics = requests.get('http://localhost:8080/api/recent?count=100').json()

        for metric in metrics:
            if metric['EventType'] == 'scene_transition':
                scene = metric['Data']['from_scene']
                if scene in scene_start:
                    duration = datetime.now().timestamp() - scene_start[scene]
                    segment_times[scene] += duration
                    print(f"{scene}: {duration:.2f}s (Total: {segment_times[scene]:.2f}s)")

                scene_start[metric['Data']['to_scene']] = datetime.now().timestamp()

analyze_segments()
```

### Practice Mode Stats

**Problem**: Track improvement in specific sections during practice.

```python
class PracticeTracker:
    def __init__(self, target_scene):
        self.target_scene = target_scene
        self.attempts = []
        self.current_attempt = None

    async def track(self):
        async with websockets.connect('ws://localhost:9091') as ws:
            async for message in ws:
                data = json.loads(message)

                if data['EventType'] == 'scene_transition':
                    if data['Data']['to_scene'] == self.target_scene:
                        # Started practice section
                        self.current_attempt = {
                            'start': time.time(),
                            'damage_taken': 0,
                            'deaths': 0
                        }
                    elif self.current_attempt and data['Data']['from_scene'] == self.target_scene:
                        # Completed practice section
                        self.current_attempt['duration'] = time.time() - self.current_attempt['start']
                        self.attempts.append(self.current_attempt)
                        self.print_stats()

                elif data['EventType'] == 'combat_damage' and self.current_attempt:
                    if data['Data']['is_player_damage']:
                        self.current_attempt['damage_taken'] += data['Data']['damage']

    def print_stats(self):
        avg_time = sum(a['duration'] for a in self.attempts) / len(self.attempts)
        best_time = min(a['duration'] for a in self.attempts)
        print(f"Attempts: {len(self.attempts)}, Avg: {avg_time:.2f}s, Best: {best_time:.2f}s")
```

---

## Use Cases for OBS/Streaming

### Dynamic Scene Switching

**Problem**: Automatically switch OBS scenes based on game state.

```javascript
// OBS WebSocket Integration
const OBSWebSocket = require('obs-websocket-js');
const WebSocket = require('ws');

const obs = new OBSWebSocket();
const gameWs = new WebSocket('ws://localhost:9091');

// Scene mappings
const sceneMap = {
  'MainMenu': 'Menu Scene',
  'Boss_': 'Boss Fight Scene',  // Prefix match
  'Cutscene_': 'Cutscene Scene',
  '_Shop': 'Shop Scene'  // Suffix match
};

gameWs.on('message', async (data) => {
  const metric = JSON.parse(data);

  if (metric.EventType === 'scene_transition') {
    const scene = metric.Data.to_scene;

    // Find matching OBS scene
    for (const [pattern, obsScene] of Object.entries(sceneMap)) {
      if (scene.includes(pattern)) {
        await obs.call('SetCurrentProgramScene', { sceneName: obsScene });
        console.log(`Switched to ${obsScene}`);
        break;
      }
    }
  }
});

await obs.connect('ws://localhost:4455', 'your-password');
```

### Damage Visualization Overlay

**Problem**: Show damage numbers and combat stats as overlay.

```html
<!DOCTYPE html>
<html>
<head>
<style>
  body {
    margin: 0;
    font-family: 'Arial Black';
    color: white;
    text-shadow: 2px 2px 4px black;
  }
  .damage-number {
    position: absolute;
    animation: float-up 2s ease-out forwards;
    font-size: 48px;
    font-weight: bold;
  }
  @keyframes float-up {
    0% { transform: translateY(0); opacity: 1; }
    100% { transform: translateY(-100px); opacity: 0; }
  }
  .player-damage { color: #ff4444; }
  .enemy-damage { color: #ffff44; }
</style>
</head>
<body>
<script>
const ws = new WebSocket('ws://localhost:9091');
const container = document.body;

ws.onmessage = (event) => {
  const metric = JSON.parse(event.data);

  if (metric.EventType === 'combat_damage') {
    const damage = document.createElement('div');
    damage.className = 'damage-number ' +
      (metric.Data.is_player_damage ? 'player-damage' : 'enemy-damage');
    damage.textContent = metric.Data.damage;
    damage.style.left = Math.random() * window.innerWidth + 'px';
    damage.style.top = Math.random() * window.innerHeight + 'px';

    container.appendChild(damage);
    setTimeout(() => damage.remove(), 2000);
  }
};
</script>
</body>
</html>
```

### Stream Alerts

**Problem**: Trigger alerts for specific achievements or events.

```python
import asyncio
import websockets
import json
from playsound import playsound

# Alert configuration
alerts = {
    'boss_defeated': {
        'sound': 'victory.mp3',
        'message': 'BOSS DEFEATED!',
        'duration': 5
    },
    'no_hit_room': {
        'sound': 'perfect.mp3',
        'message': 'PERFECT ROOM!',
        'duration': 3
    },
    'speed_achievement': {
        'sound': 'speed.mp3',
        'message': 'SPEED DEMON!',
        'duration': 4
    }
}

room_damage = 0
room_start_time = None

async def stream_alerts():
    global room_damage, room_start_time

    async with websockets.connect('ws://localhost:9091') as ws:
        async for message in ws:
            data = json.loads(message)

            # Track room performance
            if data['EventType'] == 'scene_transition':
                if room_damage == 0 and room_start_time:
                    trigger_alert('no_hit_room')
                room_damage = 0
                room_start_time = time.time()

            elif data['EventType'] == 'combat_damage':
                if data['Data']['is_player_damage']:
                    room_damage += data['Data']['damage']

            elif data['EventType'] == 'boss_defeated':
                trigger_alert('boss_defeated')

            elif data['EventType'] == 'scene_transition':
                # Check for speed achievements
                if room_start_time and (time.time() - room_start_time) < 30:
                    trigger_alert('speed_achievement')

def trigger_alert(alert_type):
    alert = alerts[alert_type]
    playsound(alert['sound'])
    # Write to file for OBS text source
    with open('alert.txt', 'w') as f:
        f.write(alert['message'])
    # Clear after duration
    asyncio.create_task(clear_alert(alert['duration']))

async def clear_alert(duration):
    await asyncio.sleep(duration)
    open('alert.txt', 'w').close()
```

---

## Python Integration

### Complete Python Client

```python
"""
Data Export Bus Python Client
Full-featured client for interacting with all protocols
"""

import asyncio
import aiohttp
import websockets
import json
from typing import Dict, List, Optional, Callable
from datetime import datetime
import logging

class DataExportBusClient:
    def __init__(self,
                 http_url: str = "http://localhost:8080",
                 ws_url: str = "ws://localhost:9091",
                 tcp_host: str = "localhost",
                 tcp_port: int = 9090):
        self.http_url = http_url
        self.ws_url = ws_url
        self.tcp_host = tcp_host
        self.tcp_port = tcp_port
        self.handlers: Dict[str, List[Callable]] = {}
        self.logger = logging.getLogger(__name__)

    # HTTP Methods
    async def get_status(self) -> Dict:
        """Get current service status"""
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.http_url}/api/status") as resp:
                return await resp.json()

    async def get_metrics(self, event_type: Optional[str] = None,
                          limit: int = 100) -> List[Dict]:
        """Get recent metrics with optional filtering"""
        params = {"limit": limit}
        if event_type:
            params["type"] = event_type

        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.http_url}/api/metrics",
                                  params=params) as resp:
                return await resp.json()

    async def configure(self, **settings) -> Dict:
        """Update configuration dynamically"""
        async with aiohttp.ClientSession() as session:
            async with session.post(f"{self.http_url}/api/configure",
                                   json=settings) as resp:
                return await resp.json()

    # WebSocket Streaming
    def on_event(self, event_type: str):
        """Decorator for event handlers"""
        def decorator(func):
            if event_type not in self.handlers:
                self.handlers[event_type] = []
            self.handlers[event_type].append(func)
            return func
        return decorator

    async def stream(self):
        """Start WebSocket streaming with event handlers"""
        async with websockets.connect(self.ws_url) as ws:
            self.logger.info("Connected to WebSocket stream")

            async for message in ws:
                try:
                    data = json.loads(message)
                    event_type = data.get('EventType')

                    # Call registered handlers
                    if event_type in self.handlers:
                        for handler in self.handlers[event_type]:
                            await handler(data) if asyncio.iscoroutinefunction(handler) else handler(data)

                    # Call wildcard handlers
                    if '*' in self.handlers:
                        for handler in self.handlers['*']:
                            await handler(data) if asyncio.iscoroutinefunction(handler) else handler(data)

                except json.JSONDecodeError:
                    self.logger.error(f"Invalid JSON: {message}")
                except Exception as e:
                    self.logger.error(f"Handler error: {e}")

    # TCP LiveSplit Integration
    async def livesplit_bridge(self, callback: Optional[Callable] = None):
        """Bridge TCP commands to LiveSplit or custom handler"""
        reader, writer = await asyncio.open_connection(self.tcp_host, self.tcp_port)
        self.logger.info("Connected to TCP server")

        try:
            while True:
                data = await reader.readline()
                if not data:
                    break

                command = data.decode().strip()
                self.logger.debug(f"TCP command: {command}")

                if callback:
                    await callback(command) if asyncio.iscoroutinefunction(callback) else callback(command)

        finally:
            writer.close()
            await writer.wait_closed()

# Example usage
async def main():
    client = DataExportBusClient()

    # Register event handlers
    @client.on_event('player_update')
    def handle_player_update(data):
        print(f"Player at ({data['Data']['position_x']:.1f}, {data['Data']['position_y']:.1f})")

    @client.on_event('combat_damage')
    async def handle_damage(data):
        if data['Data']['is_player_damage']:
            print(f"Player took {data['Data']['damage']} damage!")
            # Could trigger external API call here

    @client.on_event('scene_transition')
    def handle_scene_change(data):
        print(f"Entered: {data['Data']['to_scene']}")

    # Start streaming
    await client.stream()

if __name__ == "__main__":
    asyncio.run(main())
```

### Data Analysis and Visualization

```python
import pandas as pd
import matplotlib.pyplot as plt
from datetime import datetime
import json

class GameSessionAnalyzer:
    def __init__(self, export_file: str):
        self.events = []
        with open(export_file) as f:
            for line in f:
                self.events.append(json.loads(line))

        self.df = pd.DataFrame(self.events)
        self.df['Timestamp'] = pd.to_datetime(self.df['Timestamp'])

    def plot_health_over_time(self):
        """Visualize health changes during session"""
        health_events = self.df[self.df['EventType'] == 'player_update'].copy()
        health_events['health'] = health_events['Data'].apply(lambda x: x.get('health_current', 0))

        plt.figure(figsize=(12, 6))
        plt.plot(health_events['Timestamp'], health_events['health'], 'b-')
        plt.fill_between(health_events['Timestamp'], 0, health_events['health'], alpha=0.3)
        plt.ylabel('Health')
        plt.xlabel('Time')
        plt.title('Health Over Time')
        plt.xticks(rotation=45)
        plt.tight_layout()
        plt.show()

    def analyze_combat_performance(self):
        """Calculate combat statistics"""
        combat = self.df[self.df['EventType'] == 'combat_damage']

        player_damage = combat[combat['Data'].apply(lambda x: x.get('is_player_damage', False))]
        enemy_damage = combat[~combat['Data'].apply(lambda x: x.get('is_player_damage', False))]

        stats = {
            'total_damage_dealt': enemy_damage['Data'].apply(lambda x: x.get('damage', 0)).sum(),
            'total_damage_taken': player_damage['Data'].apply(lambda x: x.get('damage', 0)).sum(),
            'avg_damage_dealt': enemy_damage['Data'].apply(lambda x: x.get('damage', 0)).mean(),
            'avg_damage_taken': player_damage['Data'].apply(lambda x: x.get('damage', 0)).mean(),
            'combat_encounters': len(combat),
            'damage_ratio': enemy_damage['Data'].apply(lambda x: x.get('damage', 0)).sum() /
                           (player_damage['Data'].apply(lambda x: x.get('damage', 0)).sum() or 1)
        }

        return stats

    def scene_heatmap(self):
        """Generate heatmap of time spent in each scene"""
        scenes = self.df[self.df['EventType'] == 'scene_transition']
        scene_times = {}

        for i, row in scenes.iterrows():
            scene = row['Data'].get('from_scene')
            time_spent = row['Data'].get('total_scene_time', 0)

            if scene not in scene_times:
                scene_times[scene] = []
            scene_times[scene].append(time_spent)

        # Calculate averages
        scene_avg = {scene: sum(times)/len(times)
                    for scene, times in scene_times.items()}

        # Create heatmap
        plt.figure(figsize=(10, 8))
        scenes = list(scene_avg.keys())
        times = list(scene_avg.values())

        plt.barh(scenes, times, color=plt.cm.coolwarm([t/max(times) for t in times]))
        plt.xlabel('Average Time (seconds)')
        plt.title('Scene Difficulty Heatmap')
        plt.tight_layout()
        plt.show()

        return scene_avg

# Usage
analyzer = GameSessionAnalyzer('DataExport/session_20240115.ndjson')
analyzer.plot_health_over_time()
combat_stats = analyzer.analyze_combat_performance()
print(f"Combat Performance: {json.dumps(combat_stats, indent=2)}")
scene_times = analyzer.scene_heatmap()
```

---

## JavaScript/Node.js Integration

### Node.js Game Monitor

```javascript
const WebSocket = require('ws');
const express = require('express');
const app = express();

class GameMonitor {
  constructor() {
    this.stats = {
      session: {
        startTime: Date.now(),
        deaths: 0,
        damageDealt: 0,
        damageTaken: 0,
        scenesVisited: new Set(),
        currentScene: null
      },
      current: {
        health: 0,
        maxHealth: 0,
        mp: 0,
        maxMp: 0,
        position: { x: 0, y: 0 },
        velocity: { x: 0, y: 0 }
      }
    };

    this.eventHandlers = new Map();
    this.connect();
  }

  connect() {
    this.ws = new WebSocket('ws://localhost:9091');

    this.ws.on('open', () => {
      console.log('Connected to Data Export Bus');
    });

    this.ws.on('message', (data) => {
      const metric = JSON.parse(data);
      this.handleMetric(metric);

      // Trigger registered handlers
      const handlers = this.eventHandlers.get(metric.EventType) || [];
      handlers.forEach(handler => handler(metric));
    });

    this.ws.on('close', () => {
      console.log('Disconnected - reconnecting in 5s');
      setTimeout(() => this.connect(), 5000);
    });
  }

  handleMetric(metric) {
    switch(metric.EventType) {
      case 'player_update':
        this.stats.current = {
          health: metric.Data.health_current,
          maxHealth: metric.Data.health_max,
          mp: metric.Data.mp_current,
          maxMp: metric.Data.mp_max,
          position: {
            x: metric.Data.position_x,
            y: metric.Data.position_y
          },
          velocity: {
            x: metric.Data.velocity_x,
            y: metric.Data.velocity_y
          }
        };
        break;

      case 'combat_damage':
        if (metric.Data.is_player_damage) {
          this.stats.session.damageTaken += metric.Data.damage;
        } else {
          this.stats.session.damageDealt += metric.Data.damage;
        }
        break;

      case 'player_death':
        this.stats.session.deaths++;
        break;

      case 'scene_transition':
        this.stats.current.currentScene = metric.Data.to_scene;
        this.stats.session.scenesVisited.add(metric.Data.to_scene);
        break;
    }
  }

  on(eventType, handler) {
    if (!this.eventHandlers.has(eventType)) {
      this.eventHandlers.set(eventType, []);
    }
    this.eventHandlers.get(eventType).push(handler);
  }

  getStats() {
    return {
      ...this.stats,
      session: {
        ...this.stats.session,
        duration: (Date.now() - this.stats.session.startTime) / 1000,
        scenesVisited: Array.from(this.stats.session.scenesVisited)
      }
    };
  }
}

// Create monitor and API
const monitor = new GameMonitor();

// Serve stats via HTTP
app.get('/stats', (req, res) => {
  res.json(monitor.getStats());
});

app.get('/health', (req, res) => {
  res.json({
    current: monitor.stats.current.health,
    max: monitor.stats.current.maxHealth,
    percentage: (monitor.stats.current.health / monitor.stats.current.maxHealth * 100).toFixed(1)
  });
});

// Custom event handlers
monitor.on('boss_defeated', (metric) => {
  console.log(`Boss ${metric.Data.boss_name} defeated in ${metric.Data.time}s!`);
  // Could trigger Discord webhook, etc.
});

app.listen(3000, () => {
  console.log('Game monitor API running on http://localhost:3000');
});
```

---

## Advanced Use Cases

### Multi-Stream Synchronization

**Problem**: Synchronize multiple streamers playing together.

```python
"""
Multi-stream synchronization for races or co-op
Aggregates data from multiple Data Export Bus instances
"""

import asyncio
import aiohttp
from typing import Dict, List
import json

class MultiStreamSync:
    def __init__(self, players: Dict[str, str]):
        """
        players: Dict mapping player names to their Data Export Bus URLs
        e.g., {"Player1": "http://192.168.1.100:8080", "Player2": "http://192.168.1.101:8080"}
        """
        self.players = players
        self.player_states = {name: {} for name in players}

    async def sync_loop(self):
        """Continuously sync all player states"""
        while True:
            await self.update_all_states()
            self.render_comparison()
            await asyncio.sleep(0.5)  # 2Hz update rate

    async def update_all_states(self):
        """Fetch current state from all players"""
        tasks = []
        for name, url in self.players.items():
            tasks.append(self.fetch_player_state(name, url))

        await asyncio.gather(*tasks, return_exceptions=True)

    async def fetch_player_state(self, name: str, url: str):
        """Fetch state for a single player"""
        try:
            async with aiohttp.ClientSession() as session:
                async with session.get(f"{url}/api/metrics") as resp:
                    metrics = await resp.json()

                    # Extract relevant data
                    for metric in metrics:
                        if metric['EventType'] == 'player_update':
                            self.player_states[name]['position'] = {
                                'x': metric['Data']['position_x'],
                                'y': metric['Data']['position_y']
                            }
                            self.player_states[name]['health'] = metric['Data']['health_current']

                        elif metric['EventType'] == 'timing_update':
                            self.player_states[name]['game_time'] = metric['Data']['session_time']

                        elif metric['EventType'] == 'scene_transition':
                            self.player_states[name]['current_scene'] = metric['Data']['to_scene']

        except Exception as e:
            print(f"Error fetching {name}: {e}")

    def render_comparison(self):
        """Display synchronized comparison"""
        print("\n" + "="*50)
        print("RACE STATUS")
        print("="*50)

        # Sort by game time (race position)
        sorted_players = sorted(self.player_states.items(),
                              key=lambda x: x[1].get('game_time', float('inf')))

        for i, (name, state) in enumerate(sorted_players, 1):
            if 'game_time' in state:
                time_str = f"{state['game_time']:.1f}s"
                scene = state.get('current_scene', 'Unknown')
                health = state.get('health', 0)

                # Calculate relative position
                if i > 1:
                    time_behind = state['game_time'] - sorted_players[0][1]['game_time']
                    print(f"{i}. {name}: {scene} | HP: {health} | Time: {time_str} (+{time_behind:.1f}s)")
                else:
                    print(f"{i}. {name}: {scene} | HP: {health} | Time: {time_str} (LEADER)")

# Usage for race
async def main():
    sync = MultiStreamSync({
        "Speedrunner1": "http://192.168.1.100:8080",
        "Speedrunner2": "http://192.168.1.101:8080",
        "Speedrunner3": "http://192.168.1.102:8080"
    })
    await sync.sync_loop()

asyncio.run(main())
```

### AI-Powered Play Analysis

**Problem**: Use machine learning to analyze play patterns and suggest improvements.

```python
"""
Statistical analysis of gameplay patterns using exported data
Identifies inefficiencies and suggests optimizations
"""

import numpy as np
from sklearn.cluster import KMeans
from sklearn.preprocessing import StandardScaler
import json
from typing import List, Dict
from collections import defaultdict

class GameplayAnalyzer:
    def __init__(self, session_file: str):
        self.events = self.load_session(session_file)
        self.segments = self.extract_segments()

    def load_session(self, filename: str) -> List[Dict]:
        events = []
        with open(filename) as f:
            for line in f:
                events.append(json.loads(line))
        return events

    def extract_segments(self) -> Dict:
        """Extract room segments for analysis"""
        segments = defaultdict(list)
        current_scene = None
        segment_data = None

        for event in self.events:
            if event['EventType'] == 'scene_transition':
                if segment_data:
                    segments[current_scene].append(segment_data)

                current_scene = event['Data']['to_scene']
                segment_data = {
                    'scene': current_scene,
                    'start_time': event['Timestamp'],
                    'damage_taken': 0,
                    'damage_dealt': 0,
                    'abilities_used': [],
                    'path': [],
                    'duration': 0
                }

            elif segment_data:
                if event['EventType'] == 'player_update':
                    segment_data['path'].append({
                        'x': event['Data']['position_x'],
                        'y': event['Data']['position_y'],
                        'velocity': np.sqrt(
                            event['Data']['velocity_x']**2 +
                            event['Data']['velocity_y']**2
                        )
                    })

                elif event['EventType'] == 'combat_damage':
                    if event['Data']['is_player_damage']:
                        segment_data['damage_taken'] += event['Data']['damage']
                    else:
                        segment_data['damage_dealt'] += event['Data']['damage']

                elif event['EventType'] == 'ability_used':
                    segment_data['abilities_used'].append(event['Data']['ability'])

        return dict(segments)

    def analyze_routes(self, scene: str):
        """Cluster and analyze different routes through a scene"""
        if scene not in self.segments:
            return None

        attempts = self.segments[scene]
        if len(attempts) < 3:
            return None

        # Extract features for clustering
        features = []
        for attempt in attempts:
            # Calculate path efficiency
            path = attempt['path']
            if len(path) < 2:
                continue

            total_distance = sum(
                np.sqrt((path[i+1]['x'] - path[i]['x'])**2 +
                       (path[i+1]['y'] - path[i]['y'])**2)
                for i in range(len(path)-1)
            )

            avg_velocity = np.mean([p['velocity'] for p in path])

            features.append([
                total_distance,
                avg_velocity,
                attempt['damage_taken'],
                attempt['damage_dealt'],
                len(attempt['abilities_used'])
            ])

        if len(features) < 2:
            return None

        # Normalize and cluster
        scaler = StandardScaler()
        normalized = scaler.fit_transform(features)

        # Find optimal number of clusters (max 3)
        n_clusters = min(3, len(features))
        kmeans = KMeans(n_clusters=n_clusters, random_state=42)
        clusters = kmeans.fit_predict(normalized)

        # Analyze clusters
        cluster_stats = defaultdict(list)
        for i, cluster_id in enumerate(clusters):
            cluster_stats[cluster_id].append({
                'distance': features[i][0],
                'velocity': features[i][1],
                'damage_taken': features[i][2],
                'damage_dealt': features[i][3],
                'abilities': features[i][4]
            })

        # Find best performing cluster
        best_cluster = None
        best_score = float('inf')

        for cluster_id, stats in cluster_stats.items():
            # Score based on low damage taken and high velocity
            avg_damage = np.mean([s['damage_taken'] for s in stats])
            avg_velocity = np.mean([s['velocity'] for s in stats])
            score = avg_damage / (avg_velocity + 1)  # Lower is better

            if score < best_score:
                best_score = score
                best_cluster = cluster_id

        return {
            'total_attempts': len(attempts),
            'route_variants': n_clusters,
            'best_route': {
                'cluster_id': best_cluster,
                'avg_damage': np.mean([s['damage_taken']
                                       for s in cluster_stats[best_cluster]]),
                'avg_velocity': np.mean([s['velocity']
                                        for s in cluster_stats[best_cluster]]),
                'characteristics': self.describe_route(cluster_stats[best_cluster])
            },
            'recommendations': self.generate_recommendations(cluster_stats, best_cluster)
        }

    def describe_route(self, stats: List[Dict]) -> str:
        """Generate human-readable description of route characteristics"""
        avg_damage = np.mean([s['damage_taken'] for s in stats])
        avg_velocity = np.mean([s['velocity'] for s in stats])

        descriptions = []

        if avg_damage < 1:
            descriptions.append("damage-free")
        elif avg_damage < 3:
            descriptions.append("low-damage")
        else:
            descriptions.append("high-risk")

        if avg_velocity > 8:
            descriptions.append("high-speed")
        elif avg_velocity > 5:
            descriptions.append("moderate-pace")
        else:
            descriptions.append("cautious")

        return ", ".join(descriptions)

    def generate_recommendations(self, clusters: Dict, best_cluster: int) -> List[str]:
        """Generate improvement recommendations"""
        recommendations = []

        # Compare user's most recent attempt to best cluster
        best_stats = clusters[best_cluster]
        worst_stats = clusters[0] if best_cluster != 0 else clusters[1] if len(clusters) > 1 else None

        if worst_stats:
            damage_diff = np.mean([s['damage_taken'] for s in worst_stats]) - \
                         np.mean([s['damage_taken'] for s in best_stats])
            velocity_diff = np.mean([s['velocity'] for s in best_stats]) - \
                           np.mean([s['velocity'] for s in worst_stats])

            if damage_diff > 2:
                recommendations.append(f"Optimal route takes {damage_diff:.1f} less damage on average")

            if velocity_diff > 2:
                recommendations.append(f"Optimal route is {velocity_diff:.1f} units/s faster")
                recommendations.append("Consider maintaining momentum through jumps")

            ability_usage_best = np.mean([s['abilities'] for s in best_stats])
            ability_usage_worst = np.mean([s['abilities'] for s in worst_stats])

            if ability_usage_best > ability_usage_worst + 1:
                recommendations.append("Best route uses abilities more frequently for movement")
            elif ability_usage_best < ability_usage_worst - 1:
                recommendations.append("Consider conserving abilities - optimal route uses fewer")

        return recommendations

# Usage
analyzer = GameplayAnalyzer('DataExport/session_20240115.ndjson')

# Analyze a specific challenging room
analysis = analyzer.analyze_routes('BossRoom_Hornet')
if analysis:
    print(f"Room Analysis: BossRoom_Hornet")
    print(f"Attempts: {analysis['total_attempts']}")
    print(f"Distinct strategies: {analysis['route_variants']}")
    print(f"Best route: {analysis['best_route']['characteristics']}")
    print(f"Recommendations:")
    for rec in analysis['recommendations']:
        print(f"  - {rec}")
```

### Custom Mod Integration

**Problem**: Other mods want to export their data through the Data Export Bus.

```csharp
// Example: Another mod sending custom metrics to Data Export Bus
using System;
using System.Collections.Generic;
using UnityEngine;
using BepInEx;

namespace CustomMod.DataExportIntegration
{
    public class DataExportBridge
    {
        private static DataExportBridge instance;
        private object dataExportBus;
        private System.Reflection.MethodInfo broadcastMethod;

        public static DataExportBridge Instance
        {
            get
            {
                if (instance == null)
                    instance = new DataExportBridge();
                return instance;
            }
        }

        private DataExportBridge()
        {
            // Find Data Export Bus plugin
            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                if (plugin.Metadata.GUID == "com.hkss.dataexportbus")
                {
                    dataExportBus = plugin.Instance;

                    // Get BroadcastMetric method via reflection
                    broadcastMethod = dataExportBus.GetType()
                        .GetMethod("BroadcastMetric");

                    Debug.Log("Connected to Data Export Bus");
                    break;
                }
            }
        }

        public void SendCustomMetric(string eventType, Dictionary<string, object> data)
        {
            if (dataExportBus == null || broadcastMethod == null)
                return;

            // Create GameMetric object
            var metricType = dataExportBus.GetType().Assembly
                .GetType("HKSS.DataExportBus.GameMetric");

            var metric = Activator.CreateInstance(metricType, eventType);

            // Set data dictionary
            var dataProperty = metricType.GetProperty("Data");
            dataProperty.SetValue(metric, data);

            // Broadcast
            broadcastMethod.Invoke(dataExportBus, new object[] { metric });
        }

        // Helper methods for common metrics
        public void SendCustomDamage(string source, int damage, Vector2 position)
        {
            SendCustomMetric("custom_damage", new Dictionary<string, object>
            {
                ["source"] = source,
                ["damage"] = damage,
                ["position_x"] = position.x,
                ["position_y"] = position.y,
                ["mod_name"] = "YourModName"
            });
        }

        public void SendCustomEvent(string eventName, string details)
        {
            SendCustomMetric("custom_event", new Dictionary<string, object>
            {
                ["event"] = eventName,
                ["details"] = details,
                ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["mod_name"] = "YourModName"
            });
        }
    }
}

// Usage in your mod
DataExportBridge.Instance.SendCustomEvent("special_move_activated", "Mega Dash");
DataExportBridge.Instance.SendCustomDamage("custom_spell", 999, transform.position);
```

---

## Performance Optimization

### Reducing Network Overhead

```python
"""
Optimized client with batching and compression
"""

import asyncio
import aiohttp
import gzip
import json
from typing import List, Dict
from collections import deque

class OptimizedClient:
    def __init__(self, base_url: str, batch_size: int = 10,
                 batch_timeout: float = 1.0):
        self.base_url = base_url
        self.batch_size = batch_size
        self.batch_timeout = batch_timeout
        self.pending_requests = deque()
        self.metrics_cache = {}

    async def get_metrics_batch(self, requests: List[Dict]) -> List[Dict]:
        """Batch multiple metric requests into one"""
        # Combine filters
        all_types = set()
        for req in requests:
            if 'type' in req:
                all_types.add(req['type'])

        # Single request for all types
        params = {'limit': 1000}
        if all_types:
            params['types'] = ','.join(all_types)

        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.base_url}/api/metrics",
                                  params=params) as resp:
                data = await resp.json()

        # Distribute results to original requests
        results = []
        for req in requests:
            filtered = [m for m in data
                       if not req.get('type') or m['EventType'] == req['type']]
            results.append(filtered[:req.get('limit', 100)])

        return results

    async def stream_compressed(self):
        """Stream with compression to reduce bandwidth"""
        headers = {'Accept-Encoding': 'gzip'}

        async with aiohttp.ClientSession() as session:
            # Request compressed WebSocket upgrade
            async with session.ws_connect(
                self.base_url.replace('http', 'ws') + ':9091',
                compress=15  # Enable per-message deflate
            ) as ws:
                async for msg in ws:
                    if msg.type == aiohttp.WSMsgType.TEXT:
                        yield json.loads(msg.data)
                    elif msg.type == aiohttp.WSMsgType.BINARY:
                        # Handle compressed binary data
                        decompressed = gzip.decompress(msg.data)
                        yield json.loads(decompressed)

    def cache_metrics(self, metrics: List[Dict], ttl: int = 60):
        """Cache metrics to reduce repeated requests"""
        for metric in metrics:
            key = f"{metric['EventType']}_{metric.get('Timestamp', '')}"
            self.metrics_cache[key] = {
                'data': metric,
                'expires': asyncio.get_event_loop().time() + ttl
            }

    async def get_cached_or_fetch(self, event_type: str) -> List[Dict]:
        """Try cache first, then fetch if needed"""
        # Check cache
        now = asyncio.get_event_loop().time()
        cached = []

        for key, value in list(self.metrics_cache.items()):
            if value['expires'] < now:
                del self.metrics_cache[key]
            elif value['data']['EventType'] == event_type:
                cached.append(value['data'])

        if cached:
            return cached

        # Fetch if not cached
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{self.base_url}/api/metrics",
                                 params={'type': event_type}) as resp:
                metrics = await resp.json()
                self.cache_metrics(metrics)
                return metrics
```

### High-Frequency Data Collection

```javascript
// Optimized high-frequency data collection with buffering
class HighFrequencyCollector {
  constructor(options = {}) {
    this.bufferSize = options.bufferSize || 100;
    this.flushInterval = options.flushInterval || 1000;
    this.buffer = [];
    this.ws = null;
    this.stats = {
      received: 0,
      processed: 0,
      dropped: 0
    };

    this.connect();
    this.startFlushTimer();
  }

  connect() {
    this.ws = new WebSocket('ws://localhost:9091');

    this.ws.onmessage = (event) => {
      this.stats.received++;

      // Add to buffer
      if (this.buffer.length < this.bufferSize) {
        this.buffer.push(JSON.parse(event.data));
      } else {
        this.stats.dropped++;
      }
    };
  }

  startFlushTimer() {
    setInterval(() => {
      this.processBatch();
    }, this.flushInterval);
  }

  processBatch() {
    if (this.buffer.length === 0) return;

    // Process buffer in batch
    const batch = this.buffer.splice(0, this.buffer.length);

    // Aggregate similar events
    const aggregated = this.aggregate(batch);

    // Process aggregated data
    this.processAggregated(aggregated);

    this.stats.processed += batch.length;
  }

  aggregate(events) {
    const aggregated = {};

    for (const event of events) {
      const key = event.EventType;

      if (!aggregated[key]) {
        aggregated[key] = {
          count: 0,
          first: event,
          last: event,
          samples: []
        };
      }

      aggregated[key].count++;
      aggregated[key].last = event;

      // Keep samples for analysis
      if (aggregated[key].samples.length < 10) {
        aggregated[key].samples.push(event);
      }
    }

    return aggregated;
  }

  processAggregated(aggregated) {
    // Process each event type
    for (const [eventType, data] of Object.entries(aggregated)) {
      switch(eventType) {
        case 'player_update':
          // Calculate averages for position data
          const positions = data.samples.map(s => ({
            x: s.Data.position_x,
            y: s.Data.position_y
          }));

          const avgPos = {
            x: positions.reduce((sum, p) => sum + p.x, 0) / positions.length,
            y: positions.reduce((sum, p) => sum + p.y, 0) / positions.length
          };

          console.log(`Player avg position: (${avgPos.x.toFixed(1)}, ${avgPos.y.toFixed(1)}) over ${data.count} samples`);
          break;

        case 'combat_damage':
          const totalDamage = data.samples.reduce((sum, s) => sum + s.Data.damage, 0);
          console.log(`Combat: ${totalDamage} total damage in ${data.count} hits`);
          break;
      }
    }
  }

  getStats() {
    return {
      ...this.stats,
      bufferUsage: this.buffer.length,
      dropRate: (this.stats.dropped / this.stats.received * 100).toFixed(2) + '%'
    };
  }
}

// Usage
const collector = new HighFrequencyCollector({
  bufferSize: 500,
  flushInterval: 500  // Process twice per second
});

// Monitor performance
setInterval(() => {
  console.log('Collector stats:', collector.getStats());
}, 5000);
```

---

## Troubleshooting

### Common Issues and Solutions

#### Connection Refused
```bash
# Check if mod is loaded
grep "Data Export Bus" ~/.steam/steam/steamapps/common/Hollow\ Knight\ Silksong/BepInEx/LogOutput.log

# Check if ports are open
netstat -tulpn | grep -E "8080|9090|9091"

# Test with curl
curl -v http://localhost:8080/api/status
```

#### High Latency
```python
# Reduce update frequency
import requests

requests.post('http://localhost:8080/api/configure', json={
    'update_frequency_hz': 5,  # Reduce to 5Hz
    'enable_advanced_metrics': False  # Disable expensive metrics
})
```

#### Missing Events
```javascript
// Check configuration
fetch('http://localhost:8080/api/status')
  .then(r => r.json())
  .then(status => {
    console.log('Enabled features:', status.configuration);
    // Ensure required data types are enabled
  });
```

#### Memory Issues
```ini
# Edit BepInEx/config/com.hkss.dataexportbus.cfg
[FileExport]
RotationSizeMB = 5  # Smaller files
RotationMinutes = 10  # More frequent rotation

[DataCollection]
UpdateFrequencyHz = 5  # Lower frequency
EnableAdvancedMetrics = false  # Disable heavy metrics
```

---

*This usage guide is part of the HKSS Data Export Bus project. For complete API documentation, see the [API Reference](API.md).*