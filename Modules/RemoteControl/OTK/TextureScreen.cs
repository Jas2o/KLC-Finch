using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NTR {

    public class TextureScreen {
        private readonly object _lock = new object();
        public byte[] Data;
        public byte[] DataU, DataV;
        public KLC_Finch.DecodeMode DecodeMode;
        public int ID; //RGB or Y
        public int IDu, IDv;
        private bool IsNew;
        private Rectangle rect; //Kinda silly we have this twice
        private int stride;
        private int VBOScreen;
        private bool vertBufferNeedUpdate;
        private Vector2[] vertBufferScreen;
        private int width, height;

        //public bool IsRetina { get; private set; }

        /// <summary>
        /// Only call this from GL Render
        /// </summary>
        public TextureScreen(KLC_Finch.DecodeMode decodeMode) {
            DecodeMode = decodeMode;
            ID = GL.GenTexture();
            if (DecodeMode == KLC_Finch.DecodeMode.RawYUV) {
                IDu = GL.GenTexture();
                IDv = GL.GenTexture();
            }
        }

        public void Load(Rectangle rect, Bitmap decomp) {
            lock (_lock) {
                if (this.rect.Width != rect.Width || this.rect.Height != rect.Height || this.rect.X != rect.X || this.rect.Y != rect.Y) {
                    this.rect = rect;
                    if (width == this.rect.Width * 2) {
                        this.rect.Width = width;
                        this.rect.Height = height;
                    }

                    vertBufferNeedUpdate = true;
                }

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
        }

        public void LoadRaw(Rectangle rect, int width, int height, int stride, byte[] buffer) {
            lock (_lock) {
                if (this.rect.Width != rect.Width || this.rect.Height != rect.Height || this.rect.X != rect.X || this.rect.Y != rect.Y) {
                    this.rect = rect;
                    if (width == this.rect.Width * 2) {
                        this.rect.Width = width;
                        this.rect.Height = height;
                    }
                    vertBufferNeedUpdate = true;
                }

                this.width = width;
                this.height = height;
                this.stride = stride;

                if (Data == null || Data.Length != stride * height) {
                    //virtualRequireViewportUpdate = true;
                    //Console.WriteLine("[LoadTexture:Legacy] Array needs to be resized");

                    Data = new byte[stride * height];
                }

                int pos = 0;
                System.Buffer.BlockCopy(buffer, pos, Data, 0, Data.Length);

                if (DecodeMode == KLC_Finch.DecodeMode.RawYUV) {
                    int len = stride * height / 4;
                    if (DataU == null || DataU.Length != len) {
                        DataU = new byte[len];
                        DataV = new byte[len];
                    }

                    pos += Data.Length;
                    System.Buffer.BlockCopy(buffer, pos, DataU, 0, DataU.Length);
                    pos += DataU.Length;
                    System.Buffer.BlockCopy(buffer, pos, DataV, 0, DataV.Length);
                }

                IsNew = true;
            }
        }

        public bool Render(int programYUV, int[] m_shader_sampler, int m_shader_multiplyColor = 0, Color? multiplyColor = null) {
            if (ID == -1 || Data == null || rect == null)
                return false;

            if (vertBufferNeedUpdate) {
                vertBufferNeedUpdate = false;
                if (vertBufferScreen == null)
                    VBOScreen = GL.GenBuffer();

                vertBufferScreen = new Vector2[8] {
                    new Vector2(rect.Left, rect.Bottom), new Vector2(0, 1),
                    new Vector2(rect.Right, rect.Bottom), new Vector2(1, 1),
                    new Vector2(rect.Right, rect.Top), new Vector2(1, 0),
                    new Vector2(rect.Left, rect.Top), new Vector2(0, 0)
                };
            }

            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferScreen.Length), vertBufferScreen, BufferUsageHint.StaticDraw);

            GL.Enable(EnableCap.Texture2D);

            if (DecodeMode == KLC_Finch.DecodeMode.BitmapRGB) {
                GL.UseProgram(0);

                GL.Color3(multiplyColor ?? Color.White);

                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, ID);
            } else {
                if (DecodeMode == KLC_Finch.DecodeMode.RawYUV) {
                    GL.UseProgram(programYUV);
                }

                GL.EnableClientState(ArrayCap.VertexArray);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);

                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);

                GL.ActiveTexture(TextureUnit.Texture1);
                GL.EnableClientState(ArrayCap.TextureCoordArray);
                GL.BindTexture(TextureTarget.Texture2D, ID);
                if (DecodeMode == KLC_Finch.DecodeMode.RawYUV) {
                    //Y
                    GL.Uniform1(m_shader_sampler[0], 1);

                    //U
                    GL.ActiveTexture(TextureUnit.Texture2);
                    GL.EnableClientState(ArrayCap.TextureCoordArray);
                    //GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, texcoords);
                    GL.BindTexture(TextureTarget.Texture2D, IDu);
                    GL.Uniform1(m_shader_sampler[1], 2);

                    //V
                    GL.ActiveTexture(TextureUnit.Texture3);
                    GL.EnableClientState(ArrayCap.TextureCoordArray);
                    //GL.TexCoordPointer(2, TexCoordPointerType.Float, 0, texcoords);
                    GL.BindTexture(TextureTarget.Texture2D, IDv);
                    GL.Uniform1(m_shader_sampler[2], 3);

                    //Overlay
                    Color color = multiplyColor ?? Color.White;
                    GL.Uniform3(m_shader_multiplyColor, new Vector3(color.R / 255f, color.G / 255f, color.B / 255f));
                }
            }

            GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
            GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
            GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferScreen.Length / 2);

            if (DecodeMode != KLC_Finch.DecodeMode.BitmapRGB)
                GL.ActiveTexture(TextureUnit.Texture0);

            return true;
        }

        public void RenderNew(int[] m_shader_sampler) {
            if (!IsNew || ID == -1 || rect == null)
                return;

            if (DecodeMode == KLC_Finch.DecodeMode.BitmapRGB) {
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
            } else {
                GL.PixelStore(PixelStoreParameter.UnpackRowLength, stride);

                //Y
                GL.ActiveTexture(TextureUnit.Texture1);
                GL.BindTexture(TextureTarget.Texture2D, ID);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.One, width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Luminance, PixelType.UnsignedByte, Data);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                if (DecodeMode == KLC_Finch.DecodeMode.RawYUV) {
                    //Y
                    GL.Uniform1(m_shader_sampler[0], 1);

                    GL.PixelStore(PixelStoreParameter.UnpackRowLength, stride / 2);

                    //U
                    GL.ActiveTexture(TextureUnit.Texture2);
                    GL.BindTexture(TextureTarget.Texture2D, IDu);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.One, width / 2, height / 2, 0, OpenTK.Graphics.OpenGL.PixelFormat.Luminance, PixelType.UnsignedByte, DataU);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.Uniform1(m_shader_sampler[1], 2);

                    //V
                    GL.ActiveTexture(TextureUnit.Texture3);
                    GL.BindTexture(TextureTarget.Texture2D, IDv);
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.One, width / 2, height / 2, 0, OpenTK.Graphics.OpenGL.PixelFormat.Luminance, PixelType.UnsignedByte, DataV);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    GL.Uniform1(m_shader_sampler[2], 3);
                }

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0); //Reset stride
            }

            IsNew = false;
        }
    }
}