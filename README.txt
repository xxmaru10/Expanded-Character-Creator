================================================================
 Custom Body Parts  -  a mod for The RPG Engine
 v0.1.0
================================================================

The mod's in-game text follows the game's language automatically:
Portuguese when the game is in Portuguese, English otherwise.

================================================================
 ENGLISH
================================================================

WHAT IT DOES
------------
Adds an "Import Part" button to the Character Creator. Inside any
category (Heads, Hands, Feet, ...) you import an .OBJ file and it:
  - becomes a selectable option in that category;
  - attaches to the correct bone and follows the animation;
  - inherits the character shader, so you can paint it in RGB with
    the color pickers that already exist, like any native part.

It also adds:
  - "Import Folder": import every .OBJ in a folder at once, with
    shared conventions (scale/rotation/position/gender/color) and
    auto-paired textures.
  - An edit panel (opens on import, reopen with the "E" button):
    uniform + per-axis scale, rotation, position (type, nudge, or
    drag the part in the preview), gender, paint channel, attach
    mode (accessory-on-top vs replace-the-slot) and texture variants.
  - Two extra categories: "Eyes" (a sub-tab under Head) and
    "Shoe (footwear)" (a button shown while you are in the Feet
    category). The native "Feet" category is your normal feet.
  - Feet & shoes: when importing you pick LEFT or RIGHT foot
    (legLowerL / legLowerR); a shoe stacks on top of the foot.
  - "Custom only" filter, tags, a "Random" roll (custom parts only,
    with per-category locks) and per-part thumbnail icons.
  - Persistence: your imports survive restarting the game.

REQUIREMENTS
------------
- The RPG Engine (Unity 2021.3, Mono).
- BepInEx 5.4.x x64 - ALREADY INCLUDED in this package.

INSTALL
-------
1. Close the game.
2. Extract EVERYTHING from "CustomPartsMod-v0.1.0.zip" into the
   game's ROOT folder (where The_RPG_Engine.exe is):
     ...\steamapps\common\The RPG Engine\
   It should look like this:
     The RPG Engine\winhttp.dll
     The RPG Engine\BepInEx\plugins\CustomPartsMod\CustomPartsMod.dll
3. Start the game once (BepInEx initializes and creates its
   config/log folders). That's it.

Nothing in the game folder is modified - the mod is just a drop-in
loader, so it is 100% reversible (see UNINSTALL).

HOW TO TEST
-----------
1. Open the game and enter the Character Creator.
2. Go to a parts category (e.g. Heads).
3. Click "Import Part" (new button, in the top-right menu).
4. Pick an .OBJ file. The part appears in the list and on the model.
5. Use the color pickers to paint it.
(For Feet/Shoe you will be asked Left or Right first.)

UNINSTALL  (100% reversible - no game file is changed)
------------------------------------------------------
Delete from the game's root folder:
  - winhttp.dll
  - doorstop_config.ini
  - .doorstop_version
  - changelog.txt
  - the BepInEx\ folder
The game is back to the original.
(To only DISABLE the mod but keep BepInEx: delete just
 BepInEx\plugins\CustomPartsMod\ .)

Your imported parts live in the "CustomParts" folder in the game
root; deleting it removes your imports (keep it to keep them).

CONFIG
------
After running the game once, edit:
  BepInEx\config\com.xxmaru.rpgengine.custombodyparts.cfg
  - DefaultScale  : starting scale of imported parts (default 1.0).
  - UseGameShader : true = inherit the game shader (paintable).
                    false = simple shader (if it looks wrong).

LOG (for troubleshooting)
-------------------------
  BepInEx\LogOutput.log   - search for "Custom Body Parts".

KNOWN LIMITATIONS
-----------------
- .OBJ (and .STL) reload on restart; .GLB imports work but do not
  yet come back after a restart.
- Rigid attach: the part follows the bone but does not skin-deform
  (e.g. fingers do not articulate). True deformation needs a rigged
  mesh and is a future step.
- Custom parts are local (not synced in multiplayer yet).
- Position/scale may need a manual tweak per model (edit panel).

BUILD FROM SOURCE
-----------------
Requires the .NET SDK, and a "lib\" folder (next to "src\", not
tracked in the repo) with two DLLs from a BepInEx 5.4.x x64 release
(https://github.com/BepInEx/BepInEx/releases):
  lib\BepInEx.dll
  lib\0Harmony.dll
(Extract any BepInEx 5.4.x x64 zip and copy those two files out of
its BepInEx\core\ folder.)

Then, in the project folder:
  dotnet build src/CustomPartsMod.csproj -c Release
If your game is in another path:
  dotnet build src/CustomPartsMod.csproj -c Release ^
    -p:GameManaged="PATH\The_RPG_Engine_Data\Managed"
Output: src\bin\Release\CustomPartsMod.dll

To repackage the installer zip (dist\CustomPartsMod-vX.Y.Z.zip):
copy the built DLL into dist\package\BepInEx\plugins\CustomPartsMod\,
then zip the CONTENTS of dist\package\ (the BepInEx 5.4.x x64 release
itself provides everything else already in dist\package\: winhttp.dll,
doorstop_config.ini, .doorstop_version, changelog.txt, BepInEx\core\).


================================================================
 PORTUGUÊS
================================================================

O QUE FAZ
---------
Adiciona um botão "Importar Parte" no Criador de Personagens.
Dentro de qualquer categoria (Cabeças, Mãos, Pés, ...) você importa
um arquivo .OBJ e ele:
  - vira uma opção selecionável naquela categoria;
  - é anexado ao osso certo e acompanha a animação;
  - herda o shader do personagem, então dá para pintar em RGB com
    os color pickers que já existem, como qualquer parte nativa.

Também adiciona:
  - "Importar Pasta": importa todos os .OBJ de uma pasta de uma vez,
    com convenções compartilhadas (escala/rotação/posição/gênero/cor)
    e texturas pareadas automaticamente.
  - Um painel de edição (abre no import, reabre no botão "E"):
    escala uniforme + por eixo, rotação, posição (digitar, empurrar
    ou arrastar a peça no preview), gênero, canal de pintura, modo de
    encaixe (acessório por cima vs substitui o slot) e variações de
    textura.
  - Duas categorias extras: "Olhos" (uma sub-aba dentro de Cabeça) e
    "Sapato (calçado)" (um botão que aparece quando você está na
    categoria Pés). A categoria "Pés" nativa são os seus pés normais.
  - Pés e sapatos: ao importar você escolhe o pé ESQUERDO ou DIREITO
    (legLowerL / legLowerR); o sapato fica por cima do pé.
  - Filtro "Só custom", tags, um "Aleatório" (só peças custom, com
    travas por categoria) e miniaturas por peça.
  - Persistência: seus imports sobrevivem a fechar o jogo.

REQUISITOS
----------
- The RPG Engine (Unity 2021.3, Mono).
- BepInEx 5.4.x x64 - JÁ VEM INCLUSO neste pacote.

INSTALAR
--------
1. Feche o jogo.
2. Extraia TUDO de "CustomPartsMod-v0.1.0.zip" para a pasta RAIZ do
   jogo (onde fica The_RPG_Engine.exe):
     ...\steamapps\common\The RPG Engine\
   Deve ficar assim:
     The RPG Engine\winhttp.dll
     The RPG Engine\BepInEx\plugins\CustomPartsMod\CustomPartsMod.dll
3. Abra o jogo UMA vez (o BepInEx se inicializa e cria as pastas de
   config/log). Pronto.

Nenhum arquivo do jogo é alterado - o mod é só um loader "drop de
pasta", então é 100% reversível (veja DESINSTALAR).

COMO TESTAR
-----------
1. Abra o jogo e entre no Criador de Personagens.
2. Vá até uma categoria de partes (ex.: Cabeças).
3. Clique em "Importar Parte" (botão novo, no menu do topo-direito).
4. Escolha um arquivo .OBJ. A parte aparece na lista e no boneco.
5. Use os color pickers para pintar.
(Em Pés/Sapato o mod pergunta Esquerda ou Direita primeiro.)

DESINSTALAR  (100% reversível - não altera nenhum arquivo do jogo)
-----------------------------------------------------------------
Apague da pasta raiz do jogo:
  - winhttp.dll
  - doorstop_config.ini
  - .doorstop_version
  - changelog.txt
  - a pasta BepInEx\
O jogo volta exatamente ao original.
(Para só DESLIGAR o mod sem remover o BepInEx: apague apenas
 BepInEx\plugins\CustomPartsMod\ .)

Suas peças importadas ficam na pasta "CustomParts" na raiz do jogo;
apagá-la remove seus imports (mantenha-a para mantê-los).

CONFIG
------
Depois de rodar o jogo uma vez, edite:
  BepInEx\config\com.xxmaru.rpgengine.custombodyparts.cfg
  - DefaultScale  : escala inicial das partes importadas (padrão 1.0).
  - UseGameShader : true = herda o shader do jogo (pintável).
                    false = shader simples (se ficar estranho visual).

LOG (para diagnóstico)
----------------------
  BepInEx\LogOutput.log   - procure por "Custom Body Parts".

LIMITAÇÕES CONHECIDAS
---------------------
- .OBJ (e .STL) recarregam ao reiniciar; imports .GLB funcionam mas
  ainda não voltam depois de reiniciar.
- Anexo rígido: a peça segue o osso mas não deforma junto (ex.: os
  dedos não articulam). Deformação real exige malha rigada e é um
  passo futuro.
- Peças custom são locais (ainda não sincronizam em multiplayer).
- Posição/escala podem precisar de ajuste manual por modelo (painel).

COMPILAR DO CÓDIGO-FONTE
------------------------
Requer o .NET SDK, e uma pasta "lib\" (do lado de "src\", não vem no
repositório) com dois DLLs de uma release do BepInEx 5.4.x x64
(https://github.com/BepInEx/BepInEx/releases):
  lib\BepInEx.dll
  lib\0Harmony.dll
(Extraia qualquer zip do BepInEx 5.4.x x64 e copie esses dois
arquivos de dentro de BepInEx\core\.)

Depois, na pasta do projeto:
  dotnet build src/CustomPartsMod.csproj -c Release
Se o seu jogo estiver em outro caminho:
  dotnet build src/CustomPartsMod.csproj -c Release ^
    -p:GameManaged="CAMINHO\The_RPG_Engine_Data\Managed"
Saída: src\bin\Release\CustomPartsMod.dll

Para reempacotar o zip instalador (dist\CustomPartsMod-vX.Y.Z.zip):
copie o DLL compilado para dist\package\BepInEx\plugins\CustomPartsMod\,
depois compacte o CONTEÚDO de dist\package\ (a própria release do
BepInEx 5.4.x x64 já traz tudo que está em dist\package\: winhttp.dll,
doorstop_config.ini, .doorstop_version, changelog.txt, BepInEx\core\).
