using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace NTR {
    public class RCScreen {
        public string screen_id;
        public string screen_name;
        //public int screen_height;
        //public int screen_width;
        //public int screen_x;
        //public int screen_y;

        public Rectangle rect;
        public Rectangle rectEdge;
        public Rectangle rectOrg { get; private set; }
        //public Rectangle rectFixed;

        public TextureScreen Texture;
        //public System.Windows.Shapes.Rectangle Shape;
        public System.Windows.Controls.Image CanvasImage;
        private static ColorPalette grayPalette;

        public RCScreen(string screen_id, string screen_name, int screen_height, int screen_width, int screen_x, int screen_y) {
            this.screen_id = screen_id;
            this.screen_name = screen_name;
            //this.screen_height = screen_height;
            //this.screen_width = screen_width;
            //this.screen_x = screen_x;
            //this.screen_y = screen_y;

            rect = new Rectangle(screen_x, screen_y, screen_width, screen_height);
            rectEdge = new Rectangle(screen_x - 10, screen_y - 10, screen_width + 20, screen_height + 20);
            rectOrg = new Rectangle(screen_x, screen_y, screen_width, screen_height);

            /*
            //--

            int multiple = 4;
            int fixedWidth = screen_width;
            int fixedHeight = screen_height;

            int rem = screen_width % multiple;
            int result = screen_width - rem;
            if (rem > 0)
                fixedWidth = result + multiple;

            rem = screen_height % multiple;
            result = screen_height - rem;
            if (rem > 0)
                fixedHeight = result + multiple;

            rectFixed = new Rectangle(screen_x, screen_y, fixedWidth, fixedHeight);
            */
        }

        public string StringResPos() {
            return rect.Width + " x " + rect.Height + " at " + rect.X + ", " + rect.Y;
        }

        public override string ToString() {
            return screen_name + ": (" + StringResPos() + ")";
        }

        public void SetCanvasImageBW(int width, int height, int stride, byte[] data) {
            Bitmap image = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

            if (grayPalette == null) {
                grayPalette = image.Palette;
                Color[] entries = grayPalette.Entries;
                for (int i = 0; i < 256; i++) {
                    entries[i] = Color.FromArgb((byte)i, (byte)i, (byte)i);
                }
            }
            image.Palette = grayPalette;

            BitmapData bmpData = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.WriteOnly, image.PixelFormat);
            for (int y = 0; y < height; y++)
                System.Runtime.InteropServices.Marshal.Copy(data, y * stride, bmpData.Scan0 + (y * width), width);
            image.UnlockBits(bmpData);

            SetCanvasImage(image);
        }

        public void SetCanvasFilled() {
            Bitmap bTest = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(bTest)) { g.Clear(Color.Gray); }
            SetCanvasImage(bTest);
            bTest.Dispose();
        }

        public void SetCanvasImage(Bitmap bitmap) {
            BitmapSource bs = null;

            using (MemoryStream ms = new MemoryStream()) {
                using (WrappingStream ws = new WrappingStream(ms)) {
                    bitmap.Save(ws, System.Drawing.Imaging.ImageFormat.Bmp);
                    bs = BitmapFrame.Create(ws, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }
            }

            /*
            //Old code that quickly lead to running out of memory.
            IntPtr ip = bitmap.GetHbitmap();
            BitmapSource bs = null;
            try {
                bs = Imaging.CreateBitmapSourceFromHBitmap(ip,
                   IntPtr.Zero, System.Windows.Int32Rect.Empty,
                   BitmapSizeOptions.FromEmptyOptions());
            } finally {
                DeleteObject(ip);
            }
            bs.Freeze();
            */

            CanvasImage.Source = bs;
        }

        //[System.Runtime.InteropServices.DllImport("gdi32.dll")]
        //private static extern bool DeleteObject(IntPtr hObject);
    }
}