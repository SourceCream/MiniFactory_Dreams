using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MiniFactory.Analytics
{
    public sealed class AnalyticsEvent
    {
        public string Name { get; }
        public IReadOnlyDictionary<string, object> Parameters { get; }

        public AnalyticsEvent(string name, params (string Key, object Value)[] parameters)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Event name is required.", nameof(name));
            Name = name;
            var copy = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Key)) throw new ArgumentException("Parameter name is required.", nameof(parameters));
                object value = parameter.Value;
                if (value != null && !(value is string || value is bool || value is int || value is long ||
                    value is float || value is double || value is decimal))
                    throw new ArgumentException("Analytics parameters must be scalar values.", nameof(parameters));
                copy.Add(parameter.Key, value);
            }
            Parameters = new ReadOnlyDictionary<string, object>(copy);
        }
    }
}
