# Como disponibilizar o mod para outras pessoas

Você distribui **só o mod puro e funcional**, num zip que a pessoa extrai na raiz do jogo.
**Nada seu vai junto** — nenhum modelo 3D, nenhuma peça importada, nenhuma configuração sua.
A outra pessoa recebe o Criador expandido **vazio** e importa os próprios `.obj`.

O que vai no zip (e **só** isso):

```
winhttp.dll                                           <- o loader (BepInEx doorstop)
doorstop_config.ini, .doorstop_version, changelog.txt <- do BepInEx
BepInEx\core\...                                       <- o runtime do BepInEx
BepInEx\plugins\CustomPartsMod\CustomPartsMod.dll      <- O MOD (só o código)
README.txt                                             <- instruções pro usuário
```

O que **NÃO** vai (fica só na sua máquina, na raiz do SEU jogo):

```
CustomParts\            <- suas peças (.obj/.png), scales.json, thumbs\  -> NÃO entra no zip
BepInEx\config\*.cfg    <- suas configurações                            -> NÃO entra no zip
```

> O DLL do mod é só a **ferramenta**. Ele não guarda nenhuma peça sua — as peças ficam na pasta
> `CustomParts\` do seu jogo, que **não** faz parte do pacote. Por isso o zip é sempre "mod limpo".

---

## Passo a passo para gerar o zip

1. **Compile** o mod:
   ```
   dotnet build src/CustomPartsMod.csproj -c Release
   ```
   Saída: `src\bin\Release\CustomPartsMod.dll`.

2. **Copie o DLL** para dentro do pacote de distribuição:
   ```
   copy src\bin\Release\CustomPartsMod.dll dist\package\BepInEx\plugins\CustomPartsMod\
   ```

3. **Compacte o CONTEÚDO de `dist\package\`** (os itens têm que ficar na **raiz** do zip, não dentro
   de uma pasta `package`). No PowerShell:
   ```powershell
   Compress-Archive -Path dist\package\* -DestinationPath dist\CustomPartsMod-v0.1.2.zip -Force
   ```

   > A pasta `dist\package\` já contém **apenas** o mod + o BepInEx — ela nunca teve conteúdo seu.
   > Então o zip gerado é automaticamente "mod puro". Não é preciso limpar nada.

4. (Opcional) **Confira** que o zip só tem o mod e abre com o `winhttp.dll` e o `BepInEx\` na raiz:
   ```powershell
   Expand-Archive dist\CustomPartsMod-v0.1.2.zip -DestinationPath dist\_check -Force
   Get-ChildItem -Recurse dist\_check | Select-Object FullName
   Remove-Item -Recurse -Force dist\_check
   ```
   Você deve ver `winhttp.dll`, `BepInEx\plugins\CustomPartsMod\CustomPartsMod.dll` e **nenhum**
   `CustomParts`, `scales.json` ou `.obj`.

---

## Publicar

- **GitHub Releases** (repo `Expanded-Character-Creator`): crie uma nova *release*, anexe o
  `CustomPartsMod-v0.1.2.zip` e publique.
- Ou simplesmente **envie o zip** (Drive, Discord, etc.).

## O que a pessoa faz para instalar

1. Fecha o jogo.
2. Extrai TUDO do zip para `...\steamapps\common\The RPG Engine\` (a raiz, onde fica
   `The_RPG_Engine.exe`). Deve ficar:
   ```
   The RPG Engine\winhttp.dll
   The RPG Engine\BepInEx\plugins\CustomPartsMod\CustomPartsMod.dll
   ```
3. Abre o jogo uma vez (o BepInEx se inicializa). Pronto.

Nenhum arquivo do jogo é alterado → 100% reversível (basta apagar `winhttp.dll` + a pasta `BepInEx\`).
A pessoa começa com o Criador expandido vazio e usa "Importar Parte"/"Importar Pasta" para carregar
os **próprios** modelos.
