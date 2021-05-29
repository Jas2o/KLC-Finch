using System;
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

        public int ID; //RGB or Y
        public int IDu, IDv;
        public bool IsNew;
        public bool IsYUV;
        public byte[] Data; //RGB or Y
        public byte[] DataU, DataV;

        private Rectangle rect; //Kinda silly we have this twice
        private int width, height;
        private int stride; //YUV only

        private int VBOScreen;
        private Vector2[] vertBufferScreen;

        /// <summary>
        /// Only call this from GL Render
        /// </summary>
        public TextureScreen(bool isYUV=false) {
            IsYUV = isYUV;
            ID = GL.GenTexture();
            if(IsYUV) {
                IDu = GL.GenTexture();
                IDv = GL.GenTexture();
            }
        }

        public void Load(Rectangle rect, Bitmap decomp) {
            this.rect = rect;

            width = decomp.Width;
            height = decomp.Height;
            stride = 0;
            BitmapData data = decomp.LockBits(new System.Drawing.Rectangle(0, 0, decomp.Width, decomp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            if (this.Data == null || this.Data.Length != Math.Abs(data.Stride * data.Height))
                this.Data = new byte[Math.Abs(data.Stride * data.Height)];
            byte[] Data = new byte[Math.Abs(data.Stride * data.Height)];
            Marshal.Copy(data.Scan0, Data, 0, Data.Length); //This can fail with re-taking over private remote control
            this.Data = Data; //Seems more stable replacing the array rather than writing into it

            decomp.UnlockBits(data);

            IsNew = true;
        }

        public void LoadRaw(Rectangle rect, byte[] buffer, int stride) {
            IsYUV = true;
            this.rect = rect;

            width = rect.Width;
            height = rect.Height;
            this.stride = stride;

            if (Data == null || Data.Length != stride * height) {
                //virtualRequireViewportUpdate = true;
                //Console.WriteLine("[LoadTexture:Legacy] Array needs to be resized");

                Data = new byte[stride * height];
                DataU = new byte[stride * height / 4];
                DataV = new byte[stride * height / 4];
            }

            int pos = 0;
            System.Buffer.BlockCopy(buffer, pos, Data, 0, Data.Length);
            pos += Data.Length;
            System.Buffer.BlockCopy(buffer, pos, DataU, 0, DataU.Length);
            pos += DataU.Length;
            System.Buffer.BlockCopy(buffer, pos, DataV, 0, DataV.Length);

            IsNew = true;
        }

        public void RenderNew(int[] m_shader_sampler) {
            if (!IsNew || ID == -1 || rect == null)
                return;

            if (IsYUV) {
                //Y
                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, ID);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.Uniform1(m_shader_sampler[0], 1);

                //U
                GL.ActiveTexture(TextureUnit.Texture2);
                GL.BindTexture(TextureTarget.Texture2D, IDu);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.Uniform1(m_shader_sampler[1], 2);

                //V
                GL.ActiveTexture(TextureUnit.Texture3);
                GL.BindTexture(TextureTarget.Texture2D, IDv);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.Uniform1(m_shader_sampler[2], 3);
            } else {
                GL.ActiveTexture(TextureUnit.Texture0);
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
            }

            IsNew = false;
        }

        public bool Render(int programYUV) {
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

            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);

            if (IsYUV) {
                return false;
                GL.UseProgram(programYUV);

                float[] texcoords = {
                    0.0f, 1.0f,
                    1.0f, 1.0f,
                    1.0f, 0.0f,
                    0.0f, 0.0f
                };

                GL.EnableClientState(ArrayCap.VertexArray);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);

                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);

                GL.ActiveTexture(TextureUnit.Texture1);
                GL.EnableClientState(ArrayCap.TextureCoordArray);
                //GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, texcoords);
                //GL.BindTexture(TextureTarget.Texture2D, textureIDs[0]);
                //GL.Uniform1(m_shader_sampler[0], 1);

                GL.ActiveTexture(TextureUnit.Texture2);
                GL.EnableClientState(ArrayCap.TextureCoordArray);
                //GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, texcoords);
                //GL.BindTexture(TextureTarget.Texture2D, textureIDs[1]);
                //GL.Uniform1(m_shader_sampler[1], 2);

                GL.ActiveTexture(TextureUnit.Texture3);
                GL.EnableClientState(ArrayCap.TextureCoordArray);
                //GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, texcoords);
                //GL.BindTexture(TextureTarget.Texture2D, textureIDs[2]);
                //GL.Uniform1(m_shader_sampler[1], 3);
            } else {
                GL.UseProgram(0);

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, ID);
            }

            GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
            GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
            GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferScreen.Length / 2);

            return true;
        }

    }
}
