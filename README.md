# FalParcel

PWA Blazor WebAssembly para controle de parcelamentos no celular.

## O que faz

- Totais do mês, por cartão e grade Jan–Dez
- Cadastro de cartões (Configurações)
- CRUD de compras parceladas com dropdown de cartão
- Dados no **localStorage** (offline)
- Exportar modelo Excel vazio / exportar dados / importar Excel (insere as linhas preenchidas)

## Excel

Colunas do modelo:

| Cartão | Compra | Qtd Parcelas | Valor Parcela | Valor Total | Mês Início | Mês Final | Finalizou |

1. Em **Configurações** → **Baixar modelo vazio**
2. Preencha as linhas no Excel
3. **Importar Excel** — as compras são inseridas no app (cartões novos são cadastrados automaticamente)

## Rodar local

```bash
dotnet run
```

No Chrome/Edge do celular: **Instalar app** / **Adicionar à tela inicial**.

## Publicar de graça no Azure (recomendado)

O app é **Blazor WebAssembly** (só arquivos estáticos). Use **Azure Static Web Apps — plano Free** (HTTPS, domínio `*.azurestaticapps.net`, CI/CD via GitHub). Evite App Service pago; o workflow antigo de App Service ficou desativado.

### 1. Criar o Static Web App (Free)

1. Abra o [portal do Azure](https://portal.azure.com) → **Criar um recurso** → **Static Web App**
2. Preencha:
   - **Plano**: **Free**
   - **Região**: a mais próxima (ex.: West US 2 / East US 2)
   - **Origem**: **Other** (ou GitHub se preferir o assistente do portal)
3. Se escolher **Other**, depois do create copie o **Deployment token** em: Static Web App → **Manage deployment token**
4. No GitHub do repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:
   - Nome: `AZURE_STATIC_WEB_APPS_API_TOKEN`
   - Valor: o deployment token

### 2. Deploy automático

O workflow [`.github/workflows/azure-static-web-apps.yml`](.github/workflows/azure-static-web-apps.yml) publica a cada push na branch `1.0.0` (ou via **Actions** → **Deploy Azure Static Web Apps** → **Run workflow**).

Build local equivalente:

```bash
dotnet publish -c Release -o artifacts
# Artefato estático: artifacts/wwwroot
```

A URL do app aparece em **Azure Portal** → seu Static Web App → **Overview** → **URL**.

### 3. PWA no celular

Abra a URL no Chrome/Edge do celular → **Instalar app** / **Adicionar à tela inicial**.

### Alternativa: criar pelo portal com GitHub

Se criar o Static Web App com origem **GitHub**, o Azure pode gerar outro workflow e outro secret. Nesse caso:

- Aponte **App location** para `/` e use build customizado, **ou**
- Mantenha o workflow deste repo e cole o token gerado no secret `AZURE_STATIC_WEB_APPS_API_TOKEN`

Arquivo de rotas/MIME do Blazor: [`wwwroot/staticwebapp.config.json`](wwwroot/staticwebapp.config.json).

## Publicar no Cloudflare Pages (Git)

No painel do projeto Pages:

| Campo | Valor |
|--------|--------|
| **Build command** | `chmod +x build.sh && ./build.sh` |
| **Build output directory** | `output/wwwroot` |
| Framework preset | None |

Não use `dotnet run` — o Cloudflare não tem .NET instalado; o `build.sh` instala o SDK 9 e faz o `publish`.

Depois do push, o deploy sobe sozinho.
