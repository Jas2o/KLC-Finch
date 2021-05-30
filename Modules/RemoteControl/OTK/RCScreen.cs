using System.Drawing;

namespace NTR {
    internal class RCScreen {
        public string screen_id;
        public string screen_name;
        //public int screen_height;
        //public int screen_width;
        //public int screen_x;
        //public int screen_y;

        public Rectangle rect;

        public TextureScreen Texture;

        public RCScreen(string screen_id, string screen_name, int screen_height, int screen_width, int screen_x, int screen_y) {
            this.screen_id = screen_id;
            this.screen_name = screen_name;
            //this.screen_height = screen_height;
            //this.screen_width = screen_width;
            //this.screen_x = screen_x;
            //this.screen_y = screen_y;

            rect = new Rectangle(screen_x, screen_y, screen_width, screen_height);
        }

        public string StringResPos() {
            return rect.Width + " x " + rect.Height + " at " + rect.X + ", " + rect.Y;
        }

        public override string ToString() {
            return screen_name + ": (" + StringResPos() + ")";
        }
    }
}