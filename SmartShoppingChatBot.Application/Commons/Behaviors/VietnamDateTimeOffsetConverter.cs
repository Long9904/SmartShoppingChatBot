using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartShoppingChatBot.Application.Commons.Behaviors
{
    public class VietnamDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        private static readonly TimeZoneInfo VnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTimeOffset.Parse(reader.GetString()!);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            var vnTime = TimeZoneInfo.ConvertTime(value, VnTimeZone);

            writer.WriteStringValue(
                vnTime.ToString("yyyy-MM-ddTHH:mm:sszzz"));
        }
    }
}
