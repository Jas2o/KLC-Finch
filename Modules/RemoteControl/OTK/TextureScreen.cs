﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace NTR {
    public class TextureScreen {

        public byte[] Data;
        public bool IsNew;
        public int ID;

        private Rectangle rect; //Kinda silly we have this twice
        private int width, height;

        private int VBOScreen;
        private Vector2[] vertBufferScreen;

        /// <summary>
        /// Only call this from GL Render
        /// </summary>
        public TextureScreen() {
            ID = GL.GenTexture();
        }

        public void Load(Rectangle rect, Bitmap decomp) {
            this.rect = rect;

            width = decomp.Width;
            height = decomp.Height;
            BitmapData data = decomp.LockBits(new System.Drawing.Rectangle(0, 0, decomp.Width, decomp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            if (this.Data == null || this.Data.Length != Math.Abs(data.Stride * data.Height))
                this.Data = new byte[Math.Abs(data.Stride * data.Height)];
            byte[] Data = new byte[Math.Abs(data.Stride * data.Height)];
            Marshal.Copy(data.Scan0, Data, 0, Data.Length); //This can fail with re-taking over private remote control
            this.Data = Data; //Seems more stable replacing the array rather than writing into it

            decomp.UnlockBits(data);

            IsNew = true;
        }

        public void RenderNew() {
            if (!IsNew || ID == -1 || rect == null)
                return;

            GL.BindTexture(TextureTarget.Texture2D, ID);

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0, //Level
                PixelInternalFormat.Rgb,
                width,
                height,
                0, //Border
                OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
                PixelType.UnsignedByte,
                Data); //bmpData.Scan0

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            IsNew = false;
        }

        public bool Render() {
            if (ID == -1 || Data == null || rect == null)
                return false;

            if(vertBufferScreen == null) {
                vertBufferScreen = new Vector2[8] {
                    new Vector2(rect.Left, rect.Bottom), new Vector2(0, 1),
                    new Vector2(rect.Right, rect.Bottom), new Vector2(1, 1),
                    new Vector2(rect.Right, rect.Top), new Vector2(1, 0),
                    new Vector2(rect.Left, rect.Top), new Vector2(0, 0)
                };

                VBOScreen = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);
                GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferScreen.Length), vertBufferScreen, BufferUsageHint.StaticDraw);
            }

            GL.Enable(EnableCap.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, ID);

            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);
            GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
            GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
            GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferScreen.Length / 2);

            return true;
        }

    }
}
