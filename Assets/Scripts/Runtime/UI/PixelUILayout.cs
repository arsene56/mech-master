using UnityEngine;

namespace MechMaster.Runtime.UI
{
    // Layout scales, glyph bitmaps do not: draw at native render-target pixels.
    public readonly struct PixelUILayout
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public float Scale { get; }
        public Vector2 Offset { get; }

        public PixelUILayout(int width, int height)
        {
            Scale = Mathf.Max(0.01f, Mathf.Min(width / ReferenceWidth, height / ReferenceHeight));
            Offset = new Vector2(
                Mathf.Round((width - ReferenceWidth * Scale) * 0.5f),
                Mathf.Round((height - ReferenceHeight * Scale) * 0.5f));
        }

        public float Length(float referenceLength) => Mathf.Round(referenceLength * Scale);
        public int FontSize(int referenceSize) => Mathf.Max(1, Mathf.RoundToInt(referenceSize * Scale));

        public Rect ToPixels(Rect referenceRect)
        {
            return Rect.MinMaxRect(
                Offset.x + Length(referenceRect.xMin),
                Offset.y + Length(referenceRect.yMin),
                Offset.x + Length(referenceRect.xMax),
                Offset.y + Length(referenceRect.yMax));
        }

        public RectOffset ScaleInsets(RectOffset source)
        {
            return new RectOffset((int)Length(source.left), (int)Length(source.right),
                (int)Length(source.top), (int)Length(source.bottom));
        }
    }
}
