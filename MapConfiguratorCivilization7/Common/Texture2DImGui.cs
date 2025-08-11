using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapConfiguratorCivilization7.Common
{
    public class Texture2DImGui
    {
        public IntPtr ID;
        public Texture2D tex;
        public Texture2DImGui(Texture2D tex)
        {
            this.tex = tex;
            ID = GuiHandler.GuiRenderer.BindTexture(tex);
        }

        public static implicit operator Texture2D(Texture2DImGui t) => t.tex;
        public static implicit operator IntPtr(Texture2DImGui t) => t.ID;
    }
}
