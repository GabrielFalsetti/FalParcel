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
