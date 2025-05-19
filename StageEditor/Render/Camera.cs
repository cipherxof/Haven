using OpenTK;
using System;

namespace Haven.Render
{

    public class Camera
    {
        public static Camera MainCamera;

        private Vector3d _front = -Vector3d.UnitZ;

        private Vector3d _up = Vector3d.UnitY;

        private Vector3d _right = Vector3d.UnitX;

        private float _pitch;

        private float _yaw = -MathHelper.PiOver2;

        private float _fov = MathHelper.PiOver2;

        public Camera(Vector3d position, float aspectRatio)
        {
            MainCamera = this;
            Position = position;
            AspectRatio = aspectRatio;
        }

        public Vector3d Position { get; set; }

        public float AspectRatio { private get; set; }

        public Vector3d Front => _front;

        public Vector3d Up => _up;

        public Vector3d Right => _right;

        public float Pitch
        {
            get => MathHelper.RadiansToDegrees(_pitch);
            set
            {

                var angle = MathHelper.Clamp(value, -89f, 89f);
                _pitch = MathHelper.DegreesToRadians(angle);
                UpdateVectors();
            }
        }

        public float Yaw
        {
            get => MathHelper.RadiansToDegrees(_yaw);
            set
            {
                _yaw = MathHelper.DegreesToRadians(value);
                UpdateVectors();
            }
        }

        public float Fov
        {
            get => MathHelper.RadiansToDegrees(_fov);
            set
            {
                var angle = MathHelper.Clamp(value, 1f, 90f);
                _fov = MathHelper.DegreesToRadians(angle);
            }
        }

        public Matrix4d GetViewMatrix()
        {

            return Matrix4d.LookAt(Position, Position + _front, _up);
        }

        public Matrix4d GetProjectionMatrix()
        {
            return Matrix4d.CreatePerspectiveFieldOfView(_fov, AspectRatio, 500f, 200000f);
        }

        private void UpdateVectors()
        {

            _front.X = MathF.Cos(_pitch) * MathF.Cos(_yaw);
            _front.Y = MathF.Sin(_pitch);
            _front.Z = MathF.Cos(_pitch) * MathF.Sin(_yaw);

            _front = Vector3d.Normalize(_front);

            _right = Vector3d.Normalize(Vector3d.Cross(_front, Vector3d.UnitY));
            _up = Vector3d.Normalize(Vector3d.Cross(_right, _front));
        }

        public double GetDistanceTo(Vector3d point)
        {
            return (this.Position - point).Length;
        }
    }
}