# aplicativoFinanceiro

Desenvolvimento de app de finanças simples e que serve pra ajudar gente descontrolada financeiramente.

**Orçamento comprometido**: controle financeiro pessoal, local e offline, para Windows (pt-BR / BRL). Sem servidor e sem conta: os dados ficam no computador, **criptografados com a sua senha** (PBKDF2 + AES-256-GCM).

## O que tem
- Painel com saldo livre, % comprometido, parcelamentos, projeção de 6 meses e **insights automáticos** (regras fixas, sem IA).
- Receitas, despesas (fixas, parceladas, únicas), ajustes por mês, checklist "Pago" e dia de vencimento.
- Projeção de 12 meses com simulador de compra.
- **Lista de compras** (cabe ou não na sobra do mês), **reserva de emergência** (valor digitado por você) e **extrato bancário** (importa CSV/OFX e compara planejado × real).
- Backup manual e automático (pasta escolhida por você), 4 temas, bloqueio por inatividade.

## Estrutura
| Pasta | Conteúdo |
|---|---|
| `app/` | O aplicativo (`index.html` único, JavaScript puro) e ferramentas. `seed.js` aqui é a base **vazia**. |
| `instalador/` | Lançador nativo (C#, WebView2, bandeja ao fechar), instalador e `montar-pacote.ps1`. |
| `v2-com-ia/` | Versão experimental com assistente de IA (Ollama local, Gemini ou Claude). |

## Como usar
- **Rápido:** abra `app/index.html` no Edge ou no Chrome.
- **Como programa do Windows:** compile o lançador (`app/ferramentas/compilar-lancador.bat`) ou monte o instalador (`instalador/montar-pacote.ps1 -Fonte app -Compartilhar`). O instalador precisa das DLLs do WebView2 em `instalador/host/lib` (pacote NuGet `Microsoft.Web.WebView2`), que não estão no repositório.

## Privacidade
Seus dados nunca vão para este repositório: o `seed.js` versionado é vazio, e os dados reais ficam só no seu computador. Arquivos de backup (`.json`) **não são criptografados**; guarde-os com cuidado.