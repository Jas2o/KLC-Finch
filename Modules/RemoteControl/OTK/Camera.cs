using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Drawing;

//Source: OpenEngine

namespace NTR {

    public class Camera {

        public Vector2 Position { get; set; } = Vector2.Zero;
        public float Rotation { get; set; }
        public Vector2 Scale { get; set; } = Vector2.One;
        public Color SkyboxColor { get; set; } = Color.DimGray;
        public float ZFar { get; set; }
        public float ZNear { get; set; }

        public Camera(Vector2 postion, float scale = 1.0f, float rotation = 0, float zNear = 0, float zFar = 1) {
            this.Position = postion;
            this.Scale = new Vector2(scale);
            this.Rotation = rotation;
            this.ZNear = zNear;
            this.ZFar = zFar;
        }

        public void Move(Vector2 vec) {
            this.Position += vec;
        }

        public Vector2 ScreenToWorldCoordinates(Vector2 rawInput, int offsetX = 0, int offsetY = 0) {
            rawInput.X += offsetX;
            rawInput.Y += offsetY;

            rawInput /= this.Scale.X;

            var dX = new Vector2(
                (float)Math.Cos(this.Rotation),
                (float)Math.Sin(this.Rotation));

            var dY = new Vector2(
                (float)Math.Cos(this.Rotation + MathHelper.PiOver2),
                (float)Math.Sin(this.Rotation + MathHelper.PiOver2));

            return this.Position + dX * rawInput.X + dY * rawInput.Y;
        }
        internal void ApplyTransform() {
            var transform = Matrix4.Identity;

            // Position
            transform = Matrix4.Mult(
                transform,
                Matrix4.CreateTranslation(
                    -this.Position.X,
                    -this.Position.Y,
                    0));

            // Rotation
            transform = Matrix4.Mult(
                transform,
                Matrix4.CreateRotationZ(-this.Rotation));

            // Scale
            transform = Matrix4.Mult(
                transform,
                Matrix4.CreateScale(
                    this.Scale.X,
                    this.Scale.Y,
                    1));

            GL.MultMatrix(ref transform);
        }
    }
}