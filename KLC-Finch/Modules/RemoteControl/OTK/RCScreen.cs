namespace NTR {
    internal class RCScreen {
        public int screen_id;
        public string screen_name;
        public int screen_height;
        public int screen_width;
        public int screen_x;
        public int screen_y;

        public RCScreen(int screen_id, string screen_name, int screen_height, int screen_width, int screen_x, int screen_y) {
            this.screen_id = screen_id;
            this.screen_name = screen_name;
            this.screen_height = screen_height;
            this.screen_width = screen_width;
            this.screen_x = screen_x;
            this.screen_y = screen_y;
        }
    }
}