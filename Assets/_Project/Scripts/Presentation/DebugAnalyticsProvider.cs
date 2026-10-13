using System.Globalization;
using System.Text;
using MiniFactory.Analytics;
using UnityEngine;

namespace MiniFactory.Presentation
{
    public sealed class DebugAnalyticsProvider : IAnalyticsProvider
    {
        public void Track(AnalyticsEvent analyticsEvent)
        {
            var message = new StringBuilder("[Analytics] ").Append(analyticsEvent.Name);
            foreach (var parameter in analyticsEvent.Parameters)
                message.Append(" | ").Append(parameter.Key).Append('=')
                    .Append(System.Convert.ToString(parameter.Value, CultureInfo.InvariantCulture));
            Debug.Log(message.ToString());
        }
    }
}
