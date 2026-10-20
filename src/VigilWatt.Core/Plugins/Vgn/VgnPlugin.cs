using VigilWatt.Core.Hid;
using VigilWatt.Core.Models;
using VigilWatt.Core.Utils;

namespace VigilWatt.Core.Plugins.Vgn;

/// <summary>
/// 功 能： VGN（维盛）2.4G 键盘电量插件，主动发查询帧读电量。
/// 2026/10/19 11:45:00 maluy
/// 帧格式、写入坑与真机实测见 docs/hardware/protocols.md §7。
/// </summary>
public sealed class VgnPlugin : BatteryPluginBase
{
    /// <summary>VGN 接收器的 USB 厂商 ID（深圳 Evision 半导体方案，多品牌共用）。</summary>
    public const ushort VgnVid = 0x320F;

    public override string Name => "Vgn";
    public override string DisplayName => "VGN";
    public override DeviceKind Kind => DeviceKind.Keyboard;

    /// <summary>探测要写查询帧，不能走零写入的快速发现节拍；改用 SupportsDiscoveryProbe。</summary>
    public override bool SupportsDiscoveryProbe => true;

    /// <summary>10 秒 &lt; 15 秒发现节拍：冷却若 ≥ 一拍，重试间隔会被相位拉成两倍（同迈从）。</summary>
    protected override TimeSpan ProbeCooldown => TimeSpan.FromSeconds(10);

    /// <summary>
    /// 协议里没有"连接状态"字段，沉默毫无信息量；改由发过帧的分支自己报账
    /// （CountPollFailures = false + ReportPollFailure，同迈从）。
    /// </summary>
    protected override OfflinePolicy Offline { get; } = new()
    {
        Silence = TimeSpan.Zero,
        CountPollFailures = false,
    };

    /// <summary>
    /// 只认那条双向 64 字节的厂商通道（本机实测为 mi_01&amp;col04）：VID + 厂商页 + 64/64。
    /// 用长度而不是 usage 取值当判据，是为了不依赖具体型号描述符里的数字。
    /// </summary>
    protected override bool IsCandidate(HidDeviceInfo info)
    {
        if (info.VendorId != VgnVid) return false;
        bool vendor = info.UsagePage >= 0xFF00 || info.Usage >= 0xFF00;
        return vendor && info.InputReportLength == 64 && info.OutputReportLength == 64;
    }

    /// <summary>探测 = 发一条电量查询，1 秒窗口内等应答；收不到 = 键盘休眠/关机，进冷却。</summary>
    protected override (string Protocol, int? Percent, ChargingState Charging)? Probe(HidDeviceHandle handle)
    {
        var read = Query(handle, TimeSpan.FromSeconds(1), dev: null);
        return read == null ? null : (ProtocolName, read.Value.Percent, read.Value.Charging);
    }

    /// <summary>轮询 = 发一条电量查询读电量；写入被开关/熔断挡住时不发帧，也就不计失败。</summary>
    protected override (int? Percent, ChargingState Charging)? PollDevice(TrackedDevice dev)
    {
        if (!CanWrite(dev)) return null;

        var read = Query(dev.Handle, TimeSpan.FromSeconds(1), dev);
        if (read == null)
        {
            ReportPollFailure(dev);
            return null;
        }

        ReportPollAnswered(dev);
        return (read.Value.Percent, read.Value.Charging);
    }

    /// <summary>协议名（电量通道本身）。</summary>
    private const string ProtocolName = "weisheng-0x1a";

    /// <summary>
    /// 发查询帧并在窗口内收应答，解析成功即返回。返回 null = 窗口内没读到可识别应答。
    /// <paramref name="dev"/> 只用于日志去重（探测阶段为 null）。
    /// </summary>
    private static (int Percent, ChargingState Charging)? Query(
        HidDeviceHandle handle, TimeSpan window, TrackedDevice? dev)
    {
        var frame = VgnCodec.BuildQuery(handle.Info.OutputReportLength);
        if (!handle.WriteOutput(frame, TimeSpan.FromMilliseconds(500)))
        {
            Log.Info($"VGN 查询写失败: {handle.Info.DevicePath}");
            return null;
        }

        var deadline = DateTime.UtcNow + window;
        while (DateTime.UtcNow < deadline)
        {
            var input = handle.ReadInput(TimeSpan.FromMilliseconds(300));
            if (input == null) continue;
            if (!VgnCodec.TryParse(input, out int percent, out bool charging)) continue;

            // 轮询每分钟一条，逐条 Info 会淹没日志（CR-28）；只在读数变化时记一行。
            string line = $"VGN 电量: {percent}% 充电={charging} 原始={Log.Hex(input)}";
            if (dev == null) Log.Info(line);
            else Log.InfoOnChange($"vgn-battery:{dev.Status.Id}", $"{percent}|{charging}", line);

            return (percent, ChargingStateMap.FromChargingFlag(charging, percent));
        }

        return null;
    }

    protected override string BuildDisplayName(HidDeviceInfo info)
        => string.IsNullOrWhiteSpace(info.Product) ? $"VGN ({info.VidPid})" : $"VGN {info.Product}";
}
