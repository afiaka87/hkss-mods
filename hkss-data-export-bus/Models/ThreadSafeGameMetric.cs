using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using HKSS.DataExportBus.Configuration;

namespace HKSS.DataExportBus.Models
{
    /// <summary>
    /// Thread-safe version of GameMetric with proper concurrent access patterns
    /// </summary>
    public class ThreadSafeGameMetric
    {
        private readonly DateTime _timestamp;
        private readonly string _eventType;
        private readonly ConcurrentDictionary<string, object> _data;
        private readonly ReaderWriterLockSlim _lock;
        private bool _disposed;

        public DateTime Timestamp => _timestamp;
        public string EventType => _eventType;

        public ThreadSafeGameMetric(string eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType))
                throw new ArgumentNullException(nameof(eventType));

            _timestamp = DateTime.UtcNow;
            _eventType = eventType;
            _data = new ConcurrentDictionary<string, object>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _disposed = false;
        }

        /// <summary>
        /// Add or update a metric value in a thread-safe manner
        /// </summary>
        public void AddValue(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            if (_disposed)
                throw new ObjectDisposedException(nameof(ThreadSafeGameMetric));

            _data.AddOrUpdate(key, value, (k, oldValue) => value);
        }

        /// <summary>
        /// Try to get a value from the metric data
        /// </summary>
        public bool TryGetValue(string key, out object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                value = null;
                return false;
            }

            if (_disposed)
            {
                value = null;
                return false;
            }

            return _data.TryGetValue(key, out value);
        }

        /// <summary>
        /// Get a thread-safe snapshot of the data for serialization
        /// </summary>
        public Dictionary<string, object> GetDataSnapshot()
        {
            if (_disposed)
                return new Dictionary<string, object>();

            _lock.EnterReadLock();
            try
            {
                return new Dictionary<string, object>(_data);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Convert to the legacy GameMetric format for backward compatibility
        /// </summary>
        public GameMetric ToLegacyFormat()
        {
            var metric = new GameMetric(EventType)
            {
                Timestamp = Timestamp,
                Data = GetDataSnapshot()
            };
            return metric;
        }

        /// <summary>
        /// Bulk add multiple values efficiently
        /// </summary>
        public void AddValues(Dictionary<string, object> values)
        {
            if (values == null)
                return;

            if (_disposed)
                throw new ObjectDisposedException(nameof(ThreadSafeGameMetric));

            foreach (var kvp in values)
            {
                _data.AddOrUpdate(kvp.Key, kvp.Value, (k, oldValue) => kvp.Value);
            }
        }

        /// <summary>
        /// Check if a key exists
        /// </summary>
        public bool ContainsKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || _disposed)
                return false;

            return _data.ContainsKey(key);
        }

        /// <summary>
        /// Get the count of data entries
        /// </summary>
        public int Count => _disposed ? 0 : _data.Count;

        /// <summary>
        /// Clear all data (thread-safe)
        /// </summary>
        public void Clear()
        {
            if (_disposed)
                return;

            _data.Clear();
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _lock?.Dispose();
                _data?.Clear();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Object pool for ThreadSafeGameMetric instances to reduce allocations
    /// </summary>
    public class GameMetricPool
    {
        private readonly ConcurrentBag<ThreadSafeGameMetric> _pool;
        private readonly int _maxPoolSize;
        private int _currentSize;

        public GameMetricPool(int maxPoolSize = 100)
        {
            _maxPoolSize = maxPoolSize;
            _pool = new ConcurrentBag<ThreadSafeGameMetric>();
            _currentSize = 0;
        }

        /// <summary>
        /// Rent a metric from the pool or create a new one
        /// </summary>
        public ThreadSafeGameMetric Rent(string eventType)
        {
            if (_pool.TryTake(out var metric))
            {
                Interlocked.Decrement(ref _currentSize);
                // Reset the metric for reuse
                metric = new ThreadSafeGameMetric(eventType);
                return metric;
            }

            return new ThreadSafeGameMetric(eventType);
        }

        /// <summary>
        /// Return a metric to the pool
        /// </summary>
        public void Return(ThreadSafeGameMetric metric)
        {
            if (metric == null)
                return;

            // Clear the metric data
            metric.Clear();

            // Only add to pool if we haven't exceeded the size limit
            if (_currentSize < _maxPoolSize)
            {
                _pool.Add(metric);
                Interlocked.Increment(ref _currentSize);
            }
            else
            {
                // Dispose if pool is full
                metric.Dispose();
            }
        }

        /// <summary>
        /// Clear the pool
        /// </summary>
        public void Clear()
        {
            while (_pool.TryTake(out var metric))
            {
                metric.Dispose();
            }
            _currentSize = 0;
        }
    }

    /// <summary>
    /// Builder pattern for creating metrics with fluent API
    /// </summary>
    public class GameMetricBuilder
    {
        private readonly ThreadSafeGameMetric _metric;

        public GameMetricBuilder(string eventType)
        {
            _metric = new ThreadSafeGameMetric(eventType);
        }

        public GameMetricBuilder WithPlayerPosition(float x, float y)
        {
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.POSITION_X, x);
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.POSITION_Y, y);
            return this;
        }

        public GameMetricBuilder WithPlayerVelocity(float x, float y)
        {
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.VELOCITY_X, x);
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.VELOCITY_Y, y);
            return this;
        }

        public GameMetricBuilder WithHealth(int current, int max)
        {
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.HEALTH_CURRENT, current);
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.HEALTH_MAX, max);
            return this;
        }

        public GameMetricBuilder WithSoul(int current, int max)
        {
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.SOUL_CURRENT, current);
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.SOUL_MAX, max);
            return this;
        }

        public GameMetricBuilder WithPlayerState(bool grounded, bool dashing, bool attacking)
        {
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.GROUNDED, grounded);
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.DASHING, dashing);
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.ATTACKING, attacking);
            return this;
        }

        public GameMetricBuilder WithDamage(int damage, string source = null)
        {
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.DAMAGE, damage);
            if (!string.IsNullOrEmpty(source))
            {
                _metric.AddValue("damage_source", source);
            }
            return this;
        }

        public GameMetricBuilder WithScene(string sceneName, float timeInScene = 0)
        {
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.SCENE_NAME, sceneName);
            if (timeInScene > 0)
            {
                _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.TIME_IN_SCENE, timeInScene);
            }
            return this;
        }

        public GameMetricBuilder WithSessionTime(float sessionTime)
        {
            _metric.AddValue(HKSS.DataExportBus.Configuration.Constants.MetricFields.SESSION_TIME, sessionTime);
            return this;
        }

        public GameMetricBuilder WithCustom(string key, object value)
        {
            _metric.AddValue(key, value);
            return this;
        }

        public ThreadSafeGameMetric Build()
        {
            return _metric;
        }
    }
}