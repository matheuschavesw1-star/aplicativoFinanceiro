# Cria uma copia do app SEM os seus dados pessoais, pronta para colocar em um pendrive e compartilhar.
# (Arquivo com caracteres ASCII de proposito: o PowerShell 5.1 le arquivos sem BOM como ANSI.)
param([string]$Destino)
$ErrorActionPreference = 'Stop'
$app = Split-Path -Parent $PSScriptRoot

if (-not $Destino) { $Destino = Read-Host "Pasta ou unidade de destino (ex.: E:\ para o pendrive)" }
$Destino = $Destino.Trim('"').Trim()
if (-not (Test-Path -LiteralPath $Destino)) { Write-Host "Destino nao encontrado: $Destino"; exit 1 }

$pasta = Join-Path $Destino 'Orcamento'
if (Test-Path -LiteralPath $pasta) {
  $r = Read-Host "Ja existe '$pasta'. Substituir? (S/N)"
  if ($r -notmatch '^[sS]') { Write-Host "Cancelado."; exit 0 }
  Remove-Item -LiteralPath $pasta -Recurse -Force
}
New-Item -ItemType Directory -Path $pasta | Out-Null

foreach ($f in 'index.html','manifest.webmanifest','sw.js','Orcamento.exe','Abrir Orcamento.bat','LEIA-ME.txt') {
  $src = Join-Path $app $f
  if (Test-Path -LiteralPath $src) { Copy-Item -LiteralPath $src -Destination $pasta } else { Write-Host "Aviso: nao encontrei $f" }
}
Copy-Item -LiteralPath (Join-Path $app 'vendor') -Destination $pasta -Recurse
Copy-Item -LiteralPath (Join-Path $app 'icons')  -Destination $pasta -Recurse
# A versao para compartilhar usa a base VAZIA no lugar dos seus dados:
Copy-Item -LiteralPath (Join-Path $app 'seed-vazio.js') -Destination (Join-Path $pasta 'seed.js')

# Conferencia de seguranca: nenhum dado pessoal pode ter ido junto
$meus = Get-Content -LiteralPath (Join-Path $app 'seed.js') -Raw
$copia = Get-Content -LiteralPath (Join-Path $pasta 'seed.js') -Raw
$vazou = $false
$vazio = Get-Content -LiteralPath (Join-Path $app 'seed-vazio.js') -Raw
if ($copia -ne $vazio) { $vazou = $true }   # o pacote so pode levar a base VAZIA
if ($vazou) { Remove-Item -LiteralPath $pasta -Recurse -Force; Write-Host "ERRO: dados pessoais detectados no pacote. Pacote removido."; exit 1 }

Write-Host ""
Write-Host "Pacote criado em: $pasta"
Write-Host "Ele NAO contem seus dados. Para usar: abrir 'Orcamento.exe' (ou 'Abrir Orcamento.bat')."
