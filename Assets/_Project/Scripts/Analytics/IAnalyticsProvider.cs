namespace MiniFactory.Analytics
{
    public interface IAnalyticsProvider
    {
        void Track(AnalyticsEvent analyticsEvent);
    }
}
