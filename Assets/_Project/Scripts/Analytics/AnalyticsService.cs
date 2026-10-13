using System;
using System.Collections.Generic;

namespace MiniFactory.Analytics
{
    /// <summary>Fan-out to independent providers. Provider failures must never interrupt gameplay.</summary>
    public sealed class AnalyticsService
    {
        private readonly IAnalyticsProvider[] providers;
        private readonly Action<Exception> reportError;

        public AnalyticsService(IEnumerable<IAnalyticsProvider> providers, Action<Exception> reportError = null)
        {
            if (providers == null) throw new ArgumentNullException(nameof(providers));
            var copy = new List<IAnalyticsProvider>(providers);
            if (copy.Contains(null)) throw new ArgumentException("Providers cannot be null.", nameof(providers));
            this.providers = copy.ToArray();
            this.reportError = reportError;
        }

        public void Track(string name, params (string Key, object Value)[] parameters)
        {
            var analyticsEvent = new AnalyticsEvent(name, parameters);
            foreach (IAnalyticsProvider provider in providers)
            {
                try { provider.Track(analyticsEvent); }
                catch (Exception exception)
                {
                    // Even a diagnostic callback must not turn telemetry into a gameplay failure.
                    try { reportError?.Invoke(exception); }
                    catch (Exception) { }
                }
            }
        }
    }
}
