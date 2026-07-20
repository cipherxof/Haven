namespace Avalonia3DControl.UI
{
    /// <summary>
    /// 梯度条配置类，包含所有配置相关的属性
    /// </summary>
    public class GradientBarConfiguration
    {
        #region 显示配置
        /// <summary>
        /// 是否显示梯度条
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 梯度条位置（左侧或右侧）
        /// </summary>
        public GradientBarPosition Position { get; set; } = GradientBarPosition.Right;
        #endregion

        #region 尺寸配置
        /// <summary>
        /// 梯度条宽度（NDC坐标系）
        /// </summary>
        public float Width { get; set; } = 0.05f;

        /// <summary>
        /// 梯度条高度（相对于窗口高度的比例）
        /// </summary>
        public float Height { get; set; } = 1.4f;

        /// <summary>
        /// 梯度条距离边缘的偏移（相对于窗口宽度的比例）
        /// </summary>
        public float EdgeOffset { get; set; } = 0.02f;
        #endregion

        #region 梯度配置
        /// <summary>
        /// 当前颜色梯度类型
        /// </summary>
        public ColorGradientType GradientType { get; set; } = ColorGradientType.Classic;

        /// <summary>
        /// 最小值
        /// </summary>
        public float MinValue { get; set; } = -1.0f;

        /// <summary>
        /// 最大值
        /// </summary>
        public float MaxValue { get; set; } = 1.0f;
        #endregion

        #region 刻度配置
        /// <summary>
        /// 是否使用归一化刻度（-1~1）；false 则显示实际 Min~Max
        /// </summary>
        public bool UseNormalizedScale { get; set; } = true;

        /// <summary>
        /// 是否显示刻度
        /// </summary>
        public bool ShowTicks { get; set; } = true;

        /// <summary>
        /// 刻度数量（包含两端），建议为奇数，如5或7
        /// </summary>
        public int TickCount { get; set; } = 5;
        #endregion

        #region 配置验证
        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <returns>配置是否有效</returns>
        public bool IsValid()
        {
            return Width > 0 && Height > 0 && EdgeOffset >= 0 && 
                   TickCount >= 2 && MinValue < MaxValue;
        }

        /// <summary>
        /// 获取配置的副本
        /// </summary>
        /// <returns>配置副本</returns>
        public GradientBarConfiguration Clone()
        {
            return new GradientBarConfiguration
            {
                IsVisible = IsVisible,
                Position = Position,
                Width = Width,
                Height = Height,
                EdgeOffset = EdgeOffset,
                GradientType = GradientType,
                MinValue = MinValue,
                MaxValue = MaxValue,
                UseNormalizedScale = UseNormalizedScale,
                ShowTicks = ShowTicks,
                TickCount = TickCount
            };
        }
        #endregion
    }
}
