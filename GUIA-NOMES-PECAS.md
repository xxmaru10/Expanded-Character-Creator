# Guia de nomeação das peças — import no RPG Engine

Como nomear os `.obj` das peças cortadas para que o mod coloque cada uma na
categoria/slot/lado corretos automaticamente, usando o botão
**"Importar Pasta (texturas compartilhadas)"**.

## Regra de ouro

**O nome da parte vem no INÍCIO do arquivo.** Depois dele vem o resto (o nome da roupa),
que pode ser qualquer coisa.

Formato:

```
<parte>_<lado>_<nome-da-roupa>.obj
```

- Peça com lado: `braço_esquerdo_Blazer.obj`
- Peça sem lado (única): `torso_Blazer.obj`

Se o nome da parte **não** estiver no começo (ex.: `Blazer_braço_esquerdo.obj`),
o mod **não** reconhece e pula a peça.

## Tabela de prefixos

| Comece o nome com… | Vai para | Tem lado? |
|---|---|---|
| `torso_` (ou `peito_`) | Torso | Não (peça única) |
| `braço_esquerdo_` / `braço_direito_` | Braço (parte de cima do braço) | Sim |
| `antebraço_esquerdo_` / `antebraço_direito_` | Antebraço | Sim |
| `mão_esquerda_` / `mão_direita_` | Mão | Sim |
| `parte_de_cima_` (ou `coxa_`, `quadril_`, `cintura_`) | Parte de cima da perna (quadril) | Não (peça única) |
| `panturrilha_esquerda_` / `panturrilha_direita_` | Panturrilha (parte de baixo da perna) | Sim |

## Lado (esquerda / direita)

Nas peças **com lado**, o nome precisa conter:

- `esquerdo` ou `esquerda` → lado **esquerdo**
- `direito` ou `direita` → lado **direito**

Se não houver nenhum dos dois, a peça vai para o lado **direito** por padrão —
então **sempre inclua o lado** nas peças que têm lado.

Nas peças **sem lado** (torso, parte de cima da perna) não coloque lado.

## Acentos

Acento é opcional. `braço` = `braco`, `mão` = `mao`, `antebraço` = `antebraco`,
`panturrilha` = `panturrilha`. Escreva do jeito que preferir.

## Texturas

- Deixe os PNGs da roupa **na mesma pasta** das peças.
- O **nome dos PNGs não importa** para o pareamento — o botão de import compartilha
  **todas as texturas da pasta** entre **todas as peças da pasta**.
- Ou seja: 1 conjunto de texturas por roupa serve todas as partes dela. Não precisa
  copiar textura por peça.
- A primeira variante (a textura base) já vem selecionada; troque pelas outras na
  barrinha `< i / N >` no topo, dentro do jogo.

## Estrutura de pastas

- **1 roupa = 1 pasta**, com os `.obj` das partes + os PNGs das cores.
- Você pode ter **várias roupas em subpastas** dentro de uma pasta maior (ex.: `Top`).
- No import, selecione a **pasta de cima** — o mod entra em cada subpasta sozinho e
  importa tudo (as texturas são compartilhadas **por subpasta**, nunca entre roupas
  diferentes).

## Como importar no jogo

1. Abra o Criador de Personagens.
2. Clique em **"Importar Pasta (texturas compartilhadas)"**.
3. Selecione a pasta (subpastas incluídas).
4. Pronto — cada peça cai na categoria/lado certos e todas as partes de uma roupa
   compartilham as mesmas texturas.

## Exemplo

Pasta de uma roupa (blazer), pronta para importar:

```
Blazer_SolidBlackWhite/
  torso_Blazer_SolidBlackWhite.obj
  braço_esquerdo_Blazer_SolidBlackWhite.obj
  braço_direito_Blazer_SolidBlackWhite.obj
  antebraço_esquerdo_Blazer_SolidBlackWhite.obj
  antebraço_direito_Blazer_SolidBlackWhite.obj
  mão_esquerda_Blazer_SolidBlackWhite.obj
  mão_direita_Blazer_SolidBlackWhite.obj
  Blazer_SolidBlackWhite.png      (cor base / variante 1)
  Blazer_SolidBlackWhite1.png     (variante 2)
  Blazer_SolidBlackWhite2.png     (variante 3)
  …
```

## Erros comuns

- **Prefixo no meio do nome** (`Blazer_braço_...`) → não roteia. O nome da parte tem
  que ser a **primeira** coisa.
- **Prefixo desconhecido** (ex.: `perna_...` sem ser panturrilha/parte de cima, ou um
  nome que o mod não conhece) → a peça é **pulada** com aviso no log
  (`BepInEx\LogOutput.log`, linha `prefixo desconhecido`).
- **Faltou o lado** numa peça com lado → vai para o direito por padrão.
- **Duas peças no mesmo slot** (ex.: dois "braço_esquerdo" na mesma roupa) → uma
  substitui a outra no boneco.
