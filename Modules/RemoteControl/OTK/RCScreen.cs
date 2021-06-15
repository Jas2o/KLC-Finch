using System;
using System.Drawing;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace NTR {
    internal class RCScreen {
        public string screen_id;
        public string screen_name;
        //public int screen_height;
        //public int screen_width;
        //public int screen_x;
        //public int screen_y;

        public Rectangle rect;
        public Rectangle rectFixed;

        public TextureScreen Texture;
        //public System.Windows.Shapes.Rectangle Shape;
        public System.Windows.Controls.Image CanvasImage;

        public RCScreen(string screen_id, string screen_name, int screen_height, int screen_width, int screen_x, int screen_y) {
            this.screen_id = screen_id;
            this.screen_name = screen_name;
            //this.screen_height = screen_height;
            //this.screen_width = screen_width;
            //this.screen_x = screen_x;
            //this.screen_y = screen_y;

            rect = new Rectangle(screen_x, screen_y, screen_width, screen_height);

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
        }

        public string StringResPos() {
            return rect.Width + " x " + rect.Height + " at " + rect.X + ", " + rect.Y;
        }

        public override string ToString() {
            return screen_name + ": (" + StringResPos() + ")";
        }

        public void SetCanvasImage(Bitmap bitmap) {
            //High CPU

            IntPtr hBitmap = bitmap.GetHbitmap();
            try {
                CanvasImage.Source = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            } finally {
                DeleteObject(hBitmap);
            }

            //bitmap.Dispose(); //Causes issues?
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}