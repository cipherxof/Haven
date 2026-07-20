using OpenTK.Mathematics;

namespace Avalonia3DControl.Core.Lighting
{
    /// <summary>
    /// 光源基类
    /// </summary>
    public abstract class Light
    {
        public Vector3 Color { get; set; }
        public float Intensity { get; set; }
        public bool Enabled { get; set; }

        protected Light()
        {
            Color = Vector3.One;
            Intensity = 1.0f;
            Enabled = true;
        }
    }

    /// <summary>
    /// 方向光
    /// </summary>
    public class DirectionalLight : Light
    {
        public Vector3 Direction { get; set; }

        public DirectionalLight()
        {
            Direction = new Vector3(0.0f, -1.0f, 0.0f);
        }
    }

    /// <summary>
    /// 点光源
    /// </summary>
    public class PointLight : Light
    {
        public Vector3 Position { get; set; }
        public float Range { get; set; }
        public Vector3 Attenuation { get; set; } // x: constant, y: linear, z: quadratic

        public PointLight()
        {
            Position = Vector3.Zero;
            Range = 10.0f;
            Attenuation = new Vector3(1.0f, 0.09f, 0.032f);
        }
    }
}