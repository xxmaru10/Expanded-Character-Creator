# Roadmap — Custom Body Parts

Lista de features pedidas, ainda **não** implementadas. Ordem = sugestão de prioridade
(as de cima destravam as de baixo). Cada item tem uma nota técnica de viabilidade.

Legenda de esforço: 🟢 pequeno · 🟡 médio · 🔴 grande/incerto (precisa de rodadas de teste).

---

## P0 — Persistência de malha entre sessões  ✅ FEITO (Iteração 9)
- `Confirmar` salva no `scales.json` o **caminho do modelo + categoria + slot**. Na abertura do criador,
  `PersistenceLoader.LoadAll` recarrega cada modelo salvo (malha silenciosa via `MeshImporter.ImportNew`),
  registra na aba com escala/posição/textura. A parte **sobrevive ao reiniciar**.
- **Limitações:** só OBJ/STL no reload (GLB pendente — precisa do caminho async); registros antigos
  (pré-P0, sem caminho salvo) precisam de **um** Confirmar novo pra virarem persistentes.
- **Alternativa futura:** pasta drop-in `CustomParts\<Categoria>\` varrida no início (Fase 2 do DESIGN).

## P1 — Importação em massa (pasta inteira)  ✅ FEITO (Iteração 15)
Botão "Importar Pasta" (ao lado de "Importar Parte") → abre um **painel de convenções** onde o
usuário fixa **gênero** (filtro Ambos/Feminino/Masculino), **canal** (pele/roupas/etc.), **escala ×**,
**escala por eixo**, **rotação** e **posição**; então escolhe uma pasta e importa todos os `.obj`
dela — cada modelo herda essas convenções (GLB fica de fora, reload silencioso ainda não suporta,
ver P0). A **textura** é a única coisa não configurada: é pareada automaticamente por arquivo
(`TextureLoader.FindSibling`: `.mtl`/`map_Kd` ou PNG/JPG de mesmo nome). Escala é multiplicador sobre
a normalização de cada malha, então tamanhos diferentes caem consistentes. Cada item é **persistido
de imediato** em `scales.json` (mesmo registro que "Confirmar" gravaria), sobrevivendo a um restart
sem abrir o painel item a item.
- **Arquivos:** `MassImportButton.cs` (botão) + `MassImportConfig.cs` (painel de convenções, semeado
  do padrão da categoria P6 + canal auto) + `MassImportFlow.cs` (coroutine: `FileBrowser.PickMode.Folders`
  → `Directory.GetFiles(*.obj)` → `Compat.ImportMesh` por arquivo, aplicando `MassImportSettings`) +
  `PanelUi.cs` (helper de painel compartilhado, extraído do padrão do `ScaleSession`).

## P1b — Últimos valores viram o padrão do próximo import  ✅ FEITO (Iteração 8)
- Import de modelo novo = **escala normalizada × último multiplicador** + último offset. `Confirmar`
  salva por-modelo (absoluto) **e** o global `__last__` (multiplicador + offset) no `scales.json`.
- Decisão: **híbrido** (normalizar pro ballpark + lembrar o ajuste como multiplicador) — porque os
  modelos vêm de fontes/escalas variadas às vezes, então o "último absoluto" puro quebraria.
- **Falta (quando fizer a P1):** o import em massa consumir esses valores por-item.

## P2 — Canais de pintura por tipo de parte  ✅ FEITO (Fatias 1, 2 e 3 — Iterações 11–13)
> Design final (usuário, 2026-07-06): cada peça amarrada a **UM canal**; a tinta é **textura × cor**
> (mantém sombras); textura é o padrão e só pinta quando o usuário mexe no picker; **reabrir a
> engrenagem reverte** pra textura. Pincel de máscara por área fica pra depois (complexo demais).

**Mapa categoria → canal** (em `ChannelMap.cs`):
- torso→`_Color_Primary` · pernas→`_Color_Secondary` · botas→`_Color_Leather_Primary` ·
  joelhos/ombreiras/cotoveleiras→`_Color_Leather_Secondary` · braço/antebraço→`_Color_Metal_Primary` ·
  mãos→`_Color_Metal_Secondary` · capacete→`_Color_Metal_Dark` · acessórios/chifres→`_Emission` ·
  cabelo/sobrancelhas→`_Color_Hair` · olhos→`_Color_Eyes` · pele→`_Color_Skin`. (ignorar stubble/scar/bodyArt)

**Como funciona (validado no DLL):** o picker chama `PickupableCharacter.SetColor("_Color_X", cor)`
(broadcast, só na ação do usuário — o `SetAllOn` inicial aplica direto na peça, sem broadcast). Patch
`Patch_Paint` intercepta esse broadcast e, na peça custom cujo canal bate, faz `ApplyPaint` (tinge o
`_BaseColor` do material lit = textura × cor). `Patch_AttachmentColor` silencia o `SetColor` base nas
peças texturizadas (o material lit não tem `_Color_*`). `PartEditor.Reopen` chama `ResetPaint` (volta textura).

- **Fatia 1 — FEITA:** mapa por categoria + tinta textura×cor + reverter na engrenagem (auto por categoria).
- **Fatia 2 — FEITA (Iteração 12):** seletor de **Canal** no painel (botão que cicla todos os canais, cobre
  o caso pele/roupa e permite corrigir qualquer mapeamento) → `CycleChannel` atualiza `Part.ChannelId` ao
  vivo; override persistido por-modelo (`channel` no `scales.json`), usado no import e no reload.
- **Fatia 3 — FEITA (Iteração 13):** categoria **"Olhos"** própria — botão navegável com **ícone de olho
  desenhado em código** (`EyeIcon`), `savePath` sintético único (`EyesCategory.Path`), socket `face`, canal
  `_Color_Eyes` (o picker de olhos já cai no `Patch_Paint`, validado no DLL). Import/persistência reusados.
- **Futuro:** pincel de máscara por área (multi-canal) — 🔴 grande/arriscado, adiado a pedido do usuário.

## P3 — Feminino / Masculino  ✅ FEITO (Iteração 10)
Marcar a parte como feminina/masculina. A engine já filtra por tags "Feminine"/"Masculine"
(`FilterExcludeByTags` no criador) — basta gravar a tag no `PropDatabaseData.tags` da parte custom.
- **Feito:** seletor "Gênero: Ambos/Feminino/Masculino" no painel → `CustomPartCatalog.SetGender` grava a
  tag e dá `Refresh`; persistido por modelo (`gender` no `scales.json`). Verificado no DLL: modo masculino
  chama `FilterExcludeByTags(["Feminine"])`, modo feminino `["Masculine"]` (tag = o gênero que **oculta** a peça).

## P4 — Escala não-uniforme (X, Y, Z)  ✅ FEITO (Iteração 10)
Além da escala única, permitir esticar em X/Y/Z separadamente.
- **Feito:** `UserScaleAxis` (Vector3) como **multiplicador por eixo sobre** a escala uniforme (1/1/1 = sem
  esticar), então a normalização/último-multiplicador híbridos continuam operando na escala uniforme. 3
  campos "Esc XYZ" no painel; guarda contra 0/negativo; persistido (registros antigos → 1/1/1).
- **Nota sobre o offset:** 1 unidade em Y "joga a peça longe" porque o offset é em **unidades de mundo**
  (personagem ~2 unidades) — é esperado usar decimais; comportamento aceito, sem plano de mudança.

## P5 — Rotação X/Y/Z digitável  ✅ FEITO (Iteração 10)
Campos de rotação no painel (caso a peça venha girada pela orientação do osso).
- **Feito:** `UserEuler` aplicado como `Quaternion.Euler` **relativo ao osso**; o pivô é recompensado no
  `Reapply` para a peça **girar em torno do próprio centro** (não voar longe). 3 campos "Rotação"; persistido.

## P6 — Botão de reedição + "padrão" vs "só desta vez"  ✅ FEITO (Iteração 10)
> [23:39, 05/07] "Botão de reedição; tem um 'salvar como padrão' (aquela parte vai ser sempre assim)
> ou só ajustar para que aquela parte seja daquele modo só daquela vez."
- **Feito — reabrir edição:** botão **"E"** (azul) por parte custom, ao lado da lixeira, reabre o
  `ScaleSession` (`EditButton` → `PartEditor.Reopen`: aplica a parte se preciso e liga o painel à instância).
- **Feito — dois modos de aplicar:**
  - **Salvar padrão** → grava (a) o registro **por-modelo** (aquele arquivo nasce sempre assim; usado por P0
    e ao reimportar o mesmo arquivo) **e** (b) o **padrão da aba/categoria** (`__cat__<savePath>`, cada aba o
    seu), então **todo modelo novo** importado naquela aba (ex.: toda cabeça) já vem com escala
    (multiplicador)/eixo/rotação/posição daquele padrão. Gênero e textura ficam por-modelo. (Iteração 10.3)
  - **Só desta vez** → aplica na instância atual **sem** persistir (o próximo import do mesmo arquivo
    volta ao padrão salvo). Botão "Só desta vez" no painel.

## P7 — Miniatura (retrato) do modelo em vez do nome  ✅ FEITO (Iteração 27)
Hoje a opção mostrava o texto "cabeça"/"corpo". Agora, ao clicar em **Confirmar**, o mod tira um
retrato do personagem (com a peça aplicada) e usa como **ícone do botão**.
- **Feito:** reaproveita `CharacterCreator.SnapShot()` (o mesmo render transparente 512×512 que a engine
  usa pros próprios tokens/mesh, via `CharacterCreatorCamera.DoSnapShot`). `IconButton` pinta o ícone no
  postfix da lista (`button.icon.SetImage` + esconde o texto); `ThumbnailStore` grava PNG em
  `CustomParts\thumbs\` e recarrega no `Register` (persiste entre sessões); `PartsAdmin.Delete` limpa.
- **Arquivos:** `ThumbnailStore.cs`, `Thumbnailer.cs`, `IconButton.cs` + `CustomPart`/`Patches`/
  `ScaleSession`/`CustomPartCatalog`/`PartsAdmin`. Detalhe em CHANGELOG It.27.
- **Limitação v1:** o retrato é do **personagem inteiro** (pose de token — ótimo pra cabeças/rostos).
  Isolar só a peça no ícone e gerar miniatura no import em massa ficam como refinamentos futuros.

## P9 — Filtrar por ativo customizado em cada parte  ✅ FEITO (Iteração 16)
Botão de alternância "Só custom: SIM/NÃO" (abaixo de "Importar Pasta") que esconde as peças nativas e
mostra só as importadas, na categoria aberta.
- **Como:** postfix no próprio seletor de aba da engine (`BuildTabsWithPathButtons.Filter`,
  `Patch_CustomOnlyFilter`): quando ligado, derruba todo `id` que não é do `CustomPartCatalog`.
  Compõe com os filtros de caminho/gênero/busca; as setas P12 (que reusam o mesmo `selector`) passam
  a andar só na lista reduzida. Gated ao `creator.itemTabsLoader` p/ não afetar outros tab systems.
  Estado só na sessão (`CustomFilter.CustomOnly`).
- **Arquivos:** `CustomFilter.cs` (estado + patch) + `CustomFilterButton.cs` (botão).
- **Nota:** dispensou tag própria + `BuildTabsWithFilters` (a ideia original do roadmap) — o postfix
  no selector é mais direto e não polui o `PropDatabaseData`.

## P10 — Tags (temas) dentro do criador  ✅ FEITO (Iteração 22, revisto na 23 → TAGS)
Criar uma tag "Vampiro", importar com ela ativa (a peça leva a tag), e filtrar a lista por vampiro em
cabeças, torsos, etc. (usuário corrigiu: é **tag**, não pasta).
- **UI inline (It.24, pedido do usuário):** barra `TagBar` **abaixo da caixa de busca** — um **"+"** cria
  tag (digita + OK), cada tag é um **chip** clicável. Clicar num chip **seleciona** = filtra a lista
  àquela tag E marca os próximos imports com ela ("se estiver nesse filtro, o item vai pra ela").
  Clicar no chip selecionado de novo desmarca. Sem painel/botão separado.
- **`TagManager`:** um só `SelectedTag` (filtro + alvo de import; `ActiveTag` é alias p/ ImportFlow);
  `KnownTags` (sessão) faz a tag nova aparecer na hora; `AllTags` = KnownTags ∪ tags-em-uso (persistidas
  reaparecem no reload). A tag é guardada por-modelo em `CustomPart.Tag` (+ `scales.json` `tag`).
- **Como filtra:** `Patch_TagFilter` = 2º postfix no MESMO `BuildTabsWithPathButtons.Filter` do P9;
  filtro ativo → só peças custom cujo `Tag` bate. Compõe com P9, gênero, busca, setas P12.
- **`TagBar` posicionamento:** parenteado ao pai da `SearchBar`, `SetAsLastSibling` (desenha sobre a
  grade), `Follow()` cola o topo-esquerdo sob a busca via `GetWorldCorners` (independe de pivô/timing).
- **Decisão:** a tag NÃO entra no `savePath`/`CategoryPath` (dirige slot/canal) — a engine não cria
  sub-abas pra savePaths novos. Fica ao lado, só pra tag + filtro. Seleção é só-sessão; tag na peça persiste.
- **Arquivos:** `TagManager.cs` (estado + `Patch_TagFilter`), `TagBar.cs` (barra inline),
  `CustomPart.Tag`, `ScaleStore` (`tag`), import/reload/Confirm setam+persistem.

## P11 — Pegar e arrastar a peça (meio-termo do gizmo)  ✅ FEITO (Iteração 17)
Versão "pegar e arrastar" (sem as setas 3D coloridas): checkbox **"Ativar modo modelagem"** no painel
de edição; com ele ligado, arrastar a peça no preview a manipula, e as teclas **2 = Posição**,
**3 = Grossura** (escala uniforme), **4 = Rotação** trocam o modo — o chip do modo ativo fica destacado
em verde. Enquanto o modo está ligado, um `UiButton` transparente sobre o preview absorve o clique do
SlickUi para a **câmera não girar** durante o arraste (some ao desligar).
- **Conversão mouse→mundo (`ScaleSession`):** posição do mouse → ponto local no `inputPanel` → viewport
  0..1 → `creatorCam.cam.ViewportPointToRay` → interseção com um plano de frente pra câmera na
  profundidade do centro da peça. **Posição:** o delta mundo é somado via
  `CustomBodyPartAttachment.AddWorldOffset` (rotaciona pro frame do osso, onde o `UserOffset` vive).
  **Grossura:** `UserScale *= exp(Δy·0.004)`. **Rotação:** Δx→Y, Δy→X (0.4°/px). Cada arraste reusa os
  mesmos `SetUserOffset/SetUserScale/SetUserEuler` (pivô recentralizado já existente) e atualiza os
  campos digitáveis, então "Salvar padrão"/"Só desta vez" persistem igual.
- **Falta (polimento futuro):** as setas X/Y/Z coloridas por cima (precisão por eixo) — a base
  mouse→viewport→raio já está pronta pra isso. Enquanto o modo está ligado a câmera não gira (desligue
  a checkbox pra voltar a girar).

## P11-antigo — Gizmo de setas (arrastar com o mouse)  🔴 (substituído pelo meio-termo acima)
Setas X/Y/Z coloridas arrastáveis + tecla pra alternar mover/escalar/rotacionar, **como no jogo**.
- **Por que não dá pra reusar o nativo:** `LocatorMotion.SetupPlaneCaster` usa `Camera.main` fixo;
  `LocatorController.target` é um `Pickupable` de cena (guid/rede/NavMesh).
- **Viável? Sim.** `UniqueMono<CharacterCreatorCamera>.instance.cam` é a câmera de preview (dá pra
  `ViewportPointToRay`). `rotCore`, `inputPanel` e `DoSnapShot()` (útil p/ P7) também estão lá.
- **Complicação:** o preview é renderizado numa **RenderTexture** (`preview`) mostrada num painel de UI,
  **não** direto na tela. Então mouse→mundo precisa de um passo extra: posição do mouse → rect do
  painel de preview (`inputPanel`) → viewport 0..1 → `cam.ViewportPointToRay`. Fazível, mas fino — e
  como não vejo o resultado, são várias rodadas de acerto.
- **Meio-termo mais barato (recomendado 1º):** "**pegar e arrastar**" a peça com o mouse (sem setas):
  clicar no preview e arrastar → move num plano de frente pra câmera; tecla p/ trocar mover/rotacionar/
  escalar. Usa o mesmo mapeamento cam+painel, mas pula a geometria das setas 3D. Entrega o essencial
  ("mexer com o mouse") com ~metade do risco. As setas coloridas viram polimento por cima.

## P12 — Setas de navegação rápida ao lado das partes  ✅ FEITO (Iteração 10)
> [23:38, 05/07] "Setas ao lado das partes para passar rapidamente pelas opções."
Setas ◀ ▶ (na UI do criador, perto do preview ou da aba) que **avançam/voltam** pelas opções da
categoria atual, aplicando cada uma na hora — trocar de cabeça sem procurar na lista.
- **Feito:** `NavArrows` injeta ◀ ▶ abaixo do "Importar Parte". Em vez de reimplementar os filtros, reusa
  `creator.itemTabsLoader.tabSystem.selector` (o `Predicate<string>` que a engine já usa) sobre
  `CharacterCreator.attachmentPaths` → lista as visíveis **na ordem da tela**, acha a atual no dummy e
  aplica próxima/anterior via `SpawnAlongside` (que é toggle: desliga a atual, liga a alvo). Nativas **e** custom.

## P13 — Variações de textura por parte (caixas 1–5)  ✅ FEITO (Iteração 16)
> [23:43, 05/07] "Cada parte aceitar variações de textura; você coloca mais de uma textura e elas
> preenchem caixas 1,2,3,4,5 e você clica pra selecionar uma diferente."
- No painel de edição, linha "Texturas" com **caixas numeradas** (até 5): clicar numa preenchida
  troca a textura ao vivo (`SetTexture`); clicar na primeira vazia ("+") abre o file browser e
  adiciona; a ativa fica destacada em verde. `CustomPart.TextureVariants` (lista sem buracos) +
  `ActiveVariant`, persistidos no `scales.json` (`texVariants`/`texVariant`) e restaurados no
  import/reload. Substitui o antigo botão "Textura…".
- **Arquivos:** `ScaleSession.cs` (UI + persistência), `CustomPart.cs`, `ScaleStore.cs`,
  `ImportFlow.cs`/`MassImportFlow.cs`/`PersistenceLoader.cs` (seed/reload da lista).
- **Nota:** fizemos caixas próprias no painel em vez de reaproveitar `hasTextureVariants`/`tintable`
  da engine (mais simples de controlar ao vivo). Reusa `TextureLoader` + `SetTexture`.

## P14 — Acessórios por parte do corpo, estilo The Sims (encaixe)  ✅ FEITO (Iteração 20)
> [23:47, 05/07] "Acessórios de acordo com a parte do corpo, estilo The Sims, só vai encaixando."
Acessórios anexam **por cima** da peça base (não substituem o slot) e **empilham** — reusa toda a
máquina aditiva feita pros olhos (`CustomPart.Additive`, `AdditiveParts`, pular `RemoveAllChildren`).
- **Auto por categoria** (`AccessoryMap.IsAccessory`): `Full_Helmets`/`helmet`/`helmetAdditions`/
  `shoulders`/`attachments`/`extras` nascem aditivos; `AccessoryMap.ResolveAdditive(cat, override)`
  centraliza a decisão (override manual > auto = olhos + acessórios) e é chamado nos 3 pontos de criação
  (`ImportFlow`/`MassImportFlow`/`PersistenceLoader`), no lugar do antigo `EyesCategory.Is`.
- **Override manual por-modelo:** botão **"Encaixe: Auto/Acessório/Substitui"** no `ScaleSession` cicla o
  modo; `ApplyAdditiveMode` re-anexa ao vivo (remove+readiciona via `SpawnAlongside` → `Build` roda com o
  novo flag) e re-liga o painel à nova instância. Persistido por-modelo (`additive` int no `scales.json`;
  `Confirm` grava; import/reload usam o override salvo). 0=auto, 1=acessório, 2=substitui.
- **Limitação conhecida:** trocar replace→acessório num part que já removeu a base (ex.: uma cabeça) NÃO
  traz a base de volta (é só re-selecionar). Acessórios de slot próprio (capacete/ombro/costas) não têm
  esse problema. Sub-slots dedicados (dois chapéus coexistirem) continuam como polimento futuro.

## P15 — Modo Aleatório Personalizado (só peças custom, com travas por categoria)  ✅ FEITO (Iteração 18)
Botão **"Aleatório"** (empilhado com os de import) abre o `RandomPanel`: uma linha de trava por
categoria que tem peça custom (clique alterna `[ ] Travar` ↔ `[X] Travado`, cor muda) + botão
**"Aleatorizar"** que re-sorteia toda categoria destravada (só peças custom) + "Fechar".
- **Arquivos:** `Randomizer.cs` (agrupa `CustomPartCatalog.AllParts()` por `CategoryPath`, nome amigável
  PT por segmento de savePath, trava em `HashSet` só-sessão, `Randomize()` sorteia e aplica) +
  `RandomPanel.cs` (UI via `PanelUi`, reconstruído a cada abertura p/ pegar categorias novas) +
  `RandomButton.cs` (injeta o botão) + `CustomPartCatalog.AllParts()`.
- **Como aplica:** por categoria destravada, acha a peça custom aplicada no dummy, sorteia uma do grupo;
  se diferente, remove a antiga (`SpawnAlongside` toggle — cobre aditivas como olhos, que o replace-por-
  slot do `Patch_AddPart` de propósito não tira) e liga a sorteada. Só entram categorias com ≥1 peça
  custom (nunca cai em nativas). Travas não persistem em disco (preferência de uso).

## P15-original — Modo Aleatório Personalizado (só peças custom, com travas por categoria)  🟡
Um botão "Aleatorizar" que monta o personagem sorteando **só entre as peças customizadas**
(nunca peças nativas do jogo) — uma por categoria (cabeça, corpo, mãos, etc.). Um painel com
**checkbox por categoria** ("Travar Cabeça", "Travar Corpo"...) deixa o usuário fixar quais partes
NÃO devem mudar no sorteio; as destravadas são resorteadas a cada clique.
- **Escopo:** só categorias com pelo menos 1 peça custom registrada entram no sorteio (uma categoria
  sem peça custom fica sempre com o que já está posto — não tem native pra sortear).
- **Nota técnica:** `CustomPartCatalog` já sabe listar as próprias (`IsCustom`/`TryGet`); falta
  agrupá-las por categoria (mesmo agrupamento do `CategoryMap`/`ChannelMap`, por segmento de
  `savePath`). Aplicar o sorteio reusa o mesmo caminho de `NavArrows.Step`
  (`creator.SpawnAlongside(id)` liga/desliga por slot) — só troca "próximo/anterior" por "índice
  aleatório dentro da lista filtrada para custom". As checkboxes de trava persistem só na sessão
  (não precisa salvar em disco: é preferência de uso, não dado da peça).
- **Depende de:** ter peças custom importadas nas categorias desejadas (senão não há o que sortear).

---

### Dependências (o que destrava o quê)
- **P0** destrava P1, P6, P7, P10, P13 (tudo que precisa a peça existir/persistir depois de reiniciar).
- **P1b** destrava a **P1** (import em massa herda os últimos valores) — pedido como prioridade ("de cará").
- **P2** depende de um **modo híbrido de shader** (textura como albedo + canais de cor do personagem);
  esse mesmo modo híbrido é pré-requisito de qualquer pintura em parte texturizada.
- **P4/P5/P12** são extensões diretas do painel/criador atual (rápidas).
- **P14** precisa do **modo aditivo** de anexo (não substituir o slot).
