using UnityEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Builds a small, PURE-WHITE eye <see cref="Sprite"/> in code (no art asset) for the "Olhos"
    /// category button, matching the engine's monochrome white category icons. The lens is the
    /// intersection of two vertically-offset circles (an almond pointed at the left/right corners),
    /// drawn as a white outline with a white pupil dot in the centre; everything else is transparent.
    /// </summary>
    internal static class EyeIcon
    {
        private static Sprite _cached;

        internal static Sprite Get()
        {
            if (_cached != null) return _cached;

            const int N = 64;
            float cx = (N - 1) * 0.5f, cy = (N - 1) * 0.5f;
            const float k = 18f, R = 30f, R2 = R * R;
            const float inner = (R - 2.6f) * (R - 2.6f); // outline band thickness
            const float pupilR = 6f;

            var white = new Color32(255, 255, 255, 255);
            var soft = new Color32(255, 255, 255, 140); // faint anti-alias edge
            var clear = new Color32(0, 0, 0, 0);

            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dA2 = dx * dx + (dy - k) * (dy - k); // circle centred above
                    float dB2 = dx * dx + (dy + k) * (dy + k); // circle centred below
                    int i = y * N + x;

                    bool inLens = dA2 <= R2 && dB2 <= R2;
                    float dc2 = dx * dx + dy * dy;

                    if (inLens && (dA2 >= inner || dB2 >= inner))
                        px[i] = white;                 // almond outline (both arcs + the two corners)
                    else if (dc2 <= pupilR * pupilR)
                        px[i] = white;                 // pupil dot
                    else if (dc2 <= (pupilR + 1f) * (pupilR + 1f))
                        px[i] = soft;                  // soft pupil edge
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
