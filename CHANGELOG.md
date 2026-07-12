# Changelog — Custom Body Parts (The RPG Engine mod)

Formato: cada bloco é uma iteração de trabalho. Versão do plugin: **0.1.0** (em desenvolvimento).
Loader: BepInEx 5.4.23.2 (Mono x64) + HarmonyX. Alvo: Unity 2021.3.30 / `net472`.

---

## Iteração 40.1 (2026-07-12) — Chip da tag "ts4" não aparecia (bump v0.1.2)

Sintoma: "A tag ts4 sumiu — todos os meus modelos deveriam ser dessa tag." Dado **intacto no disco**:
`scales.json` tem `"tag":"ts4"` em todos os 15.280 modelos reais (só os 21 registros de config têm tag
vazia). Era exibição, não perda de dado.

Causa (confirmada no `LogOutput.log`): em `Patch_CreatorUi.Postfix` a ordem é `TagBar.Ensure` (linha de log
"Barra de Tags posicionada") **antes** de `PersistenceLoader.LoadAll` ("15280 modelo(s) registrado(s)").
`TagBar.Ensure` chama `Rebuild()`→`BuildChips()`→`TagManager.AllTags()` UMA vez, e nesse instante nenhuma
peça está registrada ainda → `AllTags()` (= KnownTags ∪ tags-nas-peças) volta vazio → nenhum chip. A barra
só reconstruía em interação do usuário (criar/selecionar/apagar tag), nunca depois do load. Como a tag ts4
vive só nas peças carregadas (foi aplicada por script no scales.json na It.37, sem KnownTags), o chip nunca
era desenhado numa sessão nova. Bug PRÉ-EXISTENTE, exposto agora.

Correção: novo `TagBar.Refresh()` (estático, reconstrói o `_current` se existir). Chamado (a) ao fim de
`Patch_CreatorUi.Postfix`, **depois** de `PersistenceLoader.LoadAll`, para repovoar os chips com as peças já
registradas; e (b) dentro de `TagManager.Refresh()`, pra a barra seguir qualquer mudança de tag (inclui o
editor de tag do painel da It.40). Versão do plugin subida para **0.1.2** (BepInPlugin + csproj + README +
log). Build 0/0, implantado (plugins + dist + `CustomPartsMod-v0.1.2.zip`).

---

## Iteração 40 (2026-07-12) — Brincos/colares fora de Acessórios · editar tag no painel · remove "Gerar miniaturas" · guia de distribuição

Quatro pedidos do usuário, build 0/0, implantado (plugins + dist + zip).

1. **Brincos e colares saem da aba Acessórios (só chapéus ficam lá).** Brincos/colares têm savePath sob
   `extras/attachments/...`, então o filtro de prefixo da engine os mostrava sempre que a região nativa
   "Acessórios" abria. Novo `SubcategoryFilter.cs` (`Patch_SubcategoryFilter`, postfix no mesmo
   `BuildTabsWithPathButtons.Filter` do P9/P10): uma peça cujo **leaf de categoria** é `brincos`/`colares`
   fica **oculta** a menos que o `pathPartsFilter` atual nomeie explicitamente esse leaf — ou seja, só
   quando o usuário clica a sub-aba **Brincos/Colares** (que chama `SetPathFilter([… ,"brincos"])`). Em
   qualquer outra visão (Acessórios, "Tudo", etc.) elas somem. **Sem migração de scales.json** — é só
   código; lê o caminho atual via `Compat.GetPathFilter`. Os botões Brincos/Colares já ficam na fileira de
   sub-abas da cabeça (`Head/TabHeaders`), então o resultado é "brincos/colares só nas sub-abas da cabeça".

2. **Editar a tag de um modelo pelo painel de edição.** Nova linha "Tag" na aba **Opções/3D** do
   `ScaleSession`: campo de texto + **Aplicar** + **Sem tag**. `ApplyTag/ClearTag/SetTag` atualizam
   `part.Tag`, persistem **na hora** via `ScaleStore.TryUpdateTag` (sobrevive ao reload mesmo sem Confirmar),
   registram a tag em `TagManager.NoteTag` (novo helper — adiciona a KnownTags **sem** trocar o filtro
   selecionado) e dão Refresh pra o filtro de tag reavaliar. `_tag` inicia de `part.Tag`; `_tagField`
   entra no `AnyFieldFocused` (não sequestra atalhos ao digitar). Aba 2 cresceu 248→288 de conteúdo.

3. **Removido o botão + função "Gerar miniaturas".** Apagados `ThumbnailButton.cs` e `ThumbnailCycler.cs`;
   removida a chamada `ThumbnailButton.Ensure` em `Patch_CreatorUi` e a config `Plugin.ThumbnailBatch`.
   As guardas `!ThumbnailCycler.Busy` (em `Patch_SpawnAlongside_VariantBar` e `Thumbnailer.Refresh`)
   viraram incondicionais. **Mantida** a foto automática ao aplicar/Confirmar uma peça
   (`Thumbnailer.Capture`) — é o caminho confiável e dá ícone às peças recém-importadas sem passo manual.
   (A biblioteca cheia já foi gerada offline via Blender na It.36.)

4. **Guia de como disponibilizar o mod de novo:** novo `DISTRIBUIR.md` (raiz) — separa **distribuir a
   FERRAMENTA** (recompilar → copiar DLL pro `dist\package` → `Compress-Archive` do conteúdo →
   GitHub Release/zip) de **compartilhar as SUAS PEÇAS** (pasta `CustomParts\` + `scales.json`, com o
   aviso de que os caminhos no `scales.json` são **absolutos**). README.txt aponta pra ele.

**Suporte a inglês mantido:** novas strings ("Tag", "Aplicar", "Sem tag", "Tag aplicada:",
"Tag removida.", e os toasts de Brincos/Colares) adicionadas ao dicionário PT→EN do `Loc.cs`.

---

## Iteração 38 (2026-07-11) — Corrige "Salvar modelo local" travado + modelos ausentes no menu de construção

Sintoma: apertar Confirmar/Salvar modelo local (ou o prompt de substituir) não fazia nada — o diálogo
não fechava, o modelo não salvava e os modelos salvos recentemente não apareciam na pesquisa do menu de
construção.

Causa (confirmada no `Player.log`): `NullReferenceException` em
`UnityEngine.Behaviour.get_isActiveAndEnabled ← BuildTabsBase.AddToTabs<T> ← AddToTabs_RPGCharacter ←
PrefabUploader.SaveToLocal ← Prompt.onYes`. `AddToTabs<T>` percorre o `public static HashSet<BuildTabsBase>
tabSystems` e, para cada aba que `is IBuildTabs_Character` (`BuildTabsLoader_Props` +
`BuildTabsLoader_CharacterTemplates`), roda `InitialiseCharacter` e então lê `tabSystem.isActiveAndEnabled`.
Uma aba **destruída** que ficou no HashSet (menu de templates/props fechado cujo OnDestroy não a removeu)
ainda passa no `is T`, então o loop toca `isActiveAndEnabled` num objeto Unity morto → NRE. Em
`SaveToLocal` a ordem é `RpgCharacterCache.Write(newItem)` (GRAVA EM DISCO) → `AddToTabs_RPGCharacter`
(estoura) → popup de sucesso: por isso o arquivo é gravado mas fica invisível na aba viva até um reload, e
o prompt trava. **Não é bug do mod** (o mod nunca escreve em `tabSystems`) — é um bug latente da engine.

Correção: novo `SavePromptFix.cs` (`Patch_SaveModelTabPrune`) — prefixo Harmony nos 4
`AddToTabs_RPGCharacter/RPGProp/Mesh/Token` que faz `BuildTabsBase.tabSystems.RemoveWhere(t => t == null)`
antes (o `==` do Unity trata MonoBehaviour destruído como null), então o foreach da engine só vê abas
vivas. Faxina defensiva do registro; também protege salvamentos vanilla de prop/mesh/token. Build 0/0.

---

## Iteração 38.1 (2026-07-11) — Óculos/acessório invisível no MAPA (vazamento de layer 21)

Sintoma: óculos aparecem no Criador mas somem no mapa (e um dos sapatos "some/troca"). O `[diag]`
mostra os óculos ANEXADOS na cabeça a sessão inteira (seguem o osso Head) — logo é layer: o preview do
Criador renderiza a **layer 21**; a câmera do mapa NÃO. Causa: `AdditiveParts.Reapply` fixava
`SetLayer(att.gameObject, 21)` em toda peça aditiva re-adicionada. `CustomBodyPartAttachment.Build` já
põe a layer certa (`go.layer = layer do osso pai` — 21 no Criador, padrão no mapa). Reapply roda em
QUALQUER personagem (postfix após todo `AddPart`, incl. spawn no mapa) e o `Active` carrega o id dos
óculos vindo da sessão do Criador → no mapa forçava layer 21 = invisível. Correção: Reapply herda
`att.transform.parent.gameObject.layer` (a layer do osso) em vez de 21. Também ajuda o sapato aditivo
(que sumia por layer 21). Posição/"troca" do sapato ainda não 100% diagnosticada — sapatos carregam o
registro CERTO no mapa (match exato, sem `fallback ->`); se persistir, precisa de log novo do mapa
(osso LowerLeg compartilhado sapato+panturrilha e auto-link L/R são os suspeitos). **Lição: nunca fixar
layer 21 numa peça custom; sempre herdar a layer do osso pai — funciona no Criador (21) E no mapa.**

---

## Iteração 36 (2026-07-11) — Miniaturas OFFLINE via Blender + giro das pernas

Gerar miniatura in-game travava/fechava o jogo mesmo em lotes; solução: gerar FORA do jogo. Novo
`tools/gen_thumbnails.py` (Blender 4.1 headless) lê o `scales.json` (utf-8 errors=replace, mesmo U+FFFD
do jogo), resolve o .obj+textura (glob `�`→`*`), renderiza 512×512 transparente (EEVEE, material
emission/unlit com a textura, câmera ortho enquadrada nos bounds) e salva como `thumbs/<key
sanitizada>.png` — o MESMO nome que o `ThumbnailStore` procura (validado: acentuado bate). Opções:
`--game --only-missing/--all --limit --grep --fill --yaw --pitch --out`. Usuário confirmou in-game: as
geradas aparecem perfeitas.
- **Giro das pernas:** coxa/panturrilha vinham de LADO (o "front" do OBJ de perna aponta pro lado, não
  como o torso). Adicionado `LEG_YAW` por-slot: `kneeR`/`legLowerR` +90°, `kneeL`/`legLowerL` −90°
  (esquerda e direita são espelhadas → giram ao contrário; confirmado: coxa direita +90 = frente, a
  esquerda no +90 = costas). Torso/braços inalterados (já estavam de frente).
- **Fluxo de limpeza:** movidas 445 miniaturas vazias (4655 bytes, do gerador in-game) pra
  `thumbs_blank_backup/`. Backfill completo `--all` rodando em background pra sobrescrever as ruins
  in-game pelas do script; as pernas serão regeneradas com o yaw novo DEPOIS do backfill (senão dois
  Blender competem no mesmo folder).

---

## Iteração 35.1 (2026-07-11) — Lote de miniaturas: 20 → 1000

Pedido do usuário: gerar de 1000 em 1000. Mudado `ThumbnailBatch` default (código `Plugin.cs` + o
`.cfg` já salvo no jogo, que teria prioridade sobre o novo default do código) de 20 para 1000. Build
0/0, deployado.
**Aviso dado ao usuário:** no `RunNextBatch` atual, um lote em andamento NÃO pode ser interrompido no
meio — cliques extra são ignorados até o lote (até 1000 peças, uma por frame) terminar. Sem stop
mid-batch implementado (nao foi pedido); se 1000 se mostrar longo/instavel demais na pratica, opções:
baixar o valor no `.cfg` sem rebuild, ou pedir que eu adicione um botão de parar mid-batch.

---

## Iteração 35 (2026-07-11) — Mostrar ícones de esquerda E direita nos membros

Pedido: nas opções de antebraço, braço, coxa e panturrilha, ver os dois ícones (esquerda e direita)
em vez de um só. O equipar já funcionava (aplicar um lado auto-equipa o outro via `LinkGroupId`);
faltava só o SEGUNDO ícone aparecer. Causa: na eleição de gatilho do `PersistenceLoader` (reload) a
`groupKey` usava `FlipToLeft(slot)`, colapsando L+R num só botão. Fix: `groupKey` passa a usar o slot
REAL (`linkId + "_" + slot`), então cada lado vira seu próprio gatilho/botão. Só duplicatas do MESMO
slot ainda colapsam; o outro lado continua auto-equipando via `LinkGroupId`. (Import novo já mostrava
os dois — `MassImportFlow` chama `AddTabInitializer` por peça; a divergência era só no reload.) Build
0/0, deployado.

---

## Iteração 34 (2026-07-10) — Miniaturas: auto-ciclo confiável (fora o render off-screen)

Feedback do usuário: as miniaturas do render de fundo continuavam "invisíveis em muitos casos" e o
botão "gerar tudo" travava o sistema. Ele pediu o modo antigo (clicar na peça → gera) ou um botão que
"vai passando automaticamente as peças" gerando. Decisão: **abandonar o render off-screen** (o
`ThumbnailGen` de objeto temporário nunca ficou confiável — bounds/enquadramento) e usar só o caminho
comprovado: fotografar a peça REALMENTE aplicada no boneco (`Thumbnailer.Capture`, que já rodava
on-apply desde a It.27).

- **Removidos** `ThumbnailGen.cs` e `ThumbnailWorker.cs` (render de fundo + fila). Tirados os enqueues
  no `IconButton` e no `MassImportFlow`. `IconButton` agora só CARREGA miniatura existente do disco;
  sem miniatura = mostra o nome (gera quando a peça for aplicada).
- **On-apply mantido** (já existia): clicar numa peça a aplica → `Patch_SpawnAlongside_VariantBar`
  chama `Thumbnailer.Capture` na attachment real → miniatura correta. É o "modo antigo" que o usuário
  gosta.
- **Novo `ThumbnailCycler`** (botão "Gerar miniaturas"): pra cada peça sem miniatura, aplica no preview
  (dispara o Capture confiável), fotografa e remove — uma por frame. Snapshot das peças custom aplicadas
  do usuário e **restaura no fim** (aplicar pra fotografar troca o slot). Durante o processamento,
  `Thumbnailer.Capture` e a `VariantBar` ficam quietos (gated em `ThumbnailCycler.Busy`; sem Refresh/flash
  por peça).

**Ajuste (34.1) — POR LOTES (pedido do usuário "ir por partes... gera e para... clica de novo"):** cada
clique processa só **um lote** (config `ThumbnailBatch`, padrão 20) e **PARA**; a fila/contagem do que
falta **persiste no botão** ("Gerar miniaturas (N)"), então clicar de novo faz o próximo lote. Bursts
curtos em vez de uma corrida longa = jogo responsivo, usuário controla o ritmo. `RunNextBatch` substituiu
o `Toggle`; label = "Gerando (N)" durante o lote, "Gerar miniaturas (N)" enquanto falta, ou o convite
simples quando zera.
- As 1.232 miniaturas ruins já tinham sido movidas pra backup (It.33.3), então regeneram limpas por
  este caminho confiável. Build 0/0, deployado. **Aguardando teste in-game.**

---

## Iteração 33 (2026-07-10) — HOTFIX mapas não carregavam + 3 pedidos (barra travada, miniaturas em massa, otimização)

**Nota de descompasso doc/código:** a Iteração 32 descreve um `Thumbnailer` reescrito (render
off-screen em layer 31 via cullingMask) e um `IconButton` que geraria miniatura preguiçosamente.
O código no HEAD (`fe93c15`) NÃO tem isso — está na versão que isola desabilitando renderers e o
`IconButton` só carrega do disco (a reescrita da It.32 aparentemente foi revertida por sair como
"texto" in-game). Este trabalho foi feito sobre o código real do HEAD.

**HOTFIX — mapas não carregavam (crash no load).** Sintoma do usuário: "meus mapas não estão
carregando." Causa (confirmada no `Player.log`): ao carregar mapa o engine reconstrói cada
personagem via `PickupableCharacter.AddPart`; nosso postfix `AdditiveParts.Reapply` iterava o
`HashSet Active` AO VIVO e o `AddPart` de dentro do loop re-entra em patches que chamam `SetIntent`,
mutando o set → `InvalidOperationException: Collection was modified`. Como `LoadBook`/`Spawn` são
async, virou `AggregateException` e **abortava o carregamento do mapa**. Fix: `Reapply` itera um
snapshot (`new List<string>(Active)`). O guard `_reapplying` só barrava recursão, não a mutação.
- **`scales.json` NÃO é bloat:** 15.301 linhas = 15.301 chaves DISTINTAS (zero duplicata).
  `ScaleStore.Save()` reescreve o arquivo do `Dictionary` (não é append). Compactar não removeria nada
  e apagar linha = perder parte. Os 42 MB são reais (bibliotecas importadas). Não mexer.

**Item 1 — VariantBar travada no mapa (FIX):** `VariantBar.Update()` checava
`creator.gameObject.activeInHierarchy`, mas o `CharacterCreator` é singleton persistente cujo root
fica ativo no mapa. Agora checa `creator.uiWindow.activeInHierarchy` → a barra some ao fechar o criador.

**Item 2 — Miniatura pra todas as peças sem aplicar uma a uma:** gerar a foto exigia a peça aplicada.
Novo `ThumbnailGen` renderiza SEM equipar: monta malha temporária na layer/posição do preview,
normaliza o tamanho (OBJ chega ~0.002 → escala 1/meshMax pra não cair no near-clip), fotografa via
`Thumbnailer.RenderIsolated` (extraído de `CaptureIsolated`), salva PNG e destrói. Novo
`ThumbnailWorker` (MonoBehaviour) processa uma fila poucas por frame (orçamento 6ms), só com o criador
aberto; libera a malha depois (`CustomPart.UnloadMesh`) se não estiver aplicada. Alimentado por:
(a) `MassImportFlow` enfileira cada peça nova na importação; (b) `IconButton.Apply` enfileira ao
desenhar botão sem foto (rolagem); (c) botão "Gerar miniaturas" (`ThumbnailButton`) →
`EnqueueAllMissing()` enfileira a biblioteca toda, com contador de progresso no rótulo. Repaint do tab
a cada 0,5s. `BuildMaterial` virou internal; `TryGetReferenceMaterial` ganhou guarda de null. Custo:
~14 mil renders + ~1,4 GB de PNG (em fundo, não trava).

**Item 3 — Otimizar abrir criador (FIX):** `PersistenceLoader.LoadAll` registrava os 15.280 itens
síncrono num frame (o freeze). Agora roda em coroutine (`CoroutineHost`) com orçamento de 8ms/frame:
o criador abre na hora e as peças populam logo depois (um Refresh no fim). Snapshot dos modelos antes
de iterar (evita "collection modified" se importar durante o load). **Load de mapa:** o import de malha
das peças custom é síncrono no `LoadBook` do engine — dá pra melhorar mas não zerar sem reescrever o
spawn do engine.

Arquivos novos: `ThumbnailGen.cs`, `ThumbnailWorker.cs`, `ThumbnailButton.cs`.
Tocados: `VariantBar`, `Thumbnailer`, `CustomBodyPartAttachment`, `CustomPart`, `IconButton`,
`MassImportFlow`, `PersistenceLoader`, `AdditiveParts`, `Patches`. Build 0/0, jogo fechado, deployado
(plugins + dist + zip).

### Correção 33.1 (mesmo dia) — 2 regressões do teste in-game do usuário
Screenshot + relato: (a) "botões atrás de outros que não consigo clicar" e (b) "não tem ícone de
peça custom alguma".
- **(a) Overlap:** o "Gerar miniaturas" foi posto em y=-226, praticamente em cima do "Aleatório"
  (RandomButton, y=-224). Movido pro fim da coluna (y=-326, abaixo de Sapato/-292).
- **(b) Sem ícones = a coroutine do item 3 NÃO completava.** O `Player.log` confirmou: o log termina
  em `[store] carregados 15301` e a linha `[persist] N registrado` **nunca sai** — a sessão inteira
  rodou sem a coroutine terminar de registrar as peças. Sem registro, `CustomPartCatalog.TryGet` falha
  e o `IconButton` trata a peça como nativa (sem ícone). **Revertido `PersistenceLoader.LoadAll` para
  síncrono** (versão da It.30, já lazy/só-metadados, que mostrava os ícones). `CoroutineHost.cs`
  removido (sem uso). Os PNGs no disco (1101) nunca foram apagados — era só o registro.
- **Item 3 (otimização de abrir criador) — abordagem trocada:** em vez de diluir o registro (quebrou),
  removi o dump de diagnóstico `[all-paths]` do `Patch_CreatorUi.Postfix` que escrevia **milhares de
  linhas de log a cada abertura** (15k+ paths em `attachmentPaths`) — ganho seguro, zero risco. O
  registro síncrono (leve, só metadados) e o freeze "leve" original permanecem; otimização mais
  profunda exige teste in-game (não dá pra fazer daqui). Build 0/0, deployado. **Aguardando novo teste.**

### Correção 33.2 (mesmo dia) — torsos femininos sumindo (encoding + eleição de gatilho)
Relato: "todos os ícones femininos sumiram do torso, só parece que tem dois modelos". Investigado a
fundo (log + scales.json + disco):
- **Causa raiz 1 (encoding, PRÉ-EXISTENTE):** no `scales.json` os acentos estão gravados como Latin-1
  (`ç` = byte `0xE7`). O `File.ReadAllLines` lê como UTF-8 → `0xE7` vira `�` (U+FFFD). O caminho
  resultante não bate com o arquivo real no disco (que é UTF-8 `c3 a7`), então a peça é **pulada**
  como "arquivo ausente". 3.368 peças puladas — TODAS braço/antebraço (nomes PT com acento).
- **Causa raiz 2 (eleição de gatilho):** cada garment do CAS tem torso + braços + antebraços no MESMO
  `link`+`slot(torso)`. Só o "gatilho" eleito do grupo ganha botão. A eleição pegava o de maior
  prioridade e, empatando (todos torso=10), **o primeiro da lista** — que na ordem do arquivo é um
  antebraço/braço com acento (pulado). Resultado: `Register` falha no gatilho → **o garment inteiro
  some da aba**. Confirmado num garment real: as 5 peças em ordem = 4 acentuadas + o torso real por
  último (nunca eleito).
- **Fix (`PersistenceLoader`):** (1) eleição do gatilho agora **pontua existência acima de prioridade**
  (`score = (existe?1000:0) + prioridade`), então o torso real (arquivo ok) sempre vence o braço com
  nome quebrado; (2) novo `TryResolveModel` tolera a corrupção: se o caminho tem `�`, faz glob na pasta
  trocando `�`→`*` e acha o arquivo real (validado: `antebra*o_..obj` casa com `antebraço_..obj`),
  usando o caminho corrigido na peça. Cache de resolução limpo ao fim do load. Isso faz o torso
  aparecer E recupera as peças acentuadas (registradas, mesmo sem botão por serem não-gatilho).
- **NÃO é regressão desta sessão:** eleição + encoding são pré-existentes; o mod não reescreve o
  `scales.json`. Surgiu agora porque a biblioteca de tops femininos (nomes com braço/antebraço) foi
  importada e, no reload, sumia. Build 0/0, deployado. **Aguardando teste in-game.**

### Correção 33.3 (mesmo dia) — miniaturas vazias/pequenas
Fix da 33.2 trouxe os torsos de volta, mas as miniaturas saíram "muito pequenas... quase invisíveis".
Inspecionei os PNGs gerados: metade (132 de 4655 bytes) totalmente transparentes, correlação 100% =
TODAS as vazias eram peças acentuadas (braço/antebraço). Duas causas no `ThumbnailGen`:
- **Vazias:** eu passava `mr.bounds` (bounds do Renderer) pro enquadramento, mas num renderer
  recém-criado o `.bounds` vem centrado na POSIÇÃO DO TRANSFORM, ignorando o centro local da malha.
  Peças cuja geometria fica longe da origem (braços na posição anatômica) ficavam fora do quadro →
  foto vazia. Fix: calcular os world bounds EXPLICITAMENTE (`transform.TransformPoint(mesh.bounds.
  center)` + `Scale(extents, lossyScale)`).
- **Sobrepostas/pequenas:** `Object.Destroy(go)` é adiado até o fim do frame; com o worker gerando
  várias por frame, os objetos temporários anteriores continuavam vivos na mesma posição e vazavam pra
  próxima foto (duas peças sobrepostas). Fix: `DestroyImmediate` + destruir o material por-miniatura.
- **Limpeza:** movi as 1.232 miniaturas ruins de hoje para `CustomParts/thumbs_bad_backup_20260710/`
  (reversível) pra regenerarem corretas; mantidas as 21 de ontem. Build 0/0, deployado.
  **Aguardando teste:** rolar o torso e conferir ALGUMAS antes de "Gerar miniaturas" (backfill total).

## Iteração 32 — Ícone com o modelo (não o nome) + preview vivo no import de pasta (P7/P15)
Duas dores: (1) peça importada aparecia como **texto** no botão da aba (só o import único, depois de
"Confirmar", gerava a miniatura — o import de pasta nunca gerava, ficava tudo escrito); (2) o import de
pasta pedia os valores **digitados às cegas**, sem ver uma peça antes, então saía tudo torto e tinha
que editar de uma em uma.

- **P7 — ícone renderizado pra QUALQUER peça, gerado sozinho (`Thumbnailer` reescrito).** A foto do
  botão agora é feita **off-screen**: dropa uma cópia descartável da malha longe da cena numa layer
  isolada (31), aponta a **própria câmera do criador** (URP-correta) só pra ela via `cullingMask`, e
  fotografa em 512² transparente — 1 render síncrono, câmera restaurada, sem flicker. Renderiza
  **unlit** com a textura ativa (variante 0), então o ícone mostra o modelo com a cor certa e não
  depende de luz/probe da cena. Como não precisa spawnar a peça no boneco, funciona pra import único,
  em massa **e** pra peça só recriada de registro (a malha/textura carrega sob demanda, `EnsureLoaded`).
- **Geração preguiçosa no `IconButton`.** Quando o botão desenha e a peça **não tem** miniatura salva,
  ele **gera na hora** e cacheia em disco. Isso conserta retroativamente **todas** as peças que já
  estavam como texto (só as visíveis, uma vez cada; próxima sessão carrega o PNG do disco). `Thumbnailer`
  não dá `Refresh` (evita recursão dentro do init do botão) — quem chama controla o repaint.
- **P15 — import de pasta com preview vivo.** "Importar Pasta" agora: (lado, se for categoria com L/R)
  → escolhe a pasta → importa a **primeira** `.obj` **viva** no boneco e abre o painel de escala normal
  (escala/eixos/rotação/posição/gênero/canal + modo modelagem por arraste) com um botão a mais
  **"Aplicar a toda a pasta (N)"**. Ao clicar, comita a peça previa (igual "Confirmar") e importa
  **todas as restantes** com esses valores — escala vira **multiplicador** sobre o normalizado de cada
  malha, então peças de tamanhos diferentes ainda caem certas. "Confirmar (só esta)" fica de escape.
- **Config nova:** `ThumbnailYaw` (graus) — se o ícone sair mostrando as **costas** do modelo, ponha
  180 (90/270 = perfil). `ThumbnailFill` (já existia) controla o zoom.
- **Arquivos:** reescrito `Thumbnailer.cs`; alterados `IconButton.cs` (gera preguiçoso), `ImportFlow.cs`
  (`OnMeshLoaded`→`ImportLoadedMesh` reusável com path explícito + `folderCtx`), `ScaleSession.cs`
  (modo pasta: botão + `ApplyToFolder`, `PersistPerModel` extraído), `MassImportFlow.cs` (`FolderImportContext`,
  fluxo preview→aplicar, `ImportRemaining`), `Plugin.cs` (`ThumbnailYaw`), `Loc.cs`. **Removido:**
  `MassImportConfig.cs` (painel só-numérico, substituído pelo preview vivo).
- Build 0/0 → implantado em `BepInEx/plugins/`. **Falta:** teste em jogo (confirmar que o render
  off-screen aparece — se ficar texto, a câmera pode precisar do caminho URP específico — e o ângulo
  do ícone / `ThumbnailYaw`).

## Iteração 31 — Import de pasta recursivo + botão "texturas compartilhadas" (auto-rota por nome)
Fluxo pro export do CAS Batch Exporter, onde cada roupa é uma subpasta com **7 partes** (torso,
braço/antebraço/mão D/E) e as texturas de swatch vêm **repetidas por parte** (o usuário apaga as
cópias, deixando só os N swatches únicos por pasta).

- **Recursão nos DOIS botões de pasta.** `Directory.GetFiles(..., AllDirectories)` — seleciona `Top\`
  e ele varre todas as subpastas de uma vez, sem importar pasta a pasta. No "Importar Pasta" original,
  cada `.obj` ainda pareia com as texturas da própria subpasta e o conjunto vai pra categoria aberta
  com o lado escolhido (modelo de categoria única inalterado).
- **Novo botão "Importar Pasta (texturas compartilhadas)"** (`SharedFolderImportButton`/`SharedFolderImportFlow`),
  abaixo do "Importar Pasta". Um clique: escolhe a pasta (subpastas incluídas) e, **por subpasta**,
  compartilha **todas** as texturas da pasta entre **todos** os `.obj` dela — variante 0 ativa, mesma
  ordem pra todos (ordenadas por índice de swatch via prefixo comum, robusto a hash terminando em
  dígito). Sem cópia de textura, sem depender de nome de arquivo casar com o OBJ. Escopo de
  compartilhamento é **por subpasta** (roupa A nunca vaza pra roupa B).
- **Auto-roteamento por nome (`PartNameRouter` + `NativeCategory`).** Cada peça é mandada pra
  categoria/slot/lado certos lendo o **prefixo PT** do nome (accent-insensitive): `torso`→torso,
  `braço`→uppers/`armUpper`, `antebraço`→wrists/elbows/arms/`armLower`, `mão`→hands/`hand`;
  bottoms (perna em 3, pois a engine **não** tem `legUpper`): `parte de cima`/`coxa`/`quadril`/`cintura`
  (peça única)→hips/`hip`, `panturrilha` (batata da perna, lado)→legs/`legLower`; `_direito/direita`→R,
  `_esquerdo/esquerda`→L.
  O savePath da categoria nativa é **descoberto em runtime** varrendo `CharacterCreator.attachmentPaths`
  (sem hardcode; independe de idioma e de update do jogo). Prefixo desconhecido ou categoria nativa
  ausente → peça **pulada** com aviso no log (nunca roteia errado). Colocação (escala/rotação/offset)
  vem do **padrão salvo da categoria** (P6) — cada categoria mantém seu ajuste no lote inteiro.
- **Persistência/UX:** batch (Fix B da It.30) + yield a cada 8 (Fix C), toast final com contagem de
  importadas/puladas. Variantes acima de 5 já caem no spinner `< i / N >` (P13) — os 9 swatches por
  roupa funcionam.
- **Arquivos novos:** `PartNameRouter.cs`, `NativeCategory.cs`, `SharedFolderImportFlow.cs`,
  `SharedFolderImportButton.cs`. **Alterados:** `MassImportFlow.cs` (recursão), `Patches.cs` (injeta o
  botão), `Loc.cs` (rótulo/toasts EN).
- Build 0/0 → implantado em `BepInEx/plugins/` + `dist/package` + `dist/CustomPartsMod-v0.1.1.zip`
  re-empacotado. **Falta:** teste em jogo (validar rota por parte e posição dos botões na UI).

## Iteração 30 — Escala para bibliotecas grandes (carga preguiçosa + batch de save)
Preparação pra bibliotecas de **milhares** de peças (ex.: centenas de calças do Sims 4 cortadas em
cintura + perna-L + perna-R). Três gargalos do mod (não da engine — o `UiScrollPool` da engine já é
virtualizado e aguenta a quantidade de botões) foram corrigidos:

- **Fix A — Carga preguiçosa da malha (mata o freeze ao abrir o Criador).** Antes, `PersistenceLoader`
  reimportava do disco, síncrono, o `.obj` de **toda** peça salva (total acumulado de todas as
  categorias) a cada abertura do Criador — o que travava por segundos/minutos conforme a biblioteca
  crescia. Agora `PersistenceLoader.LoadAll` só **registra por metadados** (id/categoria/slot/escala já
  estão no `scales.json`); nenhum OBJ é parseado nem PNG decodificado no boot. A malha + textura são
  importadas por `CustomPart.EnsureLoaded()`, chamado no topo de `CustomBodyPartAttachment.Build` — ou
  seja, **só quando a peça é efetivamente clicada/aplicada**. Boot vai de O(n) parses → ~0 I/O
  (só um `File.Exists` barato por registro pra não deixar botão morto de arquivo movido).
  - **Sub-ponto:** o retrato (thumbnail P7) também era decodificado no `Register` (um PNG por peça no
    boot). Movido pra `IconButton.Apply`, que roda no init de botão **virtualizado** → só peças visíveis
    na rolagem tocam o disco (flag `CustomPart.ThumbnailProbed` evita re-stat a cada re-init do pool).
- **Fix B — Batch do `scales.json` no import em massa (mata o O(n²)).** `ScaleStore.Set` chamava
  `Save()`, que reserializa e regrava o **arquivo inteiro**, a cada item. Importar 600 com milhares já
  salvos = 600 regravações do arquivo todo. Novos `ScaleStore.BeginBatch()`/`Flush()`: durante o import
  em massa, `Set` só atualiza o cache em memória; **uma** gravação no fim (em `try/finally`, à prova de
  exceção). Import individual (Confirmar 1 peça) segue com save normal.
- **Fix C — `yield` periódico no loop do import em massa.** `MassImportFlow.RunImport` importava a pasta
  inteira num `foreach` sem `yield` → tela congelada do início ao fim. Agora cede a cada 8 itens
  (`yield return null`) e loga progresso `[mass-import] i/N`. O trabalho continua O(n) (cada OBJ é
  parseado uma vez pra normalizar a escala — inerente), mas espalhado entre frames a UI não trava.
- **Comportamento preservado:** persistência, texturas/variantes (P13), gênero (P3), tags (P10) e
  reedição (P6) intactos — só o **momento** da carga mudou. Uma peça cujo arquivo-fonte sumiu não some
  mais silenciosa do catálogo no boot; vira aviso no log (registro pulado) e, se o arquivo sumir depois
  do registro, `EnsureLoaded` mostra um toast de erro ao clicar.
- **Arquivos alterados:** `CustomPart.cs` (`EnsureLoaded` + `ThumbnailProbed`), `PersistenceLoader.cs`
  (`Reload`→`Register`, metadados apenas), `CustomBodyPartAttachment.cs` (chama `EnsureLoaded` no
  `Build`), `CustomPartCatalog.cs` (tira a carga eager do thumb), `IconButton.cs` (carga preguiçosa do
  thumb), `ScaleStore.cs` (`BeginBatch`/`Flush` + guarda no `Save`), `MassImportFlow.cs` (batch + yield).
- Build 0/0 → implantado em `BepInEx/plugins/` + `dist/package` + `dist/CustomPartsMod-v0.1.1.zip`
  re-empacotado. **Falta:** teste em jogo com uma pasta grande (validar tempo de boot e de import).

## Iteração 29 — Idioma automático (PT/EN) + lado Esquerda/Direita também em Mãos
- **Idioma automático:** o mod agora fala o idioma do jogo. `Loc.T` (`Loc.cs`) traduz PT→EN lendo
  `PlayerPrefs["Language"]` ao vivo (`"pt"` = português; qualquer outro = inglês), aplicada dentro dos
  funis de UI (`UiFactory.Label/TextButton`, `Compat.ShowSuccess/ShowError`, `PanelUi.SetButtonLabel`),
  então quase todo texto do mod localiza sem mudar cada call-site. Botões PERSISTENTES (Importar
  Parte/Pasta, Aleatório, Sapato, Só custom) são criados uma única vez e ficavam presos no idioma da
  criação; `LocButtons.cs` resolve isso assinando `Translator.onLanguageUpdated` da engine e
  re-traduzindo-os ao vivo quando o idioma muda em runtime.
- **Esquerda/Direita generalizado para Mãos:** o sistema de lado (antes só Pés/Sapato, `FootSide`) virou
  `SidedCategory` — reconhece **Pés+Sapato** (kind `Feet`) e **Mãos** (kind `Hands`), cada um com seu
  próprio slot esquerda/direita (`legLowerL/R`, `handL/R`) e seu próprio "último lado lembrado"
  (`ScaleStore.GetLastSideLeft/SetLastSideLeft`, chave por kind — escolher lado em Mãos não afeta o
  lado lembrado de Pés). O prompt de import individual (`FootSidePrompt`→`SidePrompt`) e o toggle do
  Importar Pasta (`MassImportConfig`) usam o mesmo `SidedCategory` e mostram o título/pergunta certos
  ("Lado do pé" vs "Lado da mão").
- **Cuidado corrigido:** o botão "Sapato" só deve aparecer na categoria de Pés, não em Mãos (que também
  é "sided" agora) — `ShoeButton.UpdateVisibility` checa `SidedCategory.KindOf(category) == Kind.Feet`
  explicitamente, não mais o `AppliesTo` genérico.
- **Arquivos novos:** `Loc.cs`, `LocButtons.cs`, `SidedCategory.cs` (substitui `FootSide.cs`),
  `SidePrompt.cs` (substitui `FootSidePrompt.cs`). **Alterados:** `UiFactory.cs`, `Compat.cs`,
  `PanelUi.cs`, `ScaleSession.cs`, `CustomFilterButton.cs`, `RandomPanel.cs`, `ImportButton.cs`,
  `MassImportButton.cs`, `RandomButton.cs`, `ShoeButton.cs`, `ShoeCategory.cs`, `ImportFlow.cs`,
  `MassImportFlow.cs`, `MassImportConfig.cs`, `ScaleStore.cs`.
- **README.txt reescrito:** inglês primeiro (seção completa) + português depois (seção completa),
  ambos com instalação; instrução de build documenta a pasta `lib\` (BepInEx/0Harmony, baixados à
  parte, fora do Git) e como reempacotar `dist\CustomPartsMod-v0.1.0.zip`.
- Build 0/0 → implantado em `BepInEx/plugins/` + `dist/package` + zip re-empacotado.

## Iteração 28 — Categorias "Pés" e "Sapato" (sapato aditivo sobre os pés)
- **O quê:** duas novas categorias sintéticas de peça no criador, no mesmo estilo de "Olhos": um botão
  **"Pés"** (peça de pé que **substitui** o slot da perna) e um botão **"Sapato"** (subcategoria que
  **adiciona sobre os pés**, aditivo estilo acessório). Ficam lado a lado depois da fileira de categorias
  nativas: Olhos → Pés → Sapato.
- **Como (reaproveita o padrão de "Olhos"):** o injetor de botão de categoria (antes `EyesTabButton`) foi
  **generalizado** em `CategoryTabButton` — clona um botão de categoria nativo do painel, neutraliza a
  navegação clonada, troca o ícone e aponta `itemTabsLoader.SetPathFilter(<path>)` pra categoria sintética.
  Um único `CustomCategories.EnsureAll` cria os três (Olhos/Pés/Sapato) e **coordena o sublinhado de
  seleção** entre todos (só um custom OU uma aba nativa parece selecionado por vez).
- **Paths sintéticos (irmãos, não aninhados):** `Pés` = `{"CustomParts","feet"}`, `Sapato` =
  `{"CustomParts","shoes"}`. Como o filtro da engine (`BuildTabsWithPathButtons.FilterByPath`) é **por
  prefixo**, mantê-los irmãos garante que "Pés" mostre só pés e "Sapato" mostre só sapatos.
- **Mapeamentos (independentes de idioma, por segmento):** `feet`/`shoes` → socket `legLowerR`
  (`CategoryMap`); `feet`/`shoes` → tinta `Leather_Primary` (`ChannelMap`); `shoes` entra em
  `AccessoryMap.AccessorySegments` → **aditivo** (o sapato empilha sobre o pé em vez de substituir; segue o
  fluxo de re-aplicação aditiva de `AdditiveParts`/`Patch_AddPart`, igual aos olhos).
- **Guardas de import:** `ImportFlow`/`MassImportFlow` aceitavam prefixo curto só pra Olhos; agora usam
  `CustomCategory.IsSynthetic` (match exato de path) → importar dentro de "Pés" (2 segmentos) é aceito sem
  afrouxar a checagem `< 3` das nativas.
- **Ícones em código (brancos, sem asset):** `FootIcon` (pegada = 2 elipses + 5 dedos) e `ShoeIcon`
  (perfil lateral = sola + calota do bico + meia-elipse do cabedal).
- **Arquivos novos:** `FeetCategory.cs` (`FeetCategory`/`ShoeCategory`/`CustomCategory`), `CategoryTabButton.cs`
  (genérico), `CustomCategories.cs` (coordenador), `FootIcon.cs`, `ShoeIcon.cs`. **Removido:** `EyesTabButton.cs`
  (lógica migrada pro genérico). **Alterados:** `Patches.cs` (chama `CustomCategories.EnsureAll`),
  `CategoryMap.cs`, `ChannelMap.cs`, `AccessoryMap.cs`, `ImportFlow.cs`, `MassImportFlow.cs`.
- **Assinaturas conferidas por decompilação:** `BuildTabsWithPathButtons.FilterByPath` = match por prefixo;
  `CharacterCreatorEnums.RiggedAttachType` **não tem** socket de pé dedicado → usa `legLowerR` (como os pés
  nativos).
- Build 0/0 → implantado em `BepInEx/plugins/` + `dist` + zip.

## Iteração 27 — P7: Miniatura (retrato) do modelo como ícone do botão
- **O quê:** ao clicar em **Confirmar** no painel de edição, o mod tira um **retrato do personagem**
  (com a peça aplicada) e usa como **ícone do botão** daquela peça na lista, no lugar do nome de texto.
- **Como (reaproveita a engine):** `CharacterCreator.SnapShot()` — o **mesmo** método que a engine usa
  pros próprios ícones de mesh/token — renderiza o boneco numa RenderTexture 512×512 com **fundo
  transparente** (`CharacterCreatorCamera.DoSnapShot`: move a câmera pra pose de retrato `snapPos`,
  `clearFlags=Nothing`, `cam.Render()`, `ReadPixels`, restaura tudo — síncrono, sem flash; o painel de
  UI é overlay, não entra no render da câmera do criador).
- **Pintar o ícone:** `IconButton.Apply` roda no **mesmo postfix** (`Patch_ItemTrash` →
  `BuildTabsLoader_Characters.InitialiseDataItemButton`) da lixeira/edição, **depois** do
  `InitialiseButton` da engine (que deixa as custom em modo texto, já que o `CachedResources.LoadAsync`
  do `fullPath` virtual retorna null). Se a peça custom tem thumbnail: `button.icon.SetImage(sprite)` +
  `button.icon.SetActive(true)` + `button.text.SetActive(false)`. Pool-safe: cada re-init roda o
  `DefaultButton`+`InitialiseButton` (reseta pra texto) antes, então nada precisa ser desfeito aqui.
- **Persistência:** `ThumbnailStore` grava PNG em `CustomParts\thumbs\<sourceKey>.png` (via
  `EncodeToPNG`) e cacheia em memória; `CustomPartCatalog.Register` carrega o PNG salvo (se existir) ao
  registrar a peça → miniatura reaparece no import seguinte **e depois de reiniciar**. `PartsAdmin.Delete`
  apaga o PNG junto com a peça.
- **Arquivos novos:** `ThumbnailStore.cs` (disco+cache), `Thumbnailer.cs` (captura no Confirm),
  `IconButton.cs` (pinta o botão). Alterados: `CustomPart.cs` (`Thumbnail`/`ThumbnailSprite`),
  `Patches.cs`, `ScaleSession.cs` (Confirm), `CustomPartCatalog.cs` (Register), `PartsAdmin.cs`.
- **Assinaturas conferidas por decompilação:** `CharacterCreator.SnapShot()` público → `Texture2D`;
  `CharacterCreatorCamera.DoSnapShot()` (512×512, transparente); `BuildTabsButton.icon` = `UiImage`
  (`SetImage(Sprite/Texture2D)`, `SetActive`); `UiImage.SetImage(Texture2D)` chama `ConvertToSprite`
  (extensão UnityUtils); fluxo do pool = `AddContentItemInitializer.Routine` → `InitialiseButton` →
  `InitialiseDataItemButton` (nosso postfix). `EncodeToPNG` já referenciado (ImageConversionModule).
- **Import em massa** não passa pelo Confirmar, então essas peças ainda mostram texto até uma
  edição+Confirmar (refinamento futuro).
- Build 0/0; jogo fechado → implantado em `BepInEx/plugins/` + `dist` + zip.

### Iteração 27.1 — miniatura ISOLADA na peça (feedback: v1 fotografou o corpo todo com zoom aberto)
- **Problema:** a v1 (`creator.SnapShot()`) mostrava o **personagem inteiro** afastado, não a peça.
- **Refeito (`Thumbnailer.CaptureIsolated`):** render próprio, só da peça, enquadrado nela.
  1. **Isola:** desliga (`renderer.enabled=false`) todos os `Renderer` sob `creator.dummy` menos os da
     peça (`attachment.GetComponentsInChildren<Renderer>()`); esconde `creator.backgroundContent`
     (o cenário). Tudo restaurado no `finally`.
  2. **Enquadra:** pega os `renderer.bounds` (mundo) da peça, encaixa a **esfera envolvente** na FOV
     (`dist = raio / sin(fov/2) × 1.15`) mantendo a direção de visão atual da câmera; `LookAt(center)`,
     `aspect=1`, `nearClipPlane=0.01` (não corta peças pequenas de perto).
  3. **Render transparente:** `clearFlags=SolidColor` + `backgroundColor=(0,0,0,0)`, post-processing
     desligado via reflexão no `UniversalAdditionalCameraData.renderPostProcessing` (mesmo que o
     `DoSnapShot` faz; reflexão evita referência ao URP), RT 512×512 depth 24, `cam.Render()`,
     `ReadPixels` → `Texture2D` RGBA32.
  4. **Restaura** câmera (pos/rot/target/clear/bg/near/aspect/PP), renderers e backdrop no `finally`.
- **Fallback:** se a câmera/renderers não estiverem disponíveis, cai no `creator.SnapShot()` (corpo
  inteiro) pra ainda gerar algo.
- **Campos usados (todos públicos, conferidos no DLL):** `CharacterCreator.creatorCam`
  (`CharacterCreatorCamera`, `.cam`/`.preview`), `.dummy` (`PickupableCharacter`), `.backgroundContent`
  (`Transform`). `CustomBodyPartAttachment` é um `MeshRenderer` no próprio GO (bounds via `renderer.bounds`).
- Build 0/0; jogo fechado → implantado. **Aguarda teste:** Confirmar deve gerar um retrato **só da
  peça, com zoom nela**; reiniciar e ver se persiste.

### Iteração 27.2 — zoom da miniatura (feedback: "ainda está pequeno")
- **Causa:** o enquadramento fitava a **esfera envolvente** (`b.extents.magnitude` = meia-diagonal da
  caixa), bem maior que a peça visível → sobrava muita margem.
- **Fix:** enquadra pela **maior dimensão da caixa** (`max(extents.x,y,z)`) numa fração-alvo do quadro,
  com projeção correta no plano de imagem: `dist = maiorMeia / (fill · tan(fov/2))`.
- **Config novo `ThumbnailFill`** (`General`, padrão **0.9**): fração do quadro que a peça ocupa
  (0.1–1.0). Maior = mais zoom. Editável pelo usuário no `.cfg` sem recompilar (se cortar as bordas,
  baixar; se quiser maior, subir até ~0.95).
- Build 0/0; jogo fechado → implantado (`plugins` + `dist` + zip). **Aguarda teste do enquadramento.**

### Iteração 27.3 — botões E/X menores, pareados com os badges nativos (pedido do usuário)
- Os botões "E" (editar) e "X" (deletar) estavam em 22×22, maiores que a estrela de favoritar/bloqueio
  nativos. Novo `ItemBadge.Size(button)` lê o tamanho do `BuildTabsButton.favouriteToggle` (ou
  `hideToggle`) em runtime (rect.height, fallback sizeDelta.y, clamp 11–20, default 16) e os dois botões
  passam a usar esse lado. Espaçamento do "E" recalculado (`-(s+2)`) pra acompanhar o tamanho menor.
- Build 0/0; jogo fechado → implantado. **Aguarda teste visual.**

## Iteração 16 — Filtro "Só customizados" (P9) + Variações de textura (P13)
- **P9 — filtro "Só custom":** botão de alternância ("Só custom: SIM/NÃO", `CustomFilterButton`,
  empilhado abaixo de "Importar Pasta") que esconde as peças nativas e mostra só as importadas, na
  categoria aberta. Implementado por **postfix** no próprio seletor de aba da engine
  (`BuildTabsWithPathButtons.Filter`, `Patch_CustomOnlyFilter`): quando ligado, derruba todo `id` que
  não é do `CustomPartCatalog`. Compõe com os filtros de caminho/gênero/busca já existentes e, de
  graça, as setas de navegação (P12) passam a percorrer só a lista reduzida. Estado só na sessão.
  Gated ao `creator.itemTabsLoader` (`BuildTabsLoader_Characters`) pra não afetar outros tab systems.
- **P13 — variações de textura:** cada parte guarda **até 5 texturas**; no painel de edição há uma
  linha "Texturas" com **caixas numeradas** — clicar numa preenchida troca a textura ao vivo
  (`SetTexture`), clicar na primeira vazia ("+") abre o file browser e adiciona; a ativa fica
  destacada em verde. `CustomPart.TextureVariants`(lista sem buracos)+`ActiveVariant`; persistidos no
  `scales.json` (`texVariants`/`texVariant`) e restaurados no import/reload. Substitui o antigo botão
  "Textura…" (o "+" faz o mesmo papel). Painel cresceu p/ 470px (gênero/salvar desceram).
- Assinaturas conferidas por decompilação: cadeia de filtro é `BuildTabsWithPathButtons.Filter` →
  `base` `BuildTabsWithFilters.Filter` (tags/busca/visível); `BuildTabsLoader_Characters` **não**
  sobrescreve `Filter`; `PropDatabaseData.hasTextureVariants`/`tintable` existem mas não foram usados
  (fizemos caixas próprias no painel, mais simples de controlar).
- Build 0/0; instalado (`BepInEx/plugins/CustomPartsMod/` + `dist/CustomPartsMod-v0.1.0.zip`
  reempacotado). **Não testado em jogo nesta sessão** — verificar: (P9) ligar "Só custom" numa
  categoria com peças custom e nativas, conferir que só as custom aparecem, e que as setas P12 andam
  só entre elas; (P13) abrir a engrenagem de uma peça, adicionar 2ª/3ª textura no "+", trocar entre
  as caixas e conferir que a escolha sobrevive ao "Salvar padrão" + reiniciar.

## Iteração 15 — Importação em massa (pasta inteira) — P1
- Pedido: importar uma pasta inteira de `.obj` de uma vez, em vez de um arquivo por clique. Depois:
  antes de importar, fixar **convenções** (gênero/canal/escala/rotação/posição) que TODOS os modelos
  da pasta herdam — só a textura fica de fora (pareada automaticamente por arquivo).
- Botão **"Importar Pasta"** (`MassImportButton`, empilhado abaixo de "Importar Parte") →
  `MassImportFlow.OnImportFolderClicked` valida a categoria aberta e abre o painel de convenções.
- **Painel de convenções** (`MassImportConfig`, novo): campos Escala × (multiplicador), Esc XYZ,
  Rotação XYZ, Posição XYZ + botões de ciclo **Canal** (Pele/roupas/etc.) e **Gênero**
  (Ambos/Feminino/Masculino, tag P3). Sem preview ao vivo (não há peça ainda) e **sem textura**.
  Semeado com o padrão da categoria (P6) + canal automático (`ChannelMap.ForCategory`). "Escolher
  pasta e importar" captura as `MassImportSettings` e dispara a importação; "Cancelar" fecha.
- **`PanelUi`** (novo helper): construtores de painel/campo/botão reutilizáveis, extraídos do padrão
  provado do `ScaleSession` (fundo + header + `DragHandle` + a superfície-`UiButton` transparente que
  impede o clique de vazar pra câmera do preview). `ScaleSession` fica intacto; o helper é a base
  compartilhada dos painéis novos.
- `MassImportFlow`: no confirm, seletor de **pasta** (`FileBrowser.PickMode.Folders`), varre
  `Directory.GetFiles(*.obj)` e importa cada modelo silenciosamente via `Compat.ImportMesh` (mesmo
  caminho do `PersistenceLoader`). Cada item usa `escala = normalizada × settings.ScaleMultiplier`
  (tamanhos diferentes caem consistentes) + eixo/rotação/posição/gênero/canal das convenções.
- Cada modelo é **persistido de imediato** em `scales.json` (mesmo registro que "Confirmar" gravaria),
  então a pasta inteira sobrevive a um restart sem precisar abrir o painel item a item.
- GLB fica de fora do escopo por ora: `Compat.ImportMesh` até suportaria, mas o reload silencioso
  no próximo boot (`PersistenceLoader`) ainda não sabe recarregar GLB (limitação conhecida do P0).
- Build 0/0; instalado (`BepInEx/plugins/CustomPartsMod/` + `dist/CustomPartsMod-v0.1.0.zip`
  reempacotado). **Não testado em jogo nesta sessão** — pendente de verificação manual (abrir uma
  categoria, "Importar Pasta", ajustar convenções, escolher uma pasta com 2+ `.obj`, checar se todos
  aparecem na aba com a escala/gênero/canal escolhidos e sobrevivem a reiniciar o jogo).

## Iteração 14 — Zoom no criador (+ / – ao lado das setas)
- Pedido: ferramenta de zoom no criador, dois botões (+ e –) ao lado das setas ◀▶.
- **Sem zoom nativo** (`CharacterCreatorCamera.FocusOnHead/Full` são stubs vazios). Zoom via
  `cam.fieldOfView` (perspectiva, passo 4°, clamp 8–70) ou `orthographicSize` (ortho, ×0.88/clique) —
  independente do `Move()` que só mexe no X da câmera.
- `ZoomButton` (novo): `MaskableGraphic`+`IPointerClickHandler` desenhando "+"/"–" com barras (sem
  fonte/sprite, mesmo padrão do `NavArrowButton`). `ZoomButtons.Ensure` cria os dois e os empilha abaixo
  da seta direita, reusando o `NavArrowFollow` (agora com `zoomIn/zoomOut`) para acompanharem o boneco.
  Injetado no `Patch_CreatorUi` junto de `NavArrows`. Build 0/0.

## Iteração 0 — Scaffold (Fase 0)
- Projeto `net472`, `CustomPartsMod.csproj` referenciando os DLLs do jogo + BepInEx + Harmony.
- `Plugin.cs`: entrypoint BepInEx, `Harmony.PatchAll`, config `DefaultScale` e `UseGameShader`.
- Empacotamento em `dist/` (zip extrai-na-raiz: `winhttp.dll` + `BepInEx/`), instalação aditiva/reversível.

## Iteração 1 — Import por categoria, em memória (Fase 1)
- `ImportButton`: injeta o botão "Importar Parte" clonando um botão do criador.
- `ImportFlow`: lê a categoria aberta (`pathPartsFilter`), abre o file browser do jogo (`MeshImporter.LoadMesh`), registra a parte.
- `CustomPartCatalog`: cria um `PropDatabaseData` sintético e injeta em `CharacterCreator.attachmentPaths`; a parte vira opção clicável na aba.
- `CustomBodyPartAttachment`: constrói a malha em runtime como `CharacterAttachment` no socket do osso.
- Patches Harmony em `PickupableCharacter.AddPart(string)` e `Contains(string)` para ids customizados.
- `Compat`: reflexão para membros privados da engine (`SetAllOn`, `AddPart(string,CharacterAttachment)`, `AddContentItemInitializer`, `pathPartsFilter`, `PopupAlert`).
- **Verificação:** todas as assinaturas conferidas por decompilação (`ilspycmd`). Correção: `BuildTabsBase`/`BuildTabsWithPathButtons` estão no namespace `RpgEngine`.
- **Blindagem anti-crash:** `Build` cria o GameObject antes de qualquer operação que possa lançar; nunca retorna null (evita NRE no `SpawnAlongside`).

## Iteração 2 — Botão de texto + visibilidade
- Botão "Importar Parte" virou **texto** de verdade (o modelo clonado era só-ícone): remove o ícone e cria rótulo TMP (fonte pega da cena) — `UiFactory`.
- **Substituição de slot:** ao pôr uma parte, limpa a anterior via o próprio `CharacterAttachment.RemoveAllChildren` da engine.
- **Escala compensada:** cancela a `lossyScale` do osso (evitava a malha virar um ponto invisível).
- Recalcula `bounds`/`normals` da malha no import (anti-culling / anti-preto).
- Logs `[diag]` (escala do osso, tamanho em mundo, shader).

## Iteração 3 — Painel de escala + persistência de escala
- `ScaleSession`: painel flutuante pós-import com preview ao vivo.
- `ScaleStore`: escala salva por modelo em `CustomParts\scales.json`.

## Iteração 4 — Colocação (pivô/osso/typed) + lixeira
- **Pivô centralizado** no centro da malha (escala cresce no lugar, não translada).
- **Segue o corpo:** prende no **osso animado** via `BoneResolver` (busca por nome em `boneHelper`; loga `[bones]`), com fallback pro socket.
- Substituição agora limpa a parte nativa (socket) **e** a custom anterior (osso).
- **Campos digitáveis** no painel: Escala (até 10.000) e posição X/Y/Z (unidades de mundo), clonados de `characterName` (`UiInputField`); botões – / +.
- **Lixeira** ("X" vermelho) por parte custom: exclui do preview, do catálogo, da aba (`UiScrollPool.RemoveContentItem`) e apaga a escala salva. Patch em `BuildTabsLoader_Characters.InitialiseDataItemButton`.
- Enter só confirma o valor digitado no campo; **Confirmar é só clicando**.
- `ScaleStore` passou a guardar escala + offset (X/Y/Z).

## Iteração 5 — Texturas
- Import de OBJ da engine é só geometria; `TextureLoader` **auto-encontra** a textura irmã: lê o `.mtl` (`map_Kd`) ou um PNG/JPG de mesmo nome ao lado do modelo (caminho pego de `FileBrowser.Result`).
- Botão **"Textura…"** no painel: abre o file browser (PNG/JPG) e aplica ao vivo (`SetTexture`).
- Parte **com textura** usa shader lit simples (mostra o PNG com as cores dele); **sem textura** mantém o shader do personagem (pintável).
- Caminho da textura persistido por modelo no `scales.json` e recarregado ao reimportar.

## Iteração 6 — Ids estáveis
- Id da parte passou a ser **estável por modelo** (baseado no nome do arquivo, sem contador de sessão): reimportar o mesmo arquivo não duplica a entrada na aba e o comportamento fica determinístico.

## Iteração 7 — Normalização de escala no import
- Diagnóstico (logs `[diag]`/`[bones]` de teste real): a parte prende no osso animado certo (`Head`, segue o corpo); sem exceções no BepInEx nem no `Player.log` do Unity. Bug de usabilidade: OBJs importam com `mesh.bounds.size` ≈ 0 (o usuário precisou de escala 125).
- `ImportFlow.NormalizeScale`: calcula a escala inicial pra a **maior dimensão da malha ≈ `DefaultScale` unidades de mundo** — a parte nasce visível independente de o OBJ vir minúsculo/gigante. Config `DefaultScale` passou a significar "tamanho-alvo".

## Iteração 8 — Tamanho híbrido (último usado como multiplicador) — P1b
- Ao importar um modelo **novo** (sem valor próprio salvo), a escala inicial = **normalizada × último multiplicador** do usuário; offset = último offset. Reproduz a preferência de tamanho quando os modelos são consistentes, sem estourar quando vêm de fontes/escalas variadas.
- `Confirmar` agora salva **dois** níveis: por-modelo (escala **absoluta** exata, em `scales.json`) **e** um "último usado" global (`__last__`, escala como **multiplicador** sobre a normalizada + offset). `CustomPart.NormalizedScale` guarda a base pra calcular o multiplicador.
- Decisão de design registrada: normalização garante *visível*, não *correto*; último-absoluto reproduz a escolha mas quebra em escalas diferentes; o **híbrido** dá as duas garantias. (Usuário confirmou: modelos vêm de fontes variadas às vezes.)

## Iteração 9 — Persistência entre sessões (P0)
- Ao **Confirmar**, o `scales.json` passa a guardar também o **caminho do arquivo do modelo + categoria + slot** (`ScaleStore` estendido; `CustomPart.ModelPath`).
- `PersistenceLoader.LoadAll` roda **uma vez** na abertura do criador (postfix de `EditProp`): varre os modelos salvos e **recarrega cada um** — a peça reaparece como opção na aba, já com escala/posição/textura, **sem reimportar à mão**.
- Recarga **silenciosa** da malha: reaproveita o `MeshImporter.ImportNew` privado por reflexão (`Compat.ImportMesh`) — carrega OBJ/STL direto do disco, sem abrir o file browser.
- Robustez: arquivo movido/ausente → pula com aviso; registro antigo (pré-P0, sem caminho) → pulado até o usuário Confirmar de novo uma vez; GLB no reload ainda não (precisa do caminho async) — fica pra depois.
- **Resolve** o "salvei a cabeça mas sumiu depois de reiniciar".

## Iteração 9.1 — Bug de serialização do `scales.json` (persistência nunca gravava)
- Sintoma: `scales.json` = `{}` vazio e `[persist] 0 recarregados`. Descoberto ao testar a P0.
- Causa: `JsonUtility` não serializa uma `List<>` de **classe privada aninhada** (o objeto-raiz privado virava `{}`) — então **nenhum** "Salvo" era realmente escrito no disco desde a Iteração 3 (só funcionava em memória, na sessão).
- Correção: `ScaleEntry` virou classe **pública de topo** e o arquivo agora é **JSONL** (um objeto JSON por linha) — objeto plano único sempre round-trips no `JsonUtility`. Logs `[store] gravados/carregados N registro(s)` pra confirmar.

## Iteração 10 — Rotação, escala não-uniforme, reedição, gênero e navegação (P5/P4/P6/P3/P12)
- **P5 — Rotação X/Y/Z digitável.** `CustomBodyPartAttachment` ganhou `UserEuler`; o `Reapply` aplica
  `Quaternion.Euler(UserEuler)` como rotação **relativa ao osso** e recompensa o pivô para a peça
  **girar em torno do próprio centro** (não voar longe). 3 campos "Rotação" no painel; persistido.
- **P4 — Escala não-uniforme (X/Y/Z).** Novo `UserScaleAxis` (Vector3, multiplicador **sobre** a escala
  uniforme; 1/1/1 = sem esticar). O `Reapply` usa `s = UserScale * UserScaleAxis` por eixo, preservando
  a lógica híbrida de normalização (que continua operando na escala uniforme). 3 campos "Esc XYZ";
  guarda de 0/negativo (`SanitizeAxis`); persistido (registros antigos sem eixo → tratados como 1/1/1).
- **P6 — Reedição + "padrão" vs "só desta vez".** Botão **"E"** (azul) por item custom, ao lado da lixeira,
  reabre o `ScaleSession` (`PartEditor.Reopen` aplica a parte se preciso e liga o painel à instância viva).
  O painel agora tem **"Salvar padrão"** (grava no `scales.json`, = Confirmar antigo) e **"Só desta vez"**
  (aplica ao vivo e fecha **sem** persistir → próximo import do arquivo volta ao padrão salvo).
- **P3 — Feminino/Masculino.** Seletor "Gênero: Ambos/Feminino/Masculino" no painel. Grava a tag
  `Feminine`/`Masculine` no `PropDatabaseData.tags` da parte (`CustomPartCatalog.SetGender`) e dá `Refresh`,
  reusando o filtro nativo `FilterExcludeByTags` do criador. Persistido por modelo.
- **P12 — Setas ◀ ▶ de navegação.** Injetadas abaixo do "Importar Parte" (`NavArrows`). Reusam o
  `tabSystem.selector` (o predicado de filtro já montado pela engine) sobre `CharacterCreator.attachmentPaths`
  para listar as opções **visíveis na ordem da tela** (path + gênero + busca + favoritos), acham a atual
  aplicada no dummy e aplicam a próxima/anterior via `SpawnAlongside` (desliga a atual, liga a alvo).
  Vale para partes **nativas e customizadas**.
- **Store:** `PartTransform`/`ScaleEntry` estendidos com `scaleAxis` (sx/sy/sz), `euler` (rx/ry/rz) e
  `gender`; JSONL retrocompatível (campos ausentes em registros pré-P4/P5 assumem defaults). Build 0/0.

## Iteração 10.3 — "Salvar padrão" agora é por categoria (não só por modelo)
- Pedido: "Salvar padrão" deveria valer para **todos os modelos daquela parte do corpo** (ex.: toda cabeça
  importada já vem ajustada), não só para o arquivo salvo.
- **Feito:** `ScaleStore.SetCategoryDefault`/`TryGetCategoryDefault` guardam um **padrão por aba/categoria**
  (chave `__cat__<savePath>` — cada aba tem o seu; "Rostos" e "Cabeças" são independentes, escolha do usuário
  sobre agrupar por osso): escala como **multiplicador** sobre a normalizada + eixo (P4) + rotação (P5) +
  posição. `Confirmar` ("Salvar padrão") grava esse padrão da categoria **além** do registro por-modelo
  (que continua existindo pra P0/reimport do mesmo arquivo).
- **Seeding no import** (`ImportFlow`): modelo já salvo → valores exatos dele; senão, se a **categoria tem
  padrão** → normalizada×mult + eixo/rot/pos daquele slot; senão → fallback global `__last__` (P1b).
- **Fora do padrão de categoria (de propósito):** gênero e textura continuam **por-modelo** (não fazem
  sentido como padrão de categoria). Toast novo: "Padrão salvo: novos modelos desta categoria virão assim".

## Iteração 11.1 — Peça custom não era substituída por peça nativa do mesmo slot
- Bug: com um torso custom aplicado, selecionar um torso **nativo** deixava os dois (não substituía).
  Causa: a peça custom mora no **osso animado**; a nativa, ao entrar, só limpa o **socket** dela, então
  não removia a custom.
- Correção: `Patch_AddPart` agora, ao aplicar **qualquer** peça (nativa ou custom), identifica o slot da
  peça que entra (`CategoryMap.TryToSocket` pela savePath da nativa; `part.Slot` se custom) e remove do
  preview qualquer peça **custom** naquele slot (`RemoveCustomInSlot`) antes de adicionar. `TryToSocket`
  novo retorna false quando nenhum segmento conhecido bate (evita falso-positivo caindo em "head").

## Iteração 13.4 — Olhos: ajuste fino de posição + olho persiste ao trocar cabeça
- **Posição muito agressiva** (usuário tinha de digitar 0,0x à mão): nova linha **"Passo"** no painel
  com campo de passo **ajustável** (padrão 0.05) + botões **X– X+ Y– Y+ Z– Z+** que empurram cada eixo
  pelo passo. Painel cresceu 340→424. (`ScaleSession.NudgePos/PosStep/RefreshOffsetFields`.)
- **Trocar a cabeça/rosto removia o olho** → agora o olho **só sai se clicado de novo**. Causa: colocar
  uma peça nativa roda o `RemoveAllChildren` do engine no socket, destruindo o olho (que vive sob ele);
  e meu `RemoveCustomInSlot` também removia peças custom do mesmo slot. Correções: (a) `RemoveCustomInSlot`
  **ignora peças aditivas**; (b) novo `AdditiveParts` guarda a **intenção** do usuário (ligado via
  `SpawnAlongside`) e um **postfix em `AddPart`** re-aplica qualquer aditiva sumida depois de colocar uma
  peça (com guarda anti-recursão + layer 21); (c) `ResetParts` limpa a intenção. Build 0/0.

## Iteração 13.3 — Fatia 3: aditivo (não substitui a cabeça), Z +0.12, seleção não-dupla
- Log resolveu a ancoragem: o botão agora nasce em `.../Parts/Content/Head/TabHeaders/EyesCategoryButton`,
  junto de `TabButton_All/Face/Race/Hair` (as "quatro opções de cabeça"). A heurística "mais próximo do
  `tabSystem`" ganhou dos botões de região do manequim (`New Tab Buttons/Horn/Helm/Head/...`).
- **Importar olho substituía a cabeça** → agora **aditivo**. `CustomPart.Additive` (true p/ olhos); o
  `CustomBodyPartAttachment.Build` pula os `RemoveAllChildren` quando aditivo, então o olho é anexado
  SEM limpar o slot da cabeça. Aplicado no import e no reload (`PersistenceLoader`).
- **Olhos um pouco à frente:** novo olho começa com **Z = 0.12** de posição (pedido do usuário); o offset
  "último usado" de outras peças não se aplica a olhos.
- **"Duas selecionadas":** as TabHeaders são módulos de um `SimpleTabSystem` (seleção = `indicator`/
  sublinhado). O clone não era módulo → sublinhado nativo + do clone. `IntegrateSelection` acha o
  `SimpleTabSystem` dono da fileira e gerencia **só o sublinhado** (nunca o `content`, então itens nunca
  somem): clicar Olhos acende o nosso e apaga os nativos; clicar uma aba nativa apaga o nosso. Build 0/0.

## Iteração 13.1 — Fatia 3: botão "Olhos" DENTRO do painel + ícone branco (feedback em jogo)
- **Problema (print):** o botão do olho nasceu flutuando no canto sup. direito (perto do "Importar Parte")
  e com ícone colorido. Pedido: ficar **dentro do painel**, junto das opções de categoria de cabeça, e o
  símbolo **totalmente branco** (padrão do engine).
- **Descoberta:** `UiButton.onLeftMouseClick` é `UnityEvent<PointerEventData>`; os botões de categoria do
  criador guardam um **persistent listener** apontando pra `itemTabsLoader.Set*Path*` (fiado na cena).
  Dá pra achá-los por reflexão de UnityEvent (`GetPersistentTarget/MethodName`).
- **Correção (`EyesTabButton` reescrito):** acha um botão de categoria nativo (target == `itemTabsLoader` e
  método contendo "Path" — exclui gênero/busca que usam `FilterExcludeByTags`), **clona** ele (herda
  tamanho/estilo/slot de ícone), **neutraliza** os listeners persistentes clonados
  (`SetPersistentListenerState(Off)`), põe o **ícone de olho branco** e re-parenteia como irmão (mesma
  fileira, `SetAsLastSibling`). Prefere um botão **visível agora** (a fileira de cabeça que está aberta).
  `EyeIcon` agora é **branco monocromático** (contorno amêndoa + pupila). Log `[eyes]`/`[eyes][diag]`
  (dump dos botões da cena) se não achar âncora, pra ajustar sem adivinhar. Build 0/0.

## Iteração 13 — P2 Pintura RGB (Fatia 3: categoria "Olhos" própria com ícone de olho)
- Pedido (confirmado): olhos viram uma **aba/categoria própria** no criador, com **ícone de olho**,
  separada das demais. O engine **não tem** categoria nem socket de olhos nativos (olhos são só uma
  **cor** via `SetEyesColor`); então a categoria é sintética.
- **Validado no DLL:** `SetEyesColor` → `SetColor(Colors.Part.eyes)` → `SetColor("_Color_Eyes", cor)`,
  que é exatamente o broadcast que o `Patch_Paint` já intercepta → peças de olho texturizadas já pintam
  pelo picker de olhos **sem patch novo**. Todos os ids de `ChannelMap` conferidos 1:1 com `GetColorId`.
  Botões de categoria do criador são **fiados na cena** (`SetPathFilter`/`SetRootPath` nunca chamados em
  código) → a aba nova é um botão injetado (clonado do `createNew`), não um registro no sistema de abas.
- `EyesCategory.cs`: prefixo `savePath` único `["CustomParts","eyes"]` (nenhuma peça nativa usa) + o
  segmento `"eyes"` que mantém `CategoryMap`/`ChannelMap` independentes de idioma.
- `EyeIcon.cs`: **ícone de olho desenhado em código** (Texture2D→Sprite): lente amêndoa (interseção de 2
  círculos), íris azul, pupila e brilho — sem depender de asset/fonte (evita o problema de glifo das setas).
- `EyesTabButton.cs` + `UiFactory.IconButton`: botão de categoria "Olhos" (ícone) ao lado do "Importar
  Parte"; clique → `itemTabsLoader.SetPathFilter(EyesCategory.Path)` (mostra só olhos, via método público
  do próprio engine). `CategoryMap`: `eyes`→socket `face`. `ImportFlow`: aceita a categoria Olhos (prefixo
  curto) mantendo a exigência de ≥3 segmentos p/ as nativas. Persistência/reload reaproveitados. Build 0/0.

## Iteração 12 — P2 Pintura RGB (Fatia 2: seletor de Canal no painel + persistência por-modelo)
- Pedido: além do canal automático por categoria (Fatia 1), um **seletor de Canal** no painel de edição
  para **corrigir qualquer mapeamento** (ex.: forçar Pele em vez de Roupa), com persistência por-modelo.
- `ScaleSession`: nova **linha "Canal"** — um botão largo que cicla pelos canais amigáveis
  (Pele, Cabelo, Olhos, Primário/torso, Secundário/pernas, Couro A/B, Metal A/B, Metal escuro, Brilho).
  `CycleChannel` atualiza `Part.ChannelId` **ao vivo** (é o alvo que o `Patch_Paint` casa no broadcast do
  picker). Painel cresceu 300→340px; linhas Gênero/Textura e os botões de salvar desceram pra abrir espaço.
- `ScaleStore`/`PartTransform`/`ScaleEntry`: campo `channel` já persistido no JSONL (por-modelo).
  `Confirm` grava `channel = _channel`; `ImportFlow` usa o override salvo do canal, senão o automático
  (`ChannelMap.ForCategory`); `PersistenceLoader` aplica `rec.channel` no reload. Build 0/0.

## Iteração 11 — P2 Pintura RGB (Fatia 1: canal por categoria + tinta textura×cor)
- Design (usuário): cada peça pinta por **um canal**; tinta = **textura × cor** (mantém sombras); textura é
  o padrão e só pinta quando o usuário mexe no picker; **reabrir a engrenagem reverte** pra textura.
- `ChannelMap.cs`: categoria (savePath) → canal (`_Color_Primary` p/ torso, `_Color_Secondary` p/ pernas,
  couro/metal/emission conforme a peça; cabelo/olhos/pele). `CustomPart.ChannelId`.
- `CustomBodyPartAttachment`: peça texturizada agora guarda um material lit tintável (`_tintMat`);
  `ApplyPaint(cor)` seta `_BaseColor` (textura × cor), `ResetPaint()` volta a branco (textura pura).
- `Patch_Paint` (postfix em `PickupableCharacter.SetColor(string,Color)`, o broadcast do picker — não
  dispara no `SetAllOn` inicial): tinge a peça custom cujo canal casa. `Patch_AttachmentColor` silencia o
  `SetColor` base nas peças texturizadas (material lit não tem `_Color_*`).
- `PartEditor.Reopen` → `ResetPaint` (engrenagem volta à textura). Canal derivado da categoria no import e
  no reload (`PersistenceLoader`). Untextured seguem no material do personagem (pintura antiga). Build 0/0.

## Iteração 10.4 — Ajustes de UI (feedback em jogo)
- **Setas P12 mais perto do boneco:** antes ancoradas nas bordas do preview (canto a canto da tela); agora
  no **centro do painel** ±140px, ladeando o modelo. (`NavArrows.Make`)
- **Setas agora SEGUEM o boneco** (10.4b): abrir um painel lateral faz a câmera deslocar o modelo pro lado
  (`CharacterCreatorCamera.Move`), então as setas fixas ficavam tortas. `NavArrowFollow` projeta a posição
  do `dummy` pela câmera (`cam.WorldToViewportPoint`) a cada `LateUpdate` e reposiciona as setas em ±140px
  em torno do modelo na tela.
- **Botão "Importar Parte" mais à esquerda:** estava clipado na borda direita; `anchoredPosition.x -= 230`.
- **Arrastar o painel girava a câmera — corrigido.** Causa: o SlickUi tem um input próprio
  (`SlickInputController`) que, no clique, escolhe **o `UiButton` mais à frente** (`uiButtonHits.FirstOrDefault()`,
  montado do raycast) e roteia o arrasto pra ele; `Image` puro é ignorado, então o clique vazava pro
  `inputPanel` do preview → `CharacterCreatorCamera.MouseDrag` girava o boneco. Correção: `AddDragSurface`
  põe um **`UiButton` transparente cobrindo o painel** (clonado do `createNew`, handlers vazios) como
  primeiro filho → ele vira o alvo frontal e absorve o clique; o movimento do painel segue no `DragHandle`
  da raiz (eventos padrão sobem até ele).

## Iteração 10.2 — Painel arrastável
- Pedido: o painel de edição fixo tapava o boneco e atrapalhava ver as mudanças.
- `DragHandle` (novo, `IBeginDragHandler`/`IDragHandler`) move o `RectTransform`-alvo pelo delta do mouse
  (÷ `canvas.scaleFactor`). Adicionado ao **painel inteiro** (áreas vazias/barra/título arrastam; os campos
  mantêm seu próprio drag de texto). Barra de título no topo (40px) + dica "arraste para mover" como affordance.
- A posição arrastada é lembrada na **sessão** (`ScaleSession._lastPanelPos`), então reabrir o painel mantém o lugar.

## Iteração 10.1 — Setas P12: posição e visual
- **Problema:** as setas nasceram no painel esquerdo (abaixo de "Importar Parte") e o glifo `◀`/`▶` não
  existe na fonte da cena → aparecia como quadrado.
- **Correção:** `NavArrowButton` (novo) desenha a seta como **triângulo** via `MaskableGraphic.OnPopulateMesh`
  (sem fonte/sprite), com halo escuro pra contraste. `NavArrows` agora ancora as setas **ladeando o boneco**:
  filha do `CharacterCreatorCamera.inputPanel` (o painel do preview), esquerda na borda esquerda (aponta ◀),
  direita na borda direita (aponta ▶). Clique via `IPointerClickHandler`. Precisou referenciar
  `UnityEngine.TextRenderingModule` (tipo `UIVertex`).

---

### Limitações conhecidas (hoje)
- **Persistência de malha entre sessões (P0) já existe** para OBJ/STL: `Confirmar`/"Salvar padrão" grava
  caminho+categoria+slot e o `PersistenceLoader` recarrega ao abrir o criador. **GLB no reload ainda não**
  (precisa do caminho async); registros pré-P0 (sem caminho) precisam de **um** "Salvar padrão" novo.
- **Gizmo de setas (arrastar com mouse) ainda NÃO foi construído.** O gizmo nativo (`Locator`/`GroupManipulator`) não é reutilizável no criador (é preso a props de cena, `Camera.main`, rede). Um gizmo próprio está no roadmap.
- Cabeça/torso: anexo rígido é ideal para **acessórios**; substituir malhas *skinned* nativas perfeitamente é Nível B (GLB rigado).
