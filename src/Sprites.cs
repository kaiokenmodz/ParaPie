using UnityEngine;

namespace ParaPie
{
    /// <summary>
    /// Procedurally drawn, anti-aliased sprites so the mod ships as a single DLL with no asset bundle.
    /// Everything is white so it can be tinted with Image.color.
    /// </summary>
    internal static class Sprites
    {
        private static Sprite _circle, _softShadow, _glow, _ring, _cycle, _star;

        /// <summary>Solid circle, 9-sliced so it stretches into a pill.</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    const int s = 128;
                    _circle = Make(s, (x, y) =>
                    {
                        float d = Dist(x, y, s);
                        return Mathf.Clamp01(s / 2f - d);
                    }, new Vector4(63, 63, 63, 63));
                }
                return _circle;
            }
        }

        /// <summary>Blurred circle used as a drop shadow, also 9-sliced.</summary>
        public static Sprite SoftShadow
        {
            get
            {
                if (_softShadow == null)
                {
                    const int s = 128;
                    _softShadow = Make(s, (x, y) =>
                    {
                        float d = Dist(x, y, s) / (s / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        return a * a * (3f - 2f * a);
                    }, new Vector4(63, 63, 63, 63));
                }
                return _softShadow;
            }
        }

        /// <summary>Very soft radial glow for the backdrop behind the whole pie.</summary>
        public static Sprite Glow
        {
            get
            {
                if (_glow == null)
                {
                    const int s = 256;
                    _glow = Make(s, (x, y) =>
                    {
                        float d = Dist(x, y, s) / (s / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        return Mathf.Pow(a, 1.6f);
                    }, Vector4.zero);
                }
                return _glow;
            }
        }

        /// <summary>Thin dashed ring that the options sit on.</summary>
        public static Sprite DashedRing
        {
            get
            {
                if (_ring == null)
                {
                    const int s = 512;
                    const float thickness = 3.2f;
                    _ring = Make(s, (x, y) =>
                    {
                        float r = s / 2f - 4f;
                        float d = Dist(x, y, s);
                        float a = Mathf.Clamp01(thickness / 2f - Mathf.Abs(d - r) + 0.5f);
                        float ang = Mathf.Atan2(y - s / 2f, x - s / 2f) * Mathf.Rad2Deg + 180f;
                        float dash = Mathf.Repeat(ang, 6f);
                        float dashA = Mathf.Clamp01(Mathf.Min(dash, 3.6f - dash) * 2f);
                        return a * dashA;
                    }, Vector4.zero);
                }
                return _ring;
            }
        }

        /// <summary>Circular "swap variant" arrow.</summary>
        public static Sprite Cycle
        {
            get
            {
                if (_cycle == null)
                {
                    const int s = 64;
                    _cycle = Make(s, (x, y) =>
                    {
                        float cx = s / 2f, cy = s / 2f;
                        float d = Dist(x, y, s);
                        float ang = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg;
                        if (ang < 0) ang += 360f;
                        float ring = Mathf.Clamp01(3.2f - Mathf.Abs(d - 17f));
                        if (ang > 20f && ang < 70f) ring = 0f; // gap
                        // arrow head at the end of the arc (angle 20deg), pointing clockwise
                        Vector2 tip = new Vector2(cx + Mathf.Cos(20f * Mathf.Deg2Rad) * 17f, cy + Mathf.Sin(20f * Mathf.Deg2Rad) * 17f);
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - tip;
                        Vector2 dir = new Vector2(Mathf.Sin(20f * Mathf.Deg2Rad), -Mathf.Cos(20f * Mathf.Deg2Rad)); // tangent
                        Vector2 nrm = new Vector2(-dir.y, dir.x);
                        float along = Vector2.Dot(p, -dir);
                        float side = Mathf.Abs(Vector2.Dot(p, nrm));
                        float head = (along > -2f && along < 12f) ? Mathf.Clamp01((12f - along) * 0.75f - side + 0.5f) : 0f;
                        return Mathf.Max(ring, head);
                    }, Vector4.zero);
                }
                return _cycle;
            }
        }

        /// <summary>Rounded five point star used for the storyteller.</summary>
        public static Sprite Star
        {
            get
            {
                if (_star == null)
                {
                    const int s = 128;
                    _star = Make(s, (x, y) =>
                    {
                        float px = (x + 0.5f - s / 2f) / (s / 2f);
                        float py = (y + 0.5f - s / 2f) / (s / 2f);
                        float r = Mathf.Sqrt(px * px + py * py);
                        float a = Mathf.Atan2(py, px) + Mathf.PI / 2f;
                        float k = Mathf.Cos(5f * a);
                        float edge = Mathf.Lerp(0.5f, 0.92f, k * 0.5f + 0.5f);
                        return Mathf.Clamp01((edge - r) * s / 2f);
                    }, Vector4.zero);
                }
                return _star;
            }
        }

        private static float Dist(int x, int y, int s)
        {
            float dx = x + 0.5f - s / 2f;
            float dy = y + 0.5f - s / 2f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private delegate float AlphaFn(int x, int y);

        private static Sprite Make(int size, AlphaFn alpha, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
