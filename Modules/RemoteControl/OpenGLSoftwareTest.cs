using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Desktop;
using System;

namespace KLC_Finch
{
    public class OpenGLSoftwareTest : GameWindow
    {

        public string Version { get; private set; }

        public OpenGLSoftwareTest(int width, int height, string title) : base(GameWindowSettings.Default, NativeWindowSettings.Default)
        {
            Run();
        }

        protected override void OnLoad()
        {
            base.OnLoad();
            Version = GL.GetString(StringName.Version);
            Close();
        }

    }
}
