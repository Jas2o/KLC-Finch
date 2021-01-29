using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace NTR {
    public class QueueTexture /*: IDisposable(*/ {
        //private bool disposed = false;

        public int width;
        public int height;
        public byte[] data;

        public QueueTexture(int width, int height, Bitmap decomp) {
            this.width = width;
            this.height = height;

            BitmapData data = decomp.LockBits(new System.Drawing.Rectangle(0, 0, decomp.Width, decomp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            this.data = new byte[Math.Abs(data.Stride * data.Height)];
            Marshal.Copy(data.Scan0, this.data, 0, this.data.Length);
            decomp.UnlockBits(data);
        }

        /*
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposed)
                return;

            if (disposing) {
                data = null;
            }

            disposed = true;
        }
        */
    }
}