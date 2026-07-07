using UnityEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Builds a small, PURE-WHITE side-profile shoe <see cref="Sprite"/> in code (no art asset) for the
    /// "Sapato" category button, matching the engine's monochrome white category icons. The silhouette is
    /// a flat sole bar (with a rounded toe at the front) unioned with a rounded upper (a half-ellipse over
    /// the heel/mid); everything else is transparent.
    /// </summary>
    internal static class ShoeIcon
    {
        private static Sprite _cached;

        internal static Sprite Get()
        {
            if (_cached != null) return _cached;

            const int N = 64;
            var white = new Color32(255, 255, 255, 255);
            var clear = new Color32(0, 0, 0, 0);

            // y grows upward. Sole = bar across the bottom; toe = rounded cap at the front (right).
            const float soleBottom = 14f, soleTop = 21f, soleLeft = 8f, soleRight = 54f;
            const float toeCx = 50f, toeCy = 21f, toeRx = 9f, toeRy = 7f;   // rounded front cap
            // Upper = top half of an ellipse over the heel/mid, opening toward the back (left).
            const float upCx = 26f, upCy = 21f, upRx = 20f, upRy = 15f;

            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int i = y * N + x;
                    px[i] = clear;

                    bool inSole = x >= soleLeft && x <= soleRight && y >= soleBottom && y <= soleTop;
                    bool inToe = InEllipse(x, y, toeCx, toeCy, toeRx, toeRy) && y >= soleBottom;
                    bool inUpper = y >= upCy && x <= upCx + upRx && x >= soleLeft
                                   && InEllipse(x, y, upCx, upCy, upRx, upRy);

                    if (inSole || inToe || inUpper) px[i] = white;
                }
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();

            _cached = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _cached;
        }

        private static bool InEllipse(int x, int y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }
    }
}
