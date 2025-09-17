using System;

namespace HKSS.DataExportBus.Configuration
{
    public static class Constants
    {
        // Network Configuration
        public static class Network
        {
            public const int DEFAULT_HTTP_PORT = 8080;
            public const int DEFAULT_TCP_PORT = 9090;
            public const int DEFAULT_WEBSOCKET_PORT = 9091;
            public const string DEFAULT_BIND_ADDRESS = "localhost";
            public const int MIN_PORT = 1024;
            public const int MAX_PORT = 65535;
            public const int MAX_CONCURRENT_HTTP_REQUESTS = 10;
            public const int MAX_REQUEST_SIZE_BYTES = 10 * 1024 * 1024; // 10MB
            public const int CONNECTION_TIMEOUT_MS = 30000; // 30 seconds
            public const int READ_TIMEOUT_MS = 5000; // 5 seconds
            public const string NAMED_PIPE_NAME = "LiveSplitDataExport";
        }

        // WebSocket Configuration
        public static class WebSocket
        {
            public const int PING_INTERVAL_MS = 30000; // 30 seconds
            public const int PING_TIMEOUT_MS = 10000; // 10 seconds
            public const int RECEIVE_BUFFER_SIZE = 4096;
            public const int SEND_BUFFER_SIZE = 4096;
            public const int MAX_MESSAGE_SIZE = 65536; // 64KB
        }

        // HTTP Server Configuration
        public static class Http
        {
            public const int MAX_QUEUE_SIZE = 5000;
            public const int MAX_RESPONSE_SIZE = 1024 * 1024; // 1MB
            public const string CORS_MAX_AGE = "600";
            public const string AUTH_HEADER_PREFIX = "Bearer ";
        }

        // File Export Configuration
        public static class FileExport
        {
            public const int DEFAULT_ROTATION_SIZE_MB = 10;
            public const int DEFAULT_ROTATION_MINUTES = 30;
            public const int MAX_ROTATION_SIZE_MB = 100;
            public const int MIN_ROTATION_SIZE_MB = 1;
            public const int MAX_ROTATION_MINUTES = 1440; // 24 hours
            public const int MIN_ROTATION_MINUTES = 1;
            public const int MAX_ARCHIVE_FILES = 10;
            public const string ARCHIVE_DIRECTORY_NAME = "archive";
            public const string CSV_EXTENSION = ".csv";
            public const string NDJSON_EXTENSION = ".ndjson";
            public const string TIMESTAMP_FORMAT = "yyyyMMdd_HHmmss";
            public const string ISO_DATETIME_FORMAT = "o";
        }

        // Metrics Collection Configuration
        public static class Metrics
        {
            public const float DEFAULT_UPDATE_FREQUENCY_HZ = 10f;
            public const float MIN_UPDATE_FREQUENCY_HZ = 1f;
            public const float MAX_UPDATE_FREQUENCY_HZ = 60f;
            public const float DEFAULT_ADVANCED_METRICS_INTERVAL_SEC = 0.5f;
            public const float MIN_ADVANCED_METRICS_INTERVAL_SEC = 0.1f;
            public const float MAX_ADVANCED_METRICS_INTERVAL_SEC = 10f;
            public const int MAX_RECENT_EVENTS = 20;
            public const int EVENT_LIST_TRIM_SIZE = 20;
        }

        // Rate Limiting Configuration
        public static class RateLimit
        {
            public const int DEFAULT_MAX_CONNECTIONS_PER_IP = 5;
            public const int MAX_CONNECTIONS_PER_IP = 20;
            public const int MIN_CONNECTIONS_PER_IP = 1;
            public const int RATE_LIMIT_WINDOW_SECONDS = 60;
            public const int MAX_REQUESTS_PER_WINDOW = 100;
        }

        // Timer Configuration
        public static class Timers
        {
            public const int FILE_ROTATION_CHECK_INTERVAL_MINUTES = 1;
            public const int CLEANUP_INTERVAL_SECONDS = 60;
            public const int HEALTH_CHECK_INTERVAL_SECONDS = 30;
        }

        // LiveSplit Configuration
        public static class LiveSplit
        {
            public const string DEFAULT_AUTO_SPLIT_SCENES = "Boss_Defeated,Area_Complete,Major_Item_Obtained";
            public const string TIMER_NOT_RUNNING = "NotRunning";
            public const string TIMER_RUNNING = "Running";
            public const string TIMER_ENDED = "Ended";
            public const string TIME_FORMAT = @"hh\:mm\:ss\.fff";
            public const string DEFAULT_TIME = "00:00:00.000";
        }

        // OBS Integration
        public static class OBS
        {
            public const string SIMPLIFIED_VERSION = "simplified-1.0";
            public const int RPC_VERSION = 1;
            public const int RESPONSE_OPCODE = 7;
            public const int SUCCESS_CODE = 100;
            public const int ERROR_CODE = 204;
            public static readonly string[] DEFAULT_SCENES = { "Gameplay", "Boss Fight", "Main Menu", "Cutscene", "Death Screen" };
        }

        // Field Names for Metrics
        public static class MetricFields
        {
            // Player fields
            public const string POSITION_X = "position_x";
            public const string POSITION_Y = "position_y";
            public const string VELOCITY_X = "velocity_x";
            public const string VELOCITY_Y = "velocity_y";
            public const string HEALTH_CURRENT = "health_current";
            public const string HEALTH_MAX = "health_max";
            public const string SOUL_CURRENT = "soul_current";
            public const string SOUL_MAX = "soul_max";
            public const string GROUNDED = "grounded";
            public const string DASHING = "dashing";
            public const string ATTACKING = "attacking";
            public const string TOTAL_DISTANCE = "total_distance";

            // Scene fields
            public const string SCENE_NAME = "scene_name";
            public const string TIME_IN_SCENE = "time_in_scene";
            public const string ROOM_POSITION_X = "room_position_x";
            public const string ROOM_POSITION_Y = "room_position_y";

            // Combat fields
            public const string DAMAGE = "damage";
            public const string ENEMY_NAME = "enemy_name";
            public const string ENEMY_POSITION_X = "enemy_position_x";
            public const string ENEMY_POSITION_Y = "enemy_position_y";
            public const string HEALTH_REMAINING = "health_remaining";
            public const string TOTAL_KILLS = "total_kills";
            public const string TOTAL_DAMAGE_TAKEN = "total_damage_taken";
            public const string TOTAL_DAMAGE_DEALT = "total_damage_dealt";
            public const string ENEMIES_KILLED = "enemies_killed";

            // Timing fields
            public const string SESSION_TIME = "session_time";
            public const string REAL_TIME = "real_time";
            public const string FRAME_COUNT = "frame_count";
            public const string FPS = "fps";

            // Item/ability fields
            public const string ITEM_NAME = "item_name";
            public const string QUANTITY = "quantity";
            public const string ABILITY_NAME = "ability_name";

            // Boss fields
            public const string BOSS_NAME = "boss_name";
            public const string EVENT_TYPE = "event_type";
        }

        // Event Types
        public static class EventTypes
        {
            public const string PLAYER_UPDATE = "player_update";
            public const string SCENE_UPDATE = "scene_update";
            public const string SCENE_TRANSITION = "scene_transition";
            public const string TIMING_UPDATE = "timing_update";
            public const string COMBAT_STATS = "combat_stats";
            public const string PLAYER_DAMAGED = "player_damaged";
            public const string ENEMY_DAMAGED = "enemy_damaged";
            public const string ENEMY_KILLED = "enemy_killed";
            public const string ITEM_COLLECTED = "item_collected";
            public const string ABILITY_UNLOCKED = "ability_unlocked";
            public const string BOSS_EVENT = "boss_event";
        }

        // Boss Event Types
        public static class BossEventTypes
        {
            public const string START = "start";
            public const string PHASE_CHANGE = "phase_change";
            public const string DEFEAT = "defeat";
        }

        // Message Types
        public static class MessageTypes
        {
            public const string AUTH = "auth";
            public const string AUTHENTICATE = "authenticate";
            public const string AUTHENTICATED = "authenticated";
            public const string SUBSCRIBE = "subscribe";
            public const string UNSUBSCRIBE = "unsubscribe";
            public const string SUBSCRIBED = "subscribed";
            public const string UNSUBSCRIBED = "unsubscribed";
            public const string PING = "ping";
            public const string PONG = "pong";
            public const string HELLO = "hello";
            public const string ERROR = "error";
            public const string METRIC = "metric";
            public const string EVENT = "event";
        }

        // Error Messages
        public static class ErrorMessages
        {
            public const string AUTHENTICATION_REQUIRED = "Authentication required";
            public const string INVALID_TOKEN = "Invalid token";
            public const string UNAUTHORIZED = "Unauthorized";
            public const string NOT_FOUND = "Not found";
            public const string METHOD_NOT_ALLOWED = "Method not allowed";
            public const string INTERNAL_SERVER_ERROR = "Internal server error";
            public const string TIMER_NOT_RUNNING = "Timer not running";
            public const string NO_SPLITS_TO_UNDO = "No splits to undo";
            public const string TIME_PARAMETER_REQUIRED = "Time parameter required";
            public const string PATH_CANNOT_BE_EMPTY = "Path cannot be null or empty";
            public const string BASE_DIRECTORY_CANNOT_BE_EMPTY = "Base directory cannot be null or empty";
            public const string PATH_TRAVERSAL_NOT_ALLOWED = "Path traversal is not allowed";
            public const string PATH_MUST_BE_WITHIN_BASE = "Path must be within the base directory";
            public const string FILE_NAME_CANNOT_BE_EMPTY = "File name cannot be null or empty";
        }

        // HTTP Methods
        public static class HttpMethods
        {
            public const string GET = "GET";
            public const string POST = "POST";
            public const string OPTIONS = "OPTIONS";
        }

        // Content Types
        public static class ContentTypes
        {
            public const string APPLICATION_JSON = "application/json";
            public const string TEXT_HTML = "text/html";
            public const string TEXT_PLAIN = "text/plain";
            public const string TEXT_CSV = "text/csv";
        }

        // Limits
        public static class Limits
        {
            public const int MAX_FILE_NAME_LENGTH = 255;
            public const int MAX_PATH_LENGTH = 260; // Windows MAX_PATH
            public const int MAX_TOKEN_LENGTH = 256;
            public const int MAX_SCENE_NAME_LENGTH = 128;
            public const int MAX_ITEM_NAME_LENGTH = 128;
        }
    }
}