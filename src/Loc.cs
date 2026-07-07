using System;
using System.Collections.Generic;
using UnityEngine;

namespace CustomPartsMod
{
    /// <summary>
    /// Lightweight localization for the mod's own UI. The source language of every literal in the code is
    /// Portuguese; <see cref="T"/> returns the English translation when the GAME is set to a non-Portuguese
    /// language, otherwise it returns the Portuguese literal unchanged. Applied inside the shared UI funnels
    /// (<see cref="UiFactory"/> label/button, <see cref="Compat"/> toasts, <see cref="PanelUi"/> button
    /// labels), so almost every string is translated with no call-site changes. The current language is read
    /// live from the engine's <c>PlayerPrefs["Language"]</c> ("pt", "en", …), so switching the game language
    /// and reopening the creator shows the mod in the new language. Missing keys fall back to Portuguese.
    /// </summary>
    internal static class Loc
    {
        /// <summary>True when the game language is NOT Portuguese (English is the international fallback).</summary>
        internal static bool IsEnglish
        {
            get
            {
                string lang;
                try { lang = PlayerPrefs.GetString("Language", "en"); }
                catch { return true; }
                lang = (lang ?? "").Trim().ToLowerInvariant();
                return !(lang.StartsWith("pt") || lang.Contains("portug") || lang.Contains("bras"));
            }
        }

        internal static string T(string pt)
        {
            if (string.IsNullOrEmpty(pt) || !IsEnglish) return pt;
            return En.TryGetValue(pt, out var en) ? en : pt;
        }

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // --- injected buttons ---
            { "Importar Parte", "Import Part" },
            { "Importar Pasta", "Import Folder" },
            { "Aleatório", "Random" },
            { "Sapato (calçado)", "Shoe (footwear)" },
            { "Só custom: SIM", "Custom only: YES" },
            { "Só custom: NÃO", "Custom only: NO" },

            // --- panel titles ---
            { "Lado do pé", "Foot side" },
            { "Importar pasta — convenções", "Import folder — conventions" },
            { "Aleatório (só peças custom)", "Random (custom parts only)" },

            // --- ScaleSession / MassImportConfig labels & buttons ---
            { "arraste para mover", "drag to move" },
            { "Escala", "Scale" },
            { "Escala ×", "Scale ×" },
            { "Esc XYZ", "Scale XYZ" },
            { "Rotação", "Rotation" },
            { "Posição", "Position" },
            { "Passo", "Step" },
            { "Salvar padrão", "Save as default" },
            { "Só desta vez", "Just this time" },
            { "Escolher pasta e importar", "Pick folder & import" },
            { "Cancelar", "Cancel" },

            // --- FootSide prompt / mass-import side toggle ---
            { "Este modelo é para qual pé?", "Which foot is this model for?" },
            { "◀ Esquerda", "◀ Left" },
            { "Direita ▶", "Right ▶" },
            { "Lado: Esquerda ◀", "Side: Left ◀" },
            { "Lado: Direita ▶", "Side: Right ▶" },

            // --- gender / attach-mode / modeling cycles ---
            { "Gênero: Ambos", "Gender: Both" },
            { "Gênero: Feminino", "Gender: Female" },
            { "Gênero: Masculino", "Gender: Male" },
            { "Encaixe: Auto", "Attach: Auto" },
            { "Encaixe: Acessório (por cima)", "Attach: Accessory (on top)" },
            { "Encaixe: Substitui o slot", "Attach: Replace slot" },
            { "2  Posição", "2  Position" },
            { "3  Grossura", "3  Thickness" },
            { "4  Rotação", "4  Rotation" },
            { "[X] Modo modelagem ATIVO — arraste a peça", "[X] Modeling mode ON — drag the part" },
            { "[ ] Ativar modo modelagem", "[ ] Enable modeling mode" },

            // --- paint channel (full "Paleta de cor: X" strings that flow through the funnel) ---
            { "Paleta de cor: Pele", "Color palette: Skin" },
            { "Paleta de cor: Cabelo", "Color palette: Hair" },
            { "Paleta de cor: Olhos", "Color palette: Eyes" },
            { "Paleta de cor: Primário (torso)", "Color palette: Primary (torso)" },
            { "Paleta de cor: Secundário (pernas)", "Color palette: Secondary (legs)" },
            { "Paleta de cor: Couro A (botas)", "Color palette: Leather A (boots)" },
            { "Paleta de cor: Couro B (joelho/ombro)", "Color palette: Leather B (knee/shoulder)" },
            { "Paleta de cor: Metal A (braço)", "Color palette: Metal A (arm)" },
            { "Paleta de cor: Metal B (mãos)", "Color palette: Metal B (hands)" },
            { "Paleta de cor: Metal escuro (capacete)", "Color palette: Dark metal (helmet)" },
            { "Paleta de cor: Brilho (acessório)", "Color palette: Glow (accessory)" },

            // --- random panel ---
            { "Aleatorizar", "Randomize" },
            { "Fechar", "Close" },
            { "Importe peças custom primeiro.", "Import custom parts first." },
            { "[X] Travado", "[X] Locked" },
            { "[ ] Travar", "[ ] Lock" },

            // --- file-browser titles ---
            { "Selecionar pasta com .obj", "Select folder with .obj" },
            { "Importar pasta", "Import folder" },

            // --- success toasts ---
            { "Categoria Olhos. Importe um olho ou escolha um da lista.", "Eyes category. Import an eye or choose one from the list." },
            { "Categoria Sapato. Importe um sapato (escolha o lado) ou escolha um da lista.", "Shoe category. Import a shoe (pick the side) or choose one from the list." },
            { "Modelo removido.", "Model removed." },
            { "Parte importada:", "Part imported:" },
            { "Padrão salvo — novos modelos desta categoria virão assim.", "Default saved — new models in this category will start like this." },
            { "Aplicado só nesta sessão:", "Applied for this session only:" },
            { "Pasta importada:", "Folder imported:" },
            { "parte(s).", "part(s)." },
            { "parte(s), alguns com erro.", "part(s), some with errors." },

            // --- error toasts ---
            { "Criador de personagens indisponivel.", "Character creator unavailable." },
            { "Criador indisponível.", "Creator unavailable." },
            { "Criador indisponível para aleatorizar.", "Creator unavailable to randomize." },
            { "Abra uma categoria (ex.: Cabecas, Olhos ou Pés) antes de importar.", "Open a category (e.g. Heads, Eyes or Feet) before importing." },
            { "Abra uma categoria antes de navegar.", "Open a category before navigating." },
            { "Nenhuma opcao nesta categoria.", "No option in this category." },
            { "Nao foi possivel importar (use um .obj valido).", "Couldn't import (use a valid .obj)." },
            { "Nenhum .obj encontrado nessa pasta.", "No .obj found in that folder." },
            { "Nenhum .obj da pasta pode ser importado.", "No .obj in the folder could be imported." },
            { "Nao consegui ler a pasta:", "Couldn't read the folder:" },
            { "Importe peças custom primeiro — não há nada para sortear.", "Import custom parts first — nothing to randomize." },
            { "Não achei o preview do personagem para modelar.", "Couldn't find the character preview to model." },
            { "Nao consegui carregar essa textura.", "Couldn't load that texture." },
            { "Nao consegui carregar essa imagem.", "Couldn't load that image." },
            { "Nao consegui reabrir a edicao dessa parte.", "Couldn't reopen editing for that part." },
        };
    }
}
