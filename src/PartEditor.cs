using UnityEngine;
using UnityUtils;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>
    /// P6 — reopen the scale/position panel for an already-imported custom part. Ensures the part
    /// is applied to the preview (so there is a live attachment to bind to), then opens
    /// <see cref="ScaleSession"/> seeded with the part's current values.
    /// </summary>
    internal static class PartEditor
    {
        internal static void Reopen(string partId)
        {
            var creator = UniqueMono<CharacterCreator>.instance;
            if (creator == null || creator.dummy == null || creator.createNew == null)
            {
                Compat.ShowError("Criador de personagens indisponivel.");
                return;
            }
            if (!CustomPartCatalog.TryGet(partId, out var part))
                return;

            // Need a live attachment; apply the part if it isn't on the preview right now.
            if (!creator.dummy.attachedItems.ContainsKey(partId))
                creator.SpawnAlongside(partId);

            if (!creator.dummy.attachedItems.TryGetValue(partId, out var att)
                || !(att is CustomBodyPartAttachment custom))
            {
                Compat.ShowError("Nao consegui reabrir a edicao dessa parte.");
                return;
            }

            custom.ResetPaint(); // reopening the gear reverts the part to its pure texture (P2)

            var canvas = creator.createNew.GetComponentInParent<Canvas>();
            Transform canvasT = canvas != null ? canvas.rootCanvas.transform : creator.transform;
            GameObject inputTemplate = creator.characterName != null ? creator.characterName.gameObject : null;

            ScaleSession.Open(creator.createNew.gameObject, inputTemplate, canvasT, custom,
                part.SourceKey, part.DisplayName,
                custom.UserScale, custom.UserOffset, custom.UserEuler, custom.UserScaleAxis, part.GenderTag);
        }
    }
}
