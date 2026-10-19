namespace VigilWatt.Core.Plugins.Vgn;

/// <summary>
/// 功 能： 维盛（weisheng）2.4G 电量协议的帧构造与应答解析（纯函数，零 I/O）。
/// 2026/10/19 11:45:00 maluy
/// 帧布局、写入坑与真机实测见 docs/hardware/protocols.md §7。
/// </summary>
public static class VgnCodec
{
    /// <summary>查询与应答共用的 report ID（协议固定值，不取自 caps）。</summary>
    public const byte ReportId = 4;

    /// <summary>报文长度 = 接口的 OutputReportByteLength（含 report ID 首字节）。</summary>
    public const int FrameLength = 64;

    /// <summary>应答标志所在下标：应答第 4 字节固定 0x1A 才是电量应答。</summary>
    private const int ReplyTagOffset = 3;

    /// <summary>0x1A = "这是电量查询的应答"。</summary>
    private const byte BatteryReplyTag = 0x1A;

    /// <summary>后续命令字节（查询与应答同形，构造时照抄）。</summary>
    private const byte CommandByte = 0x06;

    /// <summary>电量百分比所在下标。</summary>
    private const int PercentOffset = 8;

    /// <summary>充电标志所在下标（非 0 = 充电中）。</summary>
    private const int ChargingOffset = 9;

    /// <summary>构造电量查询帧：report ID + 负载 00 00 1A 06 00 00 00，其余补 0。</summary>
    public static byte[] BuildQuery(int length = FrameLength)
    {
        var frame = new byte[length];
        frame[0] = ReportId;
        frame[ReplyTagOffset] = BatteryReplyTag;
        frame[ReplyTagOffset + 1] = CommandByte;
        return frame;
    }

    /// <summary>解析应答：第 4 字节非 0x1A 的不是电量应答；电量 &gt;100 判为不可信。</summary>
    public static bool TryParse(byte[] frame, out int percent, out bool charging)
    {
        percent = 0;
        charging = false;
        if (frame.Length <= ChargingOffset) return false;
        if (frame[ReplyTagOffset] != BatteryReplyTag) return false;

        int value = frame[PercentOffset];
        if (value > 100) return false;

        percent = value;
        charging = frame[ChargingOffset] != 0;
        return true;
    }
}
