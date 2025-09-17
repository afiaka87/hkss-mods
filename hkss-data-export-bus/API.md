# Data Export Bus API Reference

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![Protocol](https://img.shields.io/badge/protocols-HTTP%20%7C%20WebSocket%20%7C%20TCP-green)
![Platform](https://img.shields.io/badge/platform-Hollow%20Knight%20Silksong-purple)

## Table of Contents

- [Overview](#overview)
- [Getting Started](#getting-started)
- [Authentication](#authentication)
- [Base URL & Ports](#base-url--ports)
- [HTTP REST API](#http-rest-api)
  - [Core Endpoints](#core-endpoints)
  - [Request/Response Format](#requestresponse-format)
  - [Status Codes](#status-codes)
- [WebSocket API](#websocket-api)
  - [Connection](#connection)
  - [Message Format](#message-format)
  - [Event Types](#event-types)
- [TCP Protocol](#tcp-protocol)
  - [LiveSplit Integration](#livesplit-integration)
  - [Commands](#commands)
- [Metric Types](#metric-types)
  - [Standard Metrics](#standard-metrics)
  - [Advanced Metrics](#advanced-metrics)
- [Error Handling](#error-handling)
- [Rate Limiting](#rate-limiting)
- [Configuration](#configuration)
- [Version History](#version-history)

---

## Overview

The **Data Export Bus** is a high-performance, multi-protocol data streaming service for Hollow Knight: Silksong. It provides real-time game state access through HTTP REST, WebSocket, and TCP protocols, enabling integration with external tools like OBS, LiveSplit, and custom analytics platforms.

### Key Features

- **Real-time Streaming**: Sub-100ms latency for live game events
- **Multiple Protocols**: HTTP, WebSocket, TCP, and file export support
- **Comprehensive Data**: Player state, combat events, scene transitions, and Unity engine metrics
- **Advanced Metrics**: Deep introspection into Unity engine, BepInEx, and game internals
- **Thread-safe**: Concurrent access from multiple clients
- **Configurable**: Extensive configuration options via BepInEx

### Architecture

```
Game → BepInEx → Data Export Bus → [HTTP/WebSocket/TCP/Files] → External Tools
```

---

## Getting Started

### Quick Start

```bash
# Check if the service is running
curl -X GET http://localhost:8080/api/status

# Get latest metrics
curl -X GET http://localhost:8080/api/metrics

# Stream real-time data
websocat ws://localhost:9091
```

### Minimum Requirements

- Hollow Knight: Silksong with BepInEx 5.x
- Data Export Bus mod installed and enabled
- Network access to configured ports (default: 8080, 9090, 9091)

---

## Authentication

### Token-Based Authentication

When configured with an auth token, include it in requests:

```bash
# HTTP Header
curl -H "Authorization: Bearer YOUR_TOKEN" http://localhost:8080/api/metrics

# WebSocket (first message after connection)
{"type": "auth", "token": "YOUR_TOKEN"}

# TCP (first line after connection)
AUTH:YOUR_TOKEN
```

### CORS Configuration

Configure allowed origins in `BepInEx/config/com.hkss.dataexportbus.cfg`:

```ini
[Security]
AllowedOrigins = http://localhost:*, https://mysite.com
```

---

## Base URL & Ports

| Protocol | Default Port | Base URL | Purpose |
|----------|-------------|----------|---------|
| HTTP | 8080 | `http://localhost:8080` | REST API & Dashboard |
| WebSocket | 9091 | `ws://localhost:9091` | Real-time streaming |
| TCP | 9090 | `localhost:9090` | LiveSplit integration |

All ports and bind addresses are configurable via BepInEx configuration.

---

## HTTP REST API

### Core Endpoints

#### `GET /api/status`

Returns service health and configuration status.

**Request:**
```bash
curl -X GET http://localhost:8080/api/status
```

**Response:**
```json
{
  "status": "healthy",
  "version": "1.0.0",
  "uptime_seconds": 3600,
  "active_connections": {
    "http": 2,
    "websocket": 5,
    "tcp": 1
  },
  "metrics_collected": 15234,
  "configuration": {
    "update_frequency_hz": 10,
    "export_player_data": true,
    "export_combat_data": true,
    "export_scene_data": true,
    "advanced_metrics_enabled": true
  }
}
```

**Use Cases:**
- Health monitoring for automated systems
- Verifying configuration before streaming
- Dashboard integration status checks

---

#### `GET /api/metrics`

Returns the latest game metrics snapshot.

**Request:**
```bash
curl -X GET http://localhost:8080/api/metrics
```

**Response:**
```json
[
  {
    "Timestamp": "2024-01-15T14:30:45.123Z",
    "EventType": "player_update",
    "Data": {
      "position_x": 1234.56,
      "position_y": 789.01,
      "velocity_x": 5.2,
      "velocity_y": -3.1,
      "health_current": 5,
      "health_max": 5,
      "on_ground": true,
      "is_dashing": false
    }
  },
  {
    "Timestamp": "2024-01-15T14:30:45.125Z",
    "EventType": "timing_update",
    "Data": {
      "fps": 59.8,
      "frame_time_ms": 16.7,
      "session_time": 3600.5,
      "scene_time": 120.3
    }
  }
]
```

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `type` | string | all | Filter by event type (e.g., `player_update`, `combat_damage`) |
| `limit` | int | 100 | Maximum metrics to return (1-1000) |

**Example with filters:**
```bash
# Get only combat events
curl "http://localhost:8080/api/metrics?type=combat_damage&limit=50"
```

---

#### `GET /api/recent`

Returns recent metrics from the buffer with optional filtering.

**Request:**
```bash
curl -X GET "http://localhost:8080/api/recent?count=20&type=scene_transition"
```

**Response:**
```json
[
  {
    "Timestamp": "2024-01-15T14:29:30.000Z",
    "EventType": "scene_transition",
    "Data": {
      "from_scene": "Tutorial_01",
      "to_scene": "Crossroads_01",
      "transition_type": "door",
      "load_time_ms": 1250
    }
  }
]
```

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `count` | int | 10 | Number of recent metrics (1-100) |
| `type` | string | all | Filter by event type |
| `since` | ISO8601 | - | Only metrics after this timestamp |

---

#### `POST /api/configure`

Dynamically update configuration without restarting.

**Request:**
```bash
curl -X POST http://localhost:8080/api/configure \
  -H "Content-Type: application/json" \
  -d '{
    "update_frequency_hz": 30,
    "export_combat_data": false
  }'
```

**Response:**
```json
{
  "success": true,
  "applied": {
    "update_frequency_hz": 30,
    "export_combat_data": false
  },
  "warnings": []
}
```

**Configurable Fields:**

| Field | Type | Range | Description |
|-------|------|-------|-------------|
| `update_frequency_hz` | float | 1-60 | Data collection rate |
| `export_player_data` | bool | - | Enable player tracking |
| `export_combat_data` | bool | - | Enable combat events |
| `export_scene_data` | bool | - | Enable scene tracking |
| `export_timing_data` | bool | - | Enable FPS/timing |
| `enable_advanced_metrics` | bool | - | Enable deep introspection |

---

### Request/Response Format

All endpoints accept and return JSON with UTF-8 encoding.

**Request Headers:**
```http
Content-Type: application/json
Accept: application/json
Authorization: Bearer <token>  # If authentication enabled
```

**Response Headers:**
```http
Content-Type: application/json; charset=utf-8
X-Rate-Limit-Remaining: 95
X-Rate-Limit-Reset: 1642267890
Access-Control-Allow-Origin: *
```

---

### Status Codes

| Code | Status | Description |
|------|--------|-------------|
| 200 | OK | Request successful |
| 400 | Bad Request | Invalid parameters or malformed JSON |
| 401 | Unauthorized | Missing or invalid auth token |
| 404 | Not Found | Endpoint does not exist |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Server-side error |
| 503 | Service Unavailable | Service is starting or shutting down |

---

## WebSocket API

### Connection

Connect to the WebSocket endpoint for real-time streaming:

```javascript
const ws = new WebSocket('ws://localhost:9091');

ws.onopen = () => {
  console.log('Connected to Data Export Bus');
  // Send auth if required
  ws.send(JSON.stringify({ type: 'auth', token: 'YOUR_TOKEN' }));
};

ws.onmessage = (event) => {
  const metric = JSON.parse(event.data);
  console.log(`Event: ${metric.EventType}`, metric.Data);
};

ws.onerror = (error) => {
  console.error('WebSocket error:', error);
};
```

### Message Format

All messages are JSON-encoded with this structure:

```typescript
interface GameMetric {
  Timestamp: string;      // ISO8601 timestamp
  EventType: string;      // Event identifier
  Data: Record<string, any>; // Event-specific data
}
```

### Event Types

Real-time events streamed via WebSocket:

| Event Type | Frequency | Description |
|------------|-----------|-------------|
| `player_update` | 10Hz | Player position, velocity, state |
| `combat_damage` | On event | Damage dealt/received |
| `enemy_killed` | On event | Enemy defeat |
| `scene_transition` | On event | Room changes |
| `ability_used` | On event | Skill/ability activation |
| `item_collected` | On event | Collectible obtained |
| `checkpoint_reached` | On event | Save point activated |
| `boss_encounter` | On event | Boss fight start/end |
| `timing_update` | 1Hz | FPS and timing stats |

**Example Stream:**
```json
{"Timestamp":"2024-01-15T14:30:45.123Z","EventType":"player_update","Data":{"position_x":100.5,"health_current":4}}
{"Timestamp":"2024-01-15T14:30:45.234Z","EventType":"combat_damage","Data":{"damage":1,"source":"spike"}}
{"Timestamp":"2024-01-15T14:30:46.123Z","EventType":"player_update","Data":{"position_x":102.3,"health_current":4}}
```

---

## TCP Protocol

### LiveSplit Integration

The TCP protocol is optimized for speedrun timer integration:

```bash
# Connect via netcat
nc localhost 9090

# Or via telnet
telnet localhost 9090
```

### Commands

Commands sent by the Data Export Bus to connected clients:

| Command | Format | Trigger | Description |
|---------|--------|---------|-------------|
| Split | `split\n` | Scene match | Advance to next split |
| Reset | `reset\n` | Death/menu | Reset timer |
| Pause | `pause\n` | Game pause | Pause timer |
| Resume | `resume\n` | Unpause | Resume timer |
| Start | `start\n` | New game | Start timer |
| Time | `time:123.45\n` | Update | Game time in seconds |

**Example Session:**
```
Connected to localhost 9090
start
time:0.00
time:1.23
time:2.45
split
time:45.67
pause
resume
time:89.01
```

---

## Metric Types

### Standard Metrics

#### Player Update
Tracks player state at configured frequency (default 10Hz).

```json
{
  "EventType": "player_update",
  "Data": {
    "position_x": 1234.56,
    "position_y": 789.01,
    "velocity_x": 5.2,
    "velocity_y": -3.1,
    "health_current": 5,
    "health_max": 5,
    "mp_current": 33,
    "mp_max": 99,
    "mp_reserve": 0,
    "on_ground": true,
    "facing_right": true,
    "is_dashing": false,
    "is_attacking": false,
    "is_jumping": false,
    "is_falling": true,
    "is_wall_sliding": false,
    "has_control": true
  }
}
```

#### Combat Events
Captures all combat interactions.

```json
{
  "EventType": "combat_damage",
  "Data": {
    "damage": 2,
    "source": "enemy_projectile",
    "enemy_type": "Aspid",
    "player_health_remaining": 3,
    "position_x": 500.0,
    "position_y": 250.0,
    "is_player_damage": true
  }
}
```

#### Scene Transitions
Room change events with timing data.

```json
{
  "EventType": "scene_transition",
  "Data": {
    "from_scene": "Crossroads_01",
    "to_scene": "Crossroads_02",
    "transition_type": "door",
    "transition_side": "right",
    "load_time_ms": 1250,
    "total_scene_time": 45.67,
    "deaths_in_scene": 0
  }
}
```

### Advanced Metrics

Advanced metrics provide deep introspection into the game engine and runtime environment.

#### Unity Engine Metrics
System performance and resource usage.

```json
{
  "EventType": "unity_engine_metrics",
  "Data": {
    "total_allocated_memory_mb": 512.3,
    "mono_heap_size_mb": 256.1,
    "mono_used_size_mb": 128.5,
    "graphics_memory_mb": 1024.7,
    "temp_allocator_size_mb": 64.0,
    "total_reserved_memory_mb": 768.2,
    "total_unused_reserved_memory_mb": 256.1,
    "audio_dsp_cpu_usage": 2.3,
    "audio_stream_cpu_usage": 1.1,
    "audio_other_cpu_usage": 0.5,
    "draw_calls": 145,
    "batches": 89,
    "triangles": 25600,
    "vertices": 15200,
    "render_time_ms": 5.2,
    "gpu_time_ms": 4.8,
    "unity_version": "6000.0.50",
    "platform": "WindowsPlayer",
    "system_memory_size_mb": 16384,
    "graphics_device_name": "NVIDIA GeForce RTX 3080",
    "graphics_device_version": "DirectX 12",
    "graphics_memory_size_mb": 10240,
    "processor_type": "AMD Ryzen 9 5900X",
    "processor_count": 12,
    "processor_frequency_mhz": 3700
  }
}
```

#### Detailed Player State
Complete HeroController state flags and animation data.

```json
{
  "EventType": "detailed_player_state",
  "Data": {
    "animation_state": {
      "current_clip": "Run",
      "clip_time": 0.45,
      "clip_length": 1.2,
      "speed": 1.0,
      "is_playing": true,
      "state_hash": 123456789,
      "layer_count": 2,
      "layer_weights": [1.0, 0.0]
    },
    "physics_state": {
      "rigidbody_velocity": {"x": 5.2, "y": -3.1},
      "rigidbody_angular_velocity": 0.0,
      "rigidbody_mass": 1.0,
      "rigidbody_drag": 0.5,
      "collider_enabled": true,
      "is_trigger": false,
      "collision_layer": "Player",
      "grounded_raycast_hit": true,
      "wall_left_raycast_hit": false,
      "wall_right_raycast_hit": false
    },
    "controller_flags": {
      "onGround": true,
      "wasOnGround": true,
      "touchingWall": false,
      "wallSliding": false,
      "dashing": false,
      "superDashing": false,
      "dashCooldown": false,
      "backDashing": false,
      "jumping": false,
      "doubleJumping": false,
      "falling": true,
      "swimming": false,
      "attacking": false,
      "lookingUp": false,
      "lookingDown": false,
      "altAttack": false,
      "upAttacking": false,
      "downAttacking": false,
      "bouncing": false,
      "shroomBouncing": false,
      "recoiling": false,
      "dead": false,
      "hazardDeath": false,
      "invulnerable": false,
      "isPaused": false
    },
    "ability_states": {
      "canDash": true,
      "canJump": true,
      "canDoubleJump": true,
      "canAttack": true,
      "canNailCharge": false,
      "canWallJump": true,
      "hasDash": true,
      "hasDoubleJump": true,
      "hasSuperDash": false,
      "hasWallJump": true
    }
  }
}
```

#### BepInEx Plugin Information
Loaded mods and their status.

```json
{
  "EventType": "bepinex_plugin_info",
  "Data": {
    "plugin_count": 5,
    "plugins": [
      {
        "guid": "com.hkss.dataexportbus",
        "name": "Data Export Bus",
        "version": "1.0.0",
        "enabled": true,
        "location": "BepInEx/plugins/HKSS.DataExportBus.dll",
        "dependencies": []
      }
    ],
    "bepinex_version": "5.4.22",
    "unity_version": "6000.0.50",
    "process_name": "Hollow Knight Silksong.exe",
    "config_path": "BepInEx/config/",
    "plugin_path": "BepInEx/plugins/"
  }
}
```

---

## Error Handling

### HTTP Errors

```json
{
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Too many requests. Limit: 100/minute",
    "retry_after": 30
  }
}
```

### WebSocket Errors

```json
{
  "type": "error",
  "error": {
    "code": "AUTH_FAILED",
    "message": "Invalid authentication token"
  }
}
```

### TCP Errors

```
ERROR:COMMAND_INVALID
ERROR:AUTH_REQUIRED
```

---

## Rate Limiting

When enabled, rate limiting applies per IP address:

| Limit Type | Default | Header |
|------------|---------|--------|
| Requests/minute | 100 | `X-Rate-Limit-Limit` |
| Requests/hour | 1000 | `X-Rate-Limit-Limit-Hour` |
| Concurrent connections | 5 | `X-Connection-Limit` |

Exceeded limits return HTTP 429 with retry information:

```http
HTTP/1.1 429 Too Many Requests
X-Rate-Limit-Limit: 100
X-Rate-Limit-Remaining: 0
X-Rate-Limit-Reset: 1642267890
Retry-After: 30
```

---

## Configuration

### Configuration File Location

```
BepInEx/config/com.hkss.dataexportbus.cfg
```

### Key Configuration Options

```ini
[General]
Enabled = true

[HTTP]
Port = 8080
BindAddress = 0.0.0.0
EnableHttpServer = true

[WebSocket]
Port = 9091
EnableWebSocketServer = true

[TCP]
Port = 9090
EnableTcpServer = true
EnableNamedPipe = false

[DataCollection]
UpdateFrequencyHz = 10
ExportPlayerData = true
ExportCombatData = true
ExportSceneData = true
ExportTimingData = true
EnableAdvancedMetrics = false
AdvancedMetricsIntervalSec = 0.5

[Security]
AuthToken =
AllowedOrigins = *
EnableRateLimiting = false
MaxConnectionsPerIP = 5

[FileExport]
EnableFileExport = true
Directory = DataExport
Format = NDJSON
RotationSizeMB = 10
RotationMinutes = 30
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-01 | Added advanced metrics collection (Unity, BepInEx, detailed state) |
| 0.2.0 | 2024-01 | Added WebSocket support and real-time streaming |
| 0.1.0 | 2024-01 | Initial release with HTTP, TCP, and file export |

---

## Support & Resources

- **GitHub Issues**: Report bugs and request features
- **Discord Community**: Join for support and discussions
- **Example Integrations**: See the `examples/` directory
- **OBS Widgets**: Pre-built browser sources in `widget-*.html`

---

*This documentation is part of the HKSS Data Export Bus project for Hollow Knight: Silksong modding.*