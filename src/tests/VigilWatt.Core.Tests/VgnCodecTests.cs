using VigilWatt.Core.Plugins.Vgn;
using Xunit;

namespace VigilWatt.Core.Tests;

/// <summary>
/// 功 能： VgnCodec —— 维盛 2.4G 电量帧的构造与应答解析。
/// 2026/10/19 11:45:00 maluy
/// V2 用例帧取自真机实测（本机 VGN S99 接收器 320F:5088，读回 82%）。
/// </summary>
public class VgnCodecTests
{
    /// <summary>真机应答：04 00 00 1A 06 00 00 00 52 ...（电量 0x52 = 82%、未充电）。</summary>
    private static readonly byte[] RealReply = BuildFrame(percent: 0x52, charging: 0x00);

    /// <summary>按真机布局造一帧，便于逐字节改出边界用例。</summary>
    private static byte[] BuildFrame(byte percent, byte charging)
    {
        var frame = new byte[VgnCodec.FrameLength];
        frame[0] = VgnCodec.ReportId;
        frame[3] = 0x1A;
        frame[4] = 0x06;
        frame[8] = percent;
        frame[9] = charging;
        return frame;
    }

    private static byte[] WithByte(byte[] frame, int index, byte value)
    {
        var copy = (byte[])frame.Clone();
        copy[index] = value;
        return copy;
    }

    [Fact]
    public void V1_查询帧按协议构造()
    {
        var frame = VgnCodec.BuildQuery();
        Assert.Equal(VgnCodec.FrameLength, frame.Length);
        Assert.Equal(0x04, frame[0]);   // report ID
        Assert.Equal(0x00, frame[1]);
        Assert.Equal(0x00, frame[2]);
        Assert.Equal(0x1A, frame[3]);   // 命令标识
        Assert.Equal(0x06, frame[4]);
        Assert.All(frame[5..], b => Assert.Equal(0x00, b));   // 其余补 0
    }

    [Fact]
    public void V2_真机应答解析出82且未充电()
    {
        Assert.True(VgnCodec.TryParse(RealReply, out int percent, out bool charging));
        Assert.Equal(82, percent);
        Assert.False(charging);
    }

    [Fact]
    public void V3_非0x1A帧不是电量应答()
    {
        var other = WithByte(RealReply, 3, 0x1B);
        Assert.False(VgnCodec.TryParse(other, out _, out _));
    }

    [Fact]
    public void V4_电量越界判为不可信()
    {
        var over = WithByte(RealReply, 8, 101);
        Assert.False(VgnCodec.TryParse(over, out _, out _));
    }

    [Fact]
    public void V5_充电标志非0表示充电中()
    {
        var charging = WithByte(RealReply, 9, 0x01);
        Assert.True(VgnCodec.TryParse(charging, out int percent, out bool isCharging));
        Assert.Equal(82, percent);
        Assert.True(isCharging);
    }

    [Fact]
    public void V6_长度不足不算应答()
    {
        var shortFrame = new byte[9];
        shortFrame[3] = 0x1A;
        Assert.False(VgnCodec.TryParse(shortFrame, out _, out _));
    }
}
