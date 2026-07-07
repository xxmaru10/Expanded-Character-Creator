using System;
using UnityUtils;
using RpgEngine.Characters;

namespace CustomPartsMod
{
    /// <summary>Deletes a custom part: from the preview, the engine catalog, the tab list, and
    /// its saved scale/offset. Used by the per-item trash button.</summary>
    internal static class PartsAdmin
    {
        internal static void Delete(string partId)
        {
            string key = CustomPartCatalog.TryGet(partId, out var p) ? p.SourceKey : null;
            var creator = UniqueMono<CharacterCreator>.instance;

            try
            {
                if (creator != null && creator.dummy != null && creator.dummy.attachedItems.ContainsKey(partId))
                    creator.dummy.RemovePart(partId); // remove from the preview character
            }
            catch (Exception e) { Plugin.Log.LogWarning("Excluir (preview): " + e.Message); }

            CustomPartCatalog.Remove(partId); // registry + CharacterCreator.attachmentPaths

            try
            {
                if (creator != null && creator.itemTabsLoader != null && creator.itemTabsLoader.tabSystem != null)
                {
                    creator.itemTabsLoader.tabSystem.RemoveContentItem(partId);
                    creator.itemTabsLoader.Refresh();
                }
            }
            catch (Exception e) { Plugin.Log.LogWarning("Excluir (aba): " + e.Message); }

            if (!string.IsNullOrEmpty(key)) ScaleStore.Remove(key); // forget saved scale/offset
            if (!string.IsNullOrEmpty(key)) ThumbnailStore.Delete(key); // P7 — remove saved portrait
            ScaleSession.Close();

            Compat.ShowSuccess("Modelo removido.");
        }
    }
}
