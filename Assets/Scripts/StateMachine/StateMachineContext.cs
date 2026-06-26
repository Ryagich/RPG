using System;
using System.Collections.Generic;
using UnityEngine;

namespace StateMachine
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class StateMachineContext
    {
        private readonly Dictionary<Type, object> services = new();
        private readonly Dictionary<string, object> values = new();

        public GameObject Owner;
        public float DeltaTime;
        public float ElapsedTime;

        public void SetService<T>(T service) where T : class
        {
            if (service == null)
            {
                services.Remove(typeof(T));
                return;
            }

            services[typeof(T)] = service;
        }

        public bool TryGetService<T>(out T service) where T : class
        {
            if (services.TryGetValue(typeof(T), out object value) && value is T typedValue)
            {
                service = typedValue;
                return true;
            }

            service = null;
            return false;
        }

        public T GetService<T>() where T : class
        {
            return TryGetService(out T service) ? service : null;
        }

        public void SetValue<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("State machine context key cannot be empty.", nameof(key));
            }

            values[key] = value;
        }

        public bool TryGetValue<T>(string key, out T value)
        {
            if (values.TryGetValue(key, out object rawValue) && rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default;
            return false;
        }

        public bool RemoveValue(string key)
        {
            return values.Remove(key);
        }
    }
}
