using UnityEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Builds a pure-white earring sprite dynamically in memory.
    /// </summary>
    internal static class EarringIcon
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

                    // Hoop circle (ring)
                    float distCenter = Mathf.Sqrt(dx * dx + (dy + 4f) * (dy + 4f));
                    // Hanger line/hook
                    bool isHanger = Mathf.Abs(dx) <= 1.2f && dy >= -4f && dy <= 14f;
                    // Hook bend at top
                    bool isHook = (dx - 2f) * (dx - 2f) + (dy - 14f) * (dy - 14f) <= 2.2f * 2.2f && dy >= 14f;

                    if ((distCenter >= 8f && distCenter <= 11f) || isHanger || isHook)
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
