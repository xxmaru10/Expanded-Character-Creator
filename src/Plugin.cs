using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace CustomPartsMod
{
    /// <summary>
    /// BepInEx entry point. Applies the Harmony patches that wire a custom OBJ mesh
    /// into The RPG Engine's character creator as a selectable, rigid body part.
    /// Phase 0 (scaffold) + Phase 1 (in-memory import) of DESIGN.md.
    /// </summary>
    [BepInPlugin(Guid, "Custom Body Parts", "0.1.2")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.xxmaru.rpgengine.custombodyparts";

        public static ManualLogSource Log;
        public static ConfigEntry<float> DefaultScale;
        public static ConfigEntry<bool> UseGameShader;
        public static ConfigEntry<float> ThumbnailFill;
        public static ConfigEntry<float> ThumbnailYaw;

        private void Awake()
        {
            Log = Logger;

            DefaultScale = Config.Bind(
                "General", "DefaultScale", 1f,
                "Tamanho-alvo (em unidades de mundo) da MAIOR dimensao de uma parte recem-importada. " +
                "A escala inicial e calculada pra bater esse tamanho, entao o OBJ aparece visivel de cara " +
                "(independente de o modelo vir minusculo ou gigante). Depois que voce ajusta uma peca e " +
                "clica Confirmar, um MULTIPLICADOR sobre esse tamanho e memorizado (global ou por " +
                "categoria) e passa a valer pros proximos imports. Ex.: 1 = ~1 unidade; 0.5 = metade.");

            UseGameShader = Config.Bind(
                "General", "UseGameShader", true,
                "Se true, a parte herda o material/shader de personagem do jogo (fica pintavel pelos color pickers). " +
                "Se ficar estranho visualmente, mude para false para usar um shader simples.");

            ThumbnailFill = Config.Bind(
                "General", "ThumbnailFill", 0.9f,
                "P7 (miniatura): fracao do quadro que a peca ocupa na foto do botao (0.1 a 1.0). " +
                "Maior = mais zoom/maior. 0.9 = ocupa 90% do quadro. Se cortar as bordas, baixe um pouco.");

            ThumbnailYaw = Config.Bind(
                "General", "ThumbnailYaw", 0f,
                "P7 (miniatura): angulo (graus) de rotacao ao redor do eixo Y ao fotografar a peca pro " +
                "icone. 0 = vista de frente. Se o icone sair mostrando as COSTAS do modelo, coloque 180. " +
                "Use 90/270 pra perfil.");

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Plugin).Assembly);

            Log.LogInfo("CustomPartsMod v0.1.2 carregado. Patches aplicados.");
        }
    }
}
