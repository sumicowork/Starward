﻿using Starward.Core.JsonConverter;
using System.Text.Json.Serialization;

namespace Starward.Core.GameRecord.StarRail.TrailblazeCalendar;

/// <summary>
/// 开拓月历明细单项
/// </summary>
public class TrailblazeCalendarDetailItem
{

    [JsonIgnore]
    public int Id { get; set; }

    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long Uid { get; set; }

    /// <summary>
    /// 202304
    /// </summary>
    public string Month { get; set; }

    /// <summary>
    /// 1 星琼  2 星轨票
    /// </summary>
    public int Type { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("action_name")]
    public string ActionName { get; set; }

    [JsonPropertyName("time")]
    [JsonConverter(typeof(DateTimeStringJsonConverter))]
    public DateTime Time { get; set; }

    [JsonPropertyName("num")]
    public int Number { get; set; }

    /// <summary>
    /// Icon path based on type (1=Stellar Jade, 2=Pass)
    /// </summary>
    [JsonIgnore]
    public string IconPath => Type == 1
        ? "ms-appx:///Assets/Image/900001.png"
        : "ms-appx:///Assets/Image/101.png";

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

