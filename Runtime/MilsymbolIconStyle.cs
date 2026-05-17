using System;

namespace Leuconoe.MilsymbolUnity
{
    [Serializable]
    public sealed class MilsymbolIconStyle
    {
        public int size = 100;
        public bool frame = true;
        public bool fill = true;
        public bool square;
        public bool alternateMedal;
        public bool civilianColor = true;
        public float fillOpacity = 1f;
        public float strokeWidth = 4f;
        public float outlineWidth;
        public string colorMode = "Light";
        public string monoColor = "";
        public string fillColor = "";
        public string frameColor = "";
        public string iconColor = "";
        public string outlineColor = "rgb(239, 239, 239)";
    }
}
