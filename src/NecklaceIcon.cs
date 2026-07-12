using UnityEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Builds a pure-white necklace sprite dynamically in memory.
    /// </summary>
    internal static class NecklaceIcon
    {
        private static Sprite _cached;

        internal static Sprite Get()
        {
            if (_cached != null) return _cached;

            const int N = 64;
            float cx = (N - 1) * 0.5f, cy = (N - 1) * 0.5f;

            var white = new Color32(255, 255, 255, 255);
            var clear = new Color32(0, 0, 0, 0);

            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    int i = y * N + x;

                    // U-shape necklace chain
                    float ellipse = (dx * dx) / (18f * 18f) + ((dy - 8f) * (dy - 8f)) / (14f * 14f);
                    bool isChain = ellipse >= 0.85f && ellipse <= 1.0f && dy <= 8f;

                    // Pendant
                    float pendantDist = Mathf.Sqrt(dx * dx + (dy + 6f) * (dy + 6f));
                    bool isPendant = pendantDist <= 4f;

                    if (isChain || isPendant)
                        px[i] = white;
                    else
                        px[i] = clear;
                }
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();

            _cached = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _cached;
        }
    }
}
