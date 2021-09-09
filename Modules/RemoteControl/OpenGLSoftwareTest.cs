using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System;

namespace KLC_Finch {
    public class OpenGLSoftwareTest : GameWindow {

        public string Version { get; private set; }

        public OpenGLSoftwareTest(int width, int height, string title) : base(width, height, OpenTK.Graphics.GraphicsMode.Default, title) {
            Run(0);
        }

        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);
            Version = GL.GetString(StringName.Version);
            Close();
        }

    }
}
