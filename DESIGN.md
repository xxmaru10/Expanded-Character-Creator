---
doc: Custom Body Parts — Design & Guia de navegação (The RPG Engine mod)
language: pt-BR
status: Fase 1 + TODAS as features do ROADMAP concluídas (P0–P7, P9–P15; P8 removido). Restam só
  polimentos opcionais (gizmo de setas coloridas sobre P11, sub-slots de acessório sobre P14,
  isolar a peça na miniatura). Ver ROADMAP.md/CHANGELOG.md.
last_updated: 2026-07-08 (Iteração 31 — import de pasta recursivo (ambos os botões) + novo botão
  "Importar Pasta (texturas compartilhadas)": por subpasta compartilha as texturas entre todos os OBJs
  e auto-roteia cada peça pela categoria/slot/lado lendo o prefixo PT do nome (PartNameRouter +
  NativeCategory, savePath nativo descoberto em runtime). Iteração 30 — carga preguiçosa da malha +
  batch do scales.json. CHANGELOG/ROADMAP são a fonte da verdade.)
fonte_da_verdade:
  historico: CHANGELOG.md          # o que já foi feito, iteração por iteração
  futuro: ROADMAP.md               # o que falta (P0–P11) com nota de viabilidade
  decompilado: assinaturas exatas verificadas via ilspycmd em RpgEngine.dll (ver Regras §4)
---

# Custom Body Parts — Design & Guia (The RPG Engine)

> **Documento de design + ponto de entrada para agentes de IA.** A Fase 1 (botão "Importar
> Parte" + parte na aba, em memória) já está **implementada e instalada** no jogo; o histórico
> completo está em `CHANGELOG.md` e os próximos passos em `ROADMAP.md`. As seções numeradas 1–12
> abaixo são o **design de referência** (arquitetura, fluxos, modelo de dados) — leia-as sob demanda.
> Foco de prioridade do usuário: **Import por categoria + Persistência** e **Pintura RGB**.
> Instalar/remover é um **drop de pasta reversível** que não altera arquivo nenhum da engine.

---

## Como usar este documento (leia isto primeiro)

Este é o ponto de entrada. **Leia esta camada de navegação primeiro**, depois carregue **apenas**
o que a sua tarefa exige, usando as tabelas abaixo. Não abra os 16 arquivos de `src/` "para ver" —
o **Mapa do código-fonte** diz exatamente onde cada coisa mora.

- **Orçamento de contexto:** mantenha a janela enxuta. Se a tarefa parece exigir abrir mais de
  ~4 arquivos grandes, releia o **Carregar por contexto** e reduza o escopo primeiro.
- **Fonte da verdade por eixo:** *o que já existe* → `CHANGELOG.md`; *o que falta* → `ROADMAP.md`;
  *assinaturas da engine* → decompilação (`ilspycmd`), nunca de memória. Este doc é o design/arquitetura.
- **Antes de confiar em qualquer método/campo da engine**, valide a assinatura (ver Regras §4).

## Mapa de documentos (abra o relevante, não tudo)

| Arquivo | Quando abrir |
|---|---|
| `DESIGN.md` (este) | Arquitetura, modelo de dados, fluxos por fase, decisões. Ponto de partida. |
| `CHANGELOG.md` | O que já foi feito (Iterações 0–6) e **limitações conhecidas de hoje**. |
| `ROADMAP.md` | O que falta: **P0–P11** com esforço 🟢/🟡/🔴 e dependências entre features. |
| `README.txt` | Instruções de instalação/remoção para o usuário final. |
| `src/*.cs` | Código do mod. Use o **Mapa do código-fonte** abaixo para ir direto ao arquivo. |
| memória `rpgengine-modding-anchors` | Classes/métodos decompilados exatos a patchar (criador, import, paint, oficina). |
| memória `project-custom-bodyparts-mod` | Objetivo e restrições do usuário + status detalhado por iteração. |

## Mapa do código-fonte (`src/`) — pule direto ao arquivo certo

Total ~1.5k linhas. "Âncora na engine" = a classe/membro do jogo que o arquivo patcha ou consome.

| Arquivo | Responsabilidade | Âncora na engine |
|---|---|---|
| `Plugin.cs` | Entrypoint BepInEx; `Harmony.PatchAll`; config (`DefaultScale`, `UseGameShader`). | BepInEx `BaseUnityPlugin` |
| `Patches.cs` | Harmony hooks: injeta parte custom no `AddPart`, deixa o `Contains` reconhecê-la, e postfix que pendura a lixeira no botão da aba. | `PickupableCharacter.AddPart/Contains`, `BuildTabsLoader_Characters.InitialiseDataItemButton` |
| `Compat.cs` | Reflexão para membros privados da engine (blindagem contra mudança de acesso). | `SetAllOn`, `AddPart(string,CharacterAttachment)`, `AddContentItemInitializer`, `pathPartsFilter`, `PopupAlert` |
| `CategoryMap.cs` | Mapeia a **aba ativa → categoria/slot** por `savePath` (independente de idioma). | `BuildTabsWithPathButtons.pathPartsFilter`, `savePath` |
| `ImportButton.cs` | Injeta o botão "Importar Parte" na UI do criador (clona um botão existente). | `CharacterCreator.createNew` |
| `ImportFlow.cs` | Orquestra o import: lê a categoria aberta → file browser → registra parte → abre `ScaleSession`. | `MeshImporter.LoadMesh(Action<RPGMesh>,string[])`, `FileBrowser.Result` |
| `MassImportButton.cs` | P1: injeta o botão "Importar Pasta" abaixo de "Importar Parte". | `CharacterCreator.createNew` |
| `MassImportConfig.cs` | P1: painel de **convenções** (gênero/canal/escala/rotação/posição) aplicadas a toda a pasta; sem textura. | `MassImportSettings`, `PanelUi`, `ScaleStore` |
| `MassImportFlow.cs` | P1: abre o painel → seletor de **pasta** (recursivo, `AllDirectories`) → varre `.obj`, importa cada um com as convenções (categoria aberta + lado) e persiste. | `FileBrowser.PickMode.Folders`, `Compat.ImportMesh` |
| `SharedFolderImportButton.cs` / `SharedFolderImportFlow.cs` | Botão "Importar Pasta (texturas compartilhadas)": recursivo; **por subpasta** compartilha todas as texturas entre todos os OBJs (variante 0 ativa) e auto-roteia cada peça pela categoria/slot/lado. Placement do padrão da categoria (P6). | `Directory.GetFiles(AllDirectories)`, `Compat.ImportMesh`, `ScaleStore.TryGetCategoryDefault` |
| `PartNameRouter.cs` | Lê o prefixo PT do nome (torso/braço/antebraço/mão/perna/pé + direito/esquerdo, accent-insensitive) → segmento(s) de categoria + `SidedCategory.Kind` + lado. Prefixo desconhecido → não roteia. | — (parsing) |
| `NativeCategory.cs` | Resolve o savePath de uma categoria **nativa** por segmento (ex.: `uppers`,`hands`,`torso`) varrendo `attachmentPaths` em runtime; cacheia sucessos. | `CharacterCreator.attachmentPaths` (`PropDatabaseData.savePath`) |
| `PanelUi.cs` | Helper de painel compartilhado (fundo/header/drag/campo/botão), extraído do padrão do `ScaleSession`. | Unity UI / SlickUi (`UiButton`, `UiInputField`) |
| `CustomPartCatalog.cs` | Cria o `PropDatabaseData` sintético e injeta em `attachmentPaths`; força rebuild da aba. | `CharacterCreator.attachmentPaths`, `PropDatabaseData` |
| `CustomPart.cs` | Modelo de dados da parte (id estável, categoria, caminho, slot, escala/offset) + `EnsureLoaded()` que importa malha/textura do disco **preguiçosamente** na 1ª aplicação (boot registra só metadados). | `MeshImporter.ImportNew` (via `Compat`) |
| `CustomBodyPartAttachment.cs` | Constrói a malha em runtime como `CharacterAttachment`: pivô centralizado, escala compensada, `SetTexture`/`SetColor`. | `CharacterAttachment` (subclasse), `RiggedAttachments.AttachmentPoint` |
| `BoneResolver.cs` | Acha o **osso animado** por substring de nome; fallback pro socket estático. | `character.boneHelper` |
| `ScaleSession.cs` | Painel flutuante pós-import: campos Escala/X/Y/Z, – / +, "Textura…", Confirmar, preview ao vivo. | `creator.characterName` (`UiInputField`, clonado) |
| `ScaleStore.cs` | Persiste escala + offset + caminho de textura por modelo em `CustomParts\scales.json`. | disco (`CustomParts\scales.json`) |
| `TextureLoader.cs` | Auto-encontra a textura irmã (`.mtl map_Kd` ou PNG/JPG de mesmo nome); carga manual. | `SimpleFileBrowser`, `ImageConversionModule` |
| `PartsAdmin.cs` | Apaga uma parte custom (preview, catálogo, aba, escala salva). | `dummy.RemovePart`, `UiScrollPool.RemoveContentItem` |
| `CustomFilter.cs` | P9: estado do filtro "só custom" + postfix no seletor de aba que esconde as nativas. | `BuildTabsWithPathButtons.Filter` |
| `CustomFilterButton.cs` | P9: botão de alternância "Só custom: SIM/NÃO" no criador. | `CharacterCreator.createNew` |
| `TrashButton.cs` | "X" vermelho por item custom → `PartsAdmin.Delete`. | `BuildTabsLoader_Characters` |
| `IconButton.cs` | P7: pinta a miniatura (retrato) no botão da peça custom, no lugar do texto. | `BuildTabsButton.icon` (`UiImage`) |
| `Thumbnailer.cs` | P7: no Confirmar, tira o retrato e dispara o Refresh da aba. | `CharacterCreator.SnapShot()` |
| `ThumbnailStore.cs` | P7: grava/lê o PNG do retrato em `CustomParts\thumbs\`, com cache. | disco (`CustomParts\thumbs\*.png`) |
| `EditButton.cs` | "E" azul por item custom → reabre o painel de edição (P6). | `BuildTabsLoader_Characters` |
| `PartEditor.cs` | P6: garante a parte aplicada e reabre `ScaleSession` com os valores atuais. | `CharacterCreator.SpawnAlongside`, `dummy.attachedItems` |
| `NavArrows.cs` | P12: setas ◀ ▶ ladeando o boneco que percorrem as opções visíveis da categoria e aplicam a próxima/anterior. | `CharacterCreatorCamera.inputPanel`, `tabSystem.selector`, `CharacterCreator.SpawnAlongside`, `attachmentPaths` |
| `NavArrowButton.cs` | Seta desenhada como triângulo (`MaskableGraphic`) + clique; sem fonte/sprite. | UnityEngine.UI (`MaskableGraphic`, `UIVertex`) |
| `NavArrowFollow.cs` | Mantém setas (e botões de zoom) colados ao boneco projetando a posição pela câmera. | `CharacterCreatorCamera.cam`, `WorldToViewportPoint` |
| `ZoomButton.cs` | Botão de zoom "+"/"–" desenhado com barras (`MaskableGraphic`) + clique; sem fonte. | UnityEngine.UI (`MaskableGraphic`) |
| `ZoomButtons.cs` | Injeta +/– ao lado das setas; zoom via `cam.fieldOfView`/`orthographicSize`. | `CharacterCreatorCamera.cam` |
| `AdditiveParts.cs` | Guarda a intenção de peças aditivas (olhos) e re-aplica as removidas por colateral. | `PickupableCharacter.AddPart`, `attachedItems` |
| `ChannelMap.cs` | P2: mapeia categoria (`savePath`)→canal de cor do personagem (`_Color_*`), independente de idioma. | `Colors.Part.GetColorId` (ids validados 1:1) |
| `EyesCategory.cs` | P2 Fatia 3: define a categoria sintética "Olhos" (`savePath` único + segmento `eyes`). | — (constante) |
| `ShoeCategory.cs` | Categoria sintética "Sapato" (`shoes`, aditivo) + `CustomCategory.IsSynthetic`. "Pés" é a categoria nativa Feet (sem sintético próprio). | — (constante) |
| `EyeIcon.cs` / `ShoeIcon.cs` | Ícones brancos desenhados em código (Texture2D→Sprite) dos botões de categoria. | UnityEngine (`Texture2D`/`Sprite`) |
| `CategoryTabButton.cs` | Injetor de sub-aba de categoria sintética (clona a sub-aba nativa mais próxima, troca ícone, `SetPathFilter`, coordena o sublinhado). Usado só por Olhos — a fileira de categorias do topo é um `BasicTabSystem` script-driven e NÃO pode ser clonada (ver Regras §4). | `BuildTabsWithPathButtons.SetPathFilter`, `SimpleTabSystem`, `createNew` |
| `CustomCategories.cs` | Declara o botão Olhos (sub-aba) e faz `EnsureAll` no criador. | — (coordenador) |
| `ShoeButton.cs` | Botão "Sapato" independente (não clonado na fileira nativa); visível só quando a categoria aberta é Pés/Sapato via postfix em `SetPathFilter`. | `BuildTabsWithPathButtons.SetPathFilter` |
| `SidedCategory.cs` | Identifica categorias com lado (Pés+Sapato, Mãos) e mapeia cada uma pro par de slots esquerda/direita. | `RiggedAttachType.legLowerL/R`, `handL/R` |
| `SidePrompt.cs` | Painel "Esquerda/Direita" mostrado antes do import individual numa categoria com lado. | — (painel próprio) |
| `Loc.cs` | Tradução PT→EN lida de `PlayerPrefs["Language"]`; usada dentro dos funis de UI. | `PlayerPrefs.GetString("Language")` |
| `LocButtons.cs` | Re-traduz ao vivo os botões PERSISTENTES (criados uma vez) quando o idioma muda em runtime. | `UnityUtils.Translator.onLanguageUpdated` |
| `UiFactory.cs` | Helpers de UI (clonar botão texto/ícone, rótulo TMP com fonte da cena, input fields); aplica `Loc.T` a todo texto. | TMP / Unity UI |

## Carregar por contexto (tarefa → o que abrir)

| Se sua tarefa é… | Abra estes |
|---|---|
| **Persistência de malha (P0)** / pasta drop-in | `ScaleStore.cs`, `ImportFlow.cs`, `Plugin.cs` · DESIGN §7 · ROADMAP P0 |
| Import / botão / detectar categoria | `ImportButton.cs`, `ImportFlow.cs`, `CategoryMap.cs`, `UiFactory.cs` · DESIGN §6 |
| **Import em massa (pasta inteira) (P1)** | `MassImportButton.cs`, `MassImportFlow.cs`, `ImportFlow.cs` (lógica reusada) · ROADMAP P1 |
| Colocação: escala/posição/osso/gizmo | `CustomBodyPartAttachment.cs`, `BoneResolver.cs`, `ScaleSession.cs` · ROADMAP P4/P5/P11 |
| Pintura RGB / shader do personagem | `CustomBodyPartAttachment.cs` (bloco de shader) · DESIGN §8 · ROADMAP P2 |
| Texturas / variantes (P13) | `TextureLoader.cs`, `CustomBodyPartAttachment.SetTexture`, `ScaleSession.cs` (caixas), `ScaleStore.cs` · ROADMAP P13 |
| Catálogo / abas / lixeira / filtro custom (P9) | `CustomPartCatalog.cs`, `Patches.cs`, `TrashButton.cs`, `PartsAdmin.cs`, `CustomFilter.cs` · ROADMAP P9 |
| Reflexão / assinatura privada da engine | `Compat.cs` · valide com `ilspycmd` (Regras §4) |
| Empacotar / instalar / distribuir | `dist/`, `README.txt` · receita nas Regras §5 · DESIGN §9 (Oficina) |

## Regras de comportamento para agentes

1. **Roteie antes de ler.** Leia esta camada de navegação primeiro; depois abra só os arquivos das
   tabelas acima. Sem varredura cega (`grep`/listagem recursiva) sem alvo definido pela tarefa.
2. **Fonte da verdade separada por eixo.** Passado → `CHANGELOG.md`; futuro → `ROADMAP.md`;
   design → este doc; assinaturas da engine → decompilação. Não duplique conteúdo entre eles.
3. **NUNCA edite dentro da pasta do jogo** (`D:\SteamLibrary\...\The RPG Engine\`). A fonte do mod
   vive em `C:\Users\danie\RPGEngine-CustomPartsMod\`. Editar a pasta do jogo arrisca o "verify
   integrity" da Steam apagar tudo. Instalar/remover deve continuar sendo **drop de pasta reversível**
   (`winhttp.dll` + `BepInEx\`), sem tocar em arquivo do jogo.
4. **Valide assinaturas da engine antes de confiar nelas.** Rode `ilspycmd` sobre
   `The_RPG_Engine_Data\Managed\RpgEngine.dll` (ref `.`) — updates do jogo movem métodos/campos.
   Pegadinha confirmada: `BuildTabsBase`/`BuildTabsWithPathButtons` estão no namespace **`RpgEngine`**
   (não `RpgEngine.Characters`); `PropDatabaseData.fullPath` é **computado** de `savePath` (nunca null).
5. **Depois de mudar código:** (a) rebuild → `dotnet build src/CustomPartsMod.csproj -c Release`;
   (b) copie a DLL para `The RPG Engine\BepInEx\plugins\CustomPartsMod\` **e** `dist\package\...\` e
   re-zipe o `dist`; (c) teste em jogo (abrir o Criador, importar um `.obj`, checar aba/preview/pintura);
   (d) atualize `CHANGELOG.md` (histórico), `ROADMAP.md` (o que sobrou) e o `last_updated` deste doc.
6. **Independência de idioma (princípio load-bearing).** Detecte/mapeie categoria por `savePath`
   (ids internos estáveis, ex.: `RiggedBodyParts`), **nunca** pelo `inGameName` exibido; rótulo do
   botão via `Translator`. Detalhes na Seção 6.

---

# Design de referência (seções 1–12)

## 1. Objetivo

Adicionar ao Criador de Personagens um botão **"Importar Parte"** que transforma um arquivo
**.obj / .glb** em uma parte de corpo selecionável (cabeça, mão, braço, elmo, ombro…),
que **persiste** para sempre nas opções da categoria e é **pintável em RGB** como as demais.

Instalar/remover deve ser um **drop de pasta reversível** que não altera arquivo nenhum da engine.

---

## 2. Arquitetura em 2 camadas

### Camada 1 — O mod (código)
- **Loader:** BepInEx 5 (Mono x64) + HarmonyX.
- **Instalar:** extrair 1 zip na raiz do jogo (cria `winhttp.dll` + `BepInEx/`).
- **Remover:** apagar `winhttp.dll` + `BepInEx/`. Nenhum arquivo do jogo é tocado → 100% reversível.
- **Não vai na Oficina** (Oficina só aceita `map/mesh/prefab`; DLL precisa de loader). Distribuição = zip.

### Camada 2 — As partes (conteúdo)
- Arquivos `.obj/.glb` + `.png` (ícone opcional) em pastas por categoria.
- Fontes varridas pelo mod:
  1. `…\The RPG Engine\CustomParts\<Categoria>\` (pasta local drop-in).
  2. (fase futura) pastas de itens **`mesh`** ASSINADOS na Oficina, via `Steamworks.Ugc.Item.Directory`.

---

## 3. Estrutura do projeto (fonte do mod)

```
RPGEngine-CustomPartsMod/
├─ DESIGN.md                      (este doc)
├─ CustomPartsMod.sln
├─ src/
│  ├─ CustomPartsMod.csproj       (net472, refs aos DLLs do jogo + BepInEx + Harmony)
│  ├─ Plugin.cs                   (BepInEx entrypoint; aplica Harmony; inicializa serviços)
│  ├─ config/
│  │  └─ ModConfig.cs             (pasta raiz de partes, hotkeys, defaults)
│  ├─ import/
│  │  ├─ PartImporter.cs          (obj/glb -> Mesh, via MeshImporter do jogo)
│  │  └─ PartFileStore.cs         (copiar arquivo p/ CustomParts\<cat>\, listar, ícones)
│  ├─ model/
│  │  ├─ CustomPart.cs            (metadados: id, categoria, caminho, slot, escala/offset)
│  │  └─ CustomPartCatalog.cs     (registro em memória; injeta em attachmentPaths)
│  ├─ attach/
│  │  └─ CustomBodyPartAttachment.cs   (subclasse de CharacterAttachment — rígida no socket)
│  ├─ paint/
│  │  └─ ShaderInherit.cs         (pega o shader/material do jogo de uma parte existente)
│  ├─ ui/
│  │  ├─ ImportButtonInjector.cs  (injeta o botão no CharacterCreator, ciente da categoria)
│  │  └─ TabRefresh.cs            (força o rebuild das abas após importar)
│  └─ patches/
│     ├─ AddPartPatch.cs          (Harmony em PickupableCharacter.AddPart)
│     ├─ AttachmentPathsPatch.cs  (injeta entradas custom em CharacterCreator.attachmentPaths)
│     └─ CreatorLifecyclePatch.cs (hook de abertura/edição do criador p/ montar UI)
└─ dist/
   └─ (saída empacotada: BepInEx/plugins/CustomPartsMod.dll + README de instalação)
```

> **Nota (2026-07-06):** o layout acima é o **desenho original**. A implementação real da Fase 1
> ficou "flat" em `src/` (sem subpastas) — ver o **Mapa do código-fonte** no topo para os nomes de
> arquivo atuais (`ImportButton.cs`, `ImportFlow.cs`, `ScaleSession.cs`, etc.).

---

## 4. Dependências / referências de build

Referenciar (sem copiar) de `…\The RPG Engine\The_RPG_Engine_Data\Managed\`:
`RpgEngine.dll`, `UnityUtils.dll`, `UnityUtils.Importers.dll`, `Siccity.GLTFUtility.dll`,
`Parabox.Stl.dll`, `SimpleFileBrowser.Runtime.dll`, `Facepunch.Steamworks.Win64.dll`,
`UnityEngine.*.dll`, `Assembly-CSharp.dll`.
Loader: `BepInEx.Core`, `0Harmony`. Target framework `net472`.

---

## 5. Modelo de dados da parte customizada

Cada parte = um `PropDatabaseData` sintético + um `CustomPart`:

| Campo | Valor |
|---|---|
| `savedId` | `custom_<categoria>_<nomeArquivo>` (único, estável) |
| `inGameName` | nome do arquivo (editável depois) |
| `savePath` / `tabs` | copiados de uma parte NATIVA da mesma categoria (garante cair na aba certa) |
| `fullPath` | chave "virtual" `CUSTOM://<savedId>` (marcador p/ o patch interceptar) |
| `visible` | true |

Layout em disco (drop-in):
```
CustomParts/
  Cabeças/   meuElmo.obj   meuElmo.png(opcional)   meuElmo.json(opcional: escala/offset/slot)
  Mãos/      ...
```
O `.json` opcional por parte guarda ajustes (escala, offset, rotação, slot `RiggedAttachType`,
canal de cor default). Sem ele, usa defaults sensatos.

---

## 6. Fluxo — Import por categoria (Fase 1)

> **Princípio: independência de idioma.** A engine é localizada e o nome exibido das abas muda
> conforme o idioma. Portanto o mod **nunca** identifica categoria pelo texto exibido (`inGameName`).
> As abas são agrupadas por `savePath` (identificadores internos estáveis, ex.: `RiggedBodyParts`),
> que **não** são traduzidos → toda detecção/mapeamento de categoria usa `savePath`/`tabs`.
> O rótulo do botão "Importar Parte" usa o `Translator` do jogo (`TranslateEnFallback`) para
> aparecer no idioma atual da engine.

1. `ImportButtonInjector` adiciona botão **"Importar Parte"** na UI do criador e lê a
   **categoria/aba ativa** (pelo `savePath` da aba, não pelo rótulo) no momento do clique.
2. Clique → `PartImporter` chama `MeshImporter.LoadMesh` (file browser já existente) filtrando `.obj,.glb`.
3. Arquivo escolhido é **copiado** por `PartFileStore` para `CustomParts\<CategoriaAtiva>\`.
4. `CustomPartCatalog.Register()` cria o `PropDatabaseData` sintético e o injeta em
   `CharacterCreator.attachmentPaths`.
5. `TabRefresh` re-monta as abas → a parte aparece na categoria.
6. (opcional) já seleciona a parte no boneco via `PickupableCharacter.AddPart(savedId)`.

### Patch central — `PickupableCharacter.AddPart(string partId)`
Hoje: `CachedResources.Load<CharacterAttachment>(fullPath).Instantiate(this)`.
Patch **Prefix**: se `partId` é custom (existe no `CustomPartCatalog`), construir em runtime um
`CustomBodyPartAttachment` a partir do `Mesh` importado, parentear no socket
(`RiggedAttachments.AttachmentPoint(slot)`), registrar em `attachedItems` e **pular** o método original.
Se não for custom, deixa o original rodar.

---

## 7. Fluxo — Persistência + pasta drop-in (Fase 2)

- Na abertura do criador (`CreatorLifecyclePatch`), `PartFileStore.ScanAll()` varre
  `CustomParts\*\` e registra tudo no catálogo → partes soltas na pasta viram opções permanentes.
- Ícone: usa `<parte>.png` se existir; senão gera um snapshot (a engine já tem `SnapshotHighjack`
  usado pelo `CustomMeshCreator`) — reaproveitável.
- Meshes carregados são cacheados em memória (espelhando o padrão `RpgMeshCache` do jogo)
  para não reimportar a cada seleção.

---

## 8. Fluxo — Pintura RGB (Fase 3)

- `CharacterAttachment.SetColor(id, color)` já faz `renderer.material.SetColor(id, color)`.
- `PickupableCharacter.SetColor(target, color)` já percorre **todas** as partes anexadas.
- Logo: se o material da parte customizada usar o **shader de personagem do jogo**, ela responde
  aos color pickers existentes **sem UI nova**.
- `ShaderInherit` pega, em runtime, o `Shader`/material-template de uma parte nativa já carregada
  (ex.: a primeira em `attachedItems`) e aplica ao renderer da parte customizada; mapeia os
  canais `Colors.Part` (primary, secondary, metalA/B, leatherA/B, emission, skin…).
- Default: parte nova usa `primary` como canal principal; usuário pinta com o picker que já existe.
- Fallback (se herdar shader falhar): tint simples via `_Color/_BaseColor` com um picker próprio.

---

## 9. Fluxo — Oficina (Fase futura, prioridade menor)

- Ler itens `mesh` assinados: enumerar via `Query.Items.WhereUserSubscribed()`, pegar
  `Item.Directory` de cada `IsInstalled`, varrer como fonte extra de partes.
- (Opcional) Botão "Publicar pack": empacotar uma pasta de `CustomParts\<cat>\` e enviar por
  `WorkshopManager.SaveToWorkshop` com tag própria do mod.

---

## 10. Níveis técnicos

- **Nível A (Fases 1–3, foco atual):** anexo **rígido** no socket do osso. Cobre cabeças, elmos,
  mãos, ombros, adereços. OBJ funciona 100%.
- **Nível B (futuro):** deformação com pele (braço/mão que dobra) → exige **GLB/FBX rigado** com
  nomes de osso batendo o esqueleto do jogo; monta `SkinnedMeshRenderer` + bind de ossos.

---

## 11. Riscos e manutenção

- **Multiplayer/save:** parte custom é **local**; sincronizar exige enviar os bytes do mesh como o
  jogo já faz p/ custom meshes (fica p/ depois).
- **Update do jogo:** patches por nome de método podem quebrar; conserto é localizado.
- **Steam verify integrity:** manter a fonte/dev FORA da pasta do jogo (este doc já está em `C:\Users\danie\`).
- **OBJ sem material/rig:** textura avançada e deformação pedem GLB.

---

## 12. Decisões (resolvidas)

1. **Prioridades:** Import por categoria + Persistência, e Pintura RGB. (Nenhum requisito extra pendente.)
2. **Categorias do MVP:** a definir na hora de codar (usuário ainda não vai codar).
3. **Idioma:** mod é **independente de idioma** — mapeia categoria por `savePath`, botão via `Translator`
   (ver princípio na Seção 6). Funciona em qualquer idioma que a engine esteja.
4. **Pasta de partes:** `…\The RPG Engine\CustomParts\` — **confirmado**.

## 13. Status (resumo — detalhe em CHANGELOG.md / ROADMAP.md)

> **Fonte da verdade:** `CHANGELOG.md` (histórico, Iterações 0–6 + limitações de hoje) e
> `ROADMAP.md` (o que falta, P0–P11). Este resumo é só um ponteiro; não duplicar detalhe aqui.

- **Fase 0 (scaffold) — CONCLUÍDA.** `net472`, refs diretas aos DLLs do jogo + BepInEx 5.4.23.2,
  `Plugin.cs` com `Harmony.PatchAll`. Compila 0/0.
- **Fase 1 (import por categoria, em memória) — CONCLUÍDA e verificada contra o DLL decompilado.**
  Empacotada em `dist/CustomPartsMod-v0.1.0.zip` e **instalada** no jogo (aditivo/reversível).
  Iterações posteriores (2–6): botão de texto real, substituição de slot, escala compensada,
  painel `ScaleSession` + persistência de escala/offset, texturas (`TextureLoader`), lixeira por
  item e ids estáveis. Ver `CHANGELOG.md`.
- **P0 (persistência de malha) resolvido** para OBJ/STL (Iteração 9); **GLB no reload** ainda pendente.
- **Iteração 10:** P5 (rotação), P4 (escala não-uniforme), P6 (reedição + padrão/só-desta-vez),
  P3 (feminino/masculino), P12 (setas de navegação). Detalhe em `CHANGELOG.md`.
- **P1 (import em massa) resolvido** (Iteração 15): botão "Importar Pasta" importa todos os `.obj`
  de uma pasta de uma vez, com textura pareada automaticamente e persistência imediata por item.
- **P9 (filtro "só custom") + P13 (variações de textura) resolvidos** (Iteração 16): toggle que
  esconde as nativas; caixas 1–5 de textura por peça, ao vivo e persistidas.
- **Próximo (pendentes):** P7 (miniatura), P10 (pastas/tags), P11 (gizmo arrastável), P14
  (acessórios estilo Sims), P15 (modo aleatório só com peças custom + travas por categoria). Ordem e
  viabilidade em `ROADMAP.md`. (P8 removido do roadmap — desnecessário.)
