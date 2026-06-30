using System;
using UnityEngine;

namespace Leuconoe.MilsymbolUnity
{
    [CreateAssetMenu(menuName = "Milsymbol/Icon Asset", fileName = "MilsymbolIcon")]
    public sealed class MilsymbolIconAsset : ScriptableObject
    {
        [SerializeField] private string sidc = "";
        [SerializeField] private string decodedSidc = "";
        [SerializeField] private MilsymbolStandard standard = MilsymbolStandard.Auto;
        [SerializeField] private MilsymbolIconStyle style = new MilsymbolIconStyle();
        [SerializeField] private Texture2D texture;
        [SerializeField] private bool valid;
        [SerializeField] private float width;
        [SerializeField] private float height;
        [SerializeField] private Vector2 anchor;
        [SerializeField] private string generatedAtUtc = "";

        public string Sidc => sidc;
        public string DecodedSidc => decodedSidc;
        public MilsymbolStandard Standard => standard;
        public MilsymbolIconStyle Style => style;
        public Texture2D Texture => texture;
        public bool Valid => valid;
        public float Width => width;
        public float Height => height;
        public Vector2 Anchor => anchor;
        public string GeneratedAtUtc => generatedAtUtc;

        public void SetGeneratedData(
            MilsymbolIconRequest request,
            string decodedSidcText,
            bool generatedValid,
            float generatedWidth,
            float generatedHeight,
            Vector2 generatedAnchor,
            Texture2D generatedTexture = null)
        {
            sidc = request.sidc;
            decodedSidc = decodedSidcText ?? "";
            standard = request.standard;
            style = request.style;
            texture = generatedTexture;
            valid = generatedValid;
            width = generatedWidth;
            height = generatedHeight;
            anchor = generatedAnchor;
            generatedAtUtc = DateTime.UtcNow.ToString("O");
        }
    }
}
