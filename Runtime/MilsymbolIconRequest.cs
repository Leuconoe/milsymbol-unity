using System;

namespace Leuconoe.MilsymbolUnity
{
    [Serializable]
    public sealed class MilsymbolIconRequest
    {
        public string sidc = "";
        public MilsymbolStandard standard = MilsymbolStandard.Auto;
        public bool iconOnly = true;
        public MilsymbolIconStyle style = new MilsymbolIconStyle();
    }
}
