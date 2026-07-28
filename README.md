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
cd C:\Users\GabrielFalsetti\source\repos\GabrielFalsetti\FalParcel
dotnet run
```

No Chrome/Edge do celular: **Instalar app** / **Adicionar à tela inicial**.

## Publicar no Cloudflare Pages (Git)

No painel do projeto Pages:

| Campo | Valor |
|--------|--------|
| **Build command** | `chmod +x build.sh && ./build.sh` |
| **Build output directory** | `output/wwwroot` |
| Framework preset | None |

Não use `dotnet run` — o Cloudflare não tem .NET instalado; o `build.sh` instala o SDK 9 e faz o `publish`.

Depois do push, o deploy sobe sozinho.
