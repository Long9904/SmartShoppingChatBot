namespace SmartShoppingChatBot.Application.Commons.Behaviors
{
    public class VietnamTimeProvider : TimeProvider
    {
        private static readonly TimeZoneInfo _tz =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public override TimeZoneInfo LocalTimeZone => _tz;
    }
}
