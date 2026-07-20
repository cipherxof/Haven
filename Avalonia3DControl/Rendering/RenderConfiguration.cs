using OpenTK.Mathematics;

namespace Avalonia3DControl.Rendering
{
    /// <summary>
    /// 渲染配置类，集中管理所有渲染相关的常量和配置
    /// </summary>
    public static class RenderConfiguration
    {
        #region 相机配置
        /// <summary>
        /// 旋转敏感度
        /// </summary>
        public const float ROTATION_SENSITIVITY = 0.01f;

        /// <summary>
        /// 平移敏感度
        /// </summary>
        public const float TRANSLATION_SENSITIVITY = 0.005f;

        /// <summary>
        /// 缩放敏感度
        /// </summary>
        public const float ZOOM_SENSITIVITY = 0.3f;

        /// <summary>
        /// 缩放平滑系数
        /// </summary>
        public const float ZOOM_SMOOTHING = 0.25f;

        /// <summary>
        /// 最小缩放值
        /// </summary>
        public const float MIN_ZOOM = 0.2f;

        /// <summary>
        /// 最大缩放值
        /// </summary>
        public const float MAX_ZOOM = 10.0f;

        /// <summary>
        /// 相机距离
        /// </summary>
        public const float CAMERA_DISTANCE = 10.0f;

        /// <summary>
        /// 旋转限制偏移
        /// </summary>
        public const float ROTATION_LIMIT_OFFSET = 0.1f;

        /// <summary>
        /// 默认视野角度（度）
        /// </summary>
        public const float DEFAULT_FIELD_OF_VIEW = 45.0f;

        /// <summary>
        /// 默认近平面距离
        /// </summary>
        public const float DEFAULT_NEAR_PLANE = 0.1f;

        /// <summary>
        /// 默认远平面距离
        /// </summary>
        public const float DEFAULT_FAR_PLANE = 100.0f;
        #endregion

        #region 正交投影配置
        /// <summary>
        /// 默认正交投影大小
        /// </summary>
        public const float DEFAULT_ORTHOGRAPHIC_SIZE = 5.0f;

        /// <summary>
        /// 最小正交投影大小
        /// </summary>
        public const float MIN_ORTHOGRAPHIC_SIZE = 0.5f;

        /// <summary>
        /// 最大正交投影大小
        /// </summary>
        public const float MAX_ORTHOGRAPHIC_SIZE = 20.0f;
        #endregion

        #region 渲染配置
        /// <summary>
        /// 默认背景色
        /// </summary>
        public static readonly Vector3 DEFAULT_BACKGROUND_COLOR = new Vector3(0.1f, 0.1f, 0.2f);

        /// <summary>
        /// 默认点大小
        /// </summary>
        public const float DEFAULT_POINT_SIZE = 5.0f;

        /// <summary>
        /// 默认线宽
        /// </summary>
        public const float DEFAULT_LINE_WIDTH = 1.0f;

        /// <summary>
        /// 动画平滑阈值
        /// </summary>
        public const float ANIMATION_SMOOTH_THRESHOLD = 0.001f;

        /// <summary>
        /// 动画完成阈值
        /// </summary>
        public const float ANIMATION_COMPLETE_THRESHOLD = 0.01f;
        #endregion

        #region 视口配置
        /// <summary>
        /// 最小视口宽度
        /// </summary>
        public const int MIN_VIEWPORT_WIDTH = 1;

        /// <summary>
        /// 最小视口高度
        /// </summary>
        public const int MIN_VIEWPORT_HEIGHT = 1;
        #endregion

        #region 材质配置
        /// <summary>
        /// 默认材质透明度
        /// </summary>
        public const float DEFAULT_MATERIAL_ALPHA = 1.0f;

        /// <summary>
        /// 坐标轴透明度（始终不透明）
        /// </summary>
        public const float AXES_ALPHA = 1.0f;
        #endregion

        #region 性能配置
        /// <summary>
        /// 最大渲染帧率（FPS）
        /// </summary>
        public const int MAX_RENDER_FPS = 60;

        /// <summary>
        /// 渲染超时时间（毫秒）
        /// </summary>
        public const int RENDER_TIMEOUT_MS = 16; // ~60 FPS
        #endregion
    }
}