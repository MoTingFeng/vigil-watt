namespace VigilWatt.App.Services;

/// <summary>贴片在任务栏里的锚点：Left = 任务栏方向的起始端，Center = 居中，Tray = 紧贴系统托盘左侧。</summary>
public enum PillAnchor
{
    Left,
    Center,
    Tray,
}

/// <summary>
/// 功 能： 任务栏电量贴片的**位置锚点**：取值定义 + 收敛逻辑。
/// 2026/10/19 15:20:00 maluy
/// 拖动出来的 offset 是叠加在锚点之上的微调；换锚点必须清掉它，否则"选靠左"还会被旧偏移推走。
/// </summary>
public static class TaskbarPillAnchor
{
    public const string Left = "Left";

    public const string Center = "Center";

    public const string Tray = "Tray";

    /// <summary>把任意输入（含 null / 手改坏了的配置）收敛成三个合法取值之一；未知按 Tray（改造前的行为）。</summary>
    public static string Normalize(string? value) => value switch
    {
        Left => Left,
        Center => Center,
        _ => Tray,
    };

    /// <summary>收敛后转成定位数学用的枚举（<see cref="TaskbarLayout.Place"/> 的入参）。</summary>
    public static PillAnchor Parse(string? value) => Normalize(value) switch
    {
        Left => PillAnchor.Left,
        Center => PillAnchor.Center,
        _ => PillAnchor.Tray,
    };

    /// <summary>设置页副标题用的一句话说明。竖向任务栏时"靠左"即靠上端（沿任务栏方向的起始端）。</summary>
    public static string Describe(string? value) => Normalize(value) switch
    {
        Left => "任务栏起始端（横向任务栏就是最左边，开始按钮那一侧）",
        Center => "任务栏正中",
        _ => "紧贴系统托盘左侧（默认）",
    };
}
