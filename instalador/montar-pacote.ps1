# montar-pacote.ps1 - compila o host (Orcamento.exe) e o instalador e monta a pasta final em .\pacote (ao lado do repositorio)
# Uso:  powershell -ExecutionPolicy Bypass -File montar-pacote.ps1 -Fonte "<pasta do app>" [-Compartilhar] [-Saida "<pasta>"]
#   -Compartilhar      troca o seed.js pelo seed-vazio.js (sem dados pessoais) e confere se nada pessoal vazou
# (Arquivo ASCII de proposito: PowerShell 5.1 le arquivos sem BOM como ANSI.)
param(
  [Parameter(Mandatory = $true)][string]$Fonte,
  [string]$Saida,
  [switch]$Compartilhar
)
$ErrorActionPreference = 'Stop'
$aqui = Split-Path -Parent $MyInvocation.MyCommand.Path
$Fonte = (Resolve-Path -LiteralPath $Fonte).Path
if (-not $Saida) { $Saida = Join-Path (Split-Path -Parent $aqui) 'pacote' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe nao encontrado: $csc" }
$u8 = New-Object System.Text.UTF8Encoding($false)

# 1) host
Write-Host "[1/5] Compilando o host (Orcamento.exe)..."
& cmd.exe /s /c "`"$aqui\host\compilar-host.bat`" `"$Fonte`""
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o host." }

# 2) pasta de preparo do payload
Write-Host "[2/5] Preparando os arquivos do aplicativo..."
$prep = Join-Path $aqui '_build\payload'
if (Test-Path $prep) { Remove-Item $prep -Recurse -Force }
New-Item -ItemType Directory -Path $prep | Out-Null
foreach ($f in 'index.html', 'manifest.webmanifest', 'sw.js', 'LEIA-ME.txt') {
  $src = Join-Path $Fonte $f
  if (Test-Path -LiteralPath $src) { Copy-Item -LiteralPath $src -Destination $prep } else { Write-Host "  aviso: nao encontrei $f" }
}
Copy-Item -LiteralPath (Join-Path $Fonte 'vendor') -Destination $prep -Recurse
Copy-Item -LiteralPath (Join-Path $Fonte 'icons') -Destination $prep -Recurse
if ($Compartilhar) {
  Copy-Item -LiteralPath (Join-Path $Fonte 'seed-vazio.js') -Destination (Join-Path $prep 'seed.js')
  $meus = [IO.File]::ReadAllText((Join-Path $Fonte 'seed.js'), $u8)
  $copia = [IO.File]::ReadAllText((Join-Path $prep 'seed.js'), $u8)
  $vazou = $false
  $vazio = [IO.File]::ReadAllText((Join-Path $Fonte 'seed-vazio.js'), $u8)
  if ($copia -ne $vazio) { $vazou = $true }   # o pacote so pode levar a base VAZIA
  if ($vazou) { Remove-Item $prep -Recurse -Force; throw "ERRO: dados pessoais detectados no pacote. Montagem abortada e arquivos removidos." }
  Write-Host "  seed-vazio.js usado e conferido (sem dados pessoais)."
} else {
  Copy-Item -LiteralPath (Join-Path $Fonte 'seed.js') -Destination $prep
  Write-Host "  ATENCAO: este pacote leva o seed.js COM os dados do dono (use -Compartilhar para outras pessoas)."
}
foreach ($f in 'Orcamento.exe', 'Microsoft.Web.WebView2.Core.dll', 'Microsoft.Web.WebView2.WinForms.dll', 'WebView2Loader.dll') {
  Copy-Item -LiteralPath (Join-Path $aqui "host\bin\$f") -Destination $prep
}
Copy-Item -LiteralPath (Join-Path $aqui 'host\lib\WEBVIEW2-LICENSE.txt') -Destination $prep -ErrorAction SilentlyContinue

# 3) payload.zip (entradas com "/")
Write-Host "[3/5] Gerando payload.zip..."
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zipPath = Join-Path $aqui '_build\payload.zip'
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
$zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
  $base = (Resolve-Path $prep).Path.TrimEnd('\') + '\'
  foreach ($f in Get-ChildItem -LiteralPath $prep -Recurse -File) {
    $rel = $f.FullName.Substring($base.Length).Replace('\', '/')
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $rel, [IO.Compression.CompressionLevel]::Optimal)
  }
} finally { $zip.Dispose() }

# 4) instalador
Write-Host "[4/5] Compilando o instalador..."
if (-not (Test-Path $Saida)) { New-Item -ItemType Directory -Path $Saida | Out-Null }
$exeSaida = Join-Path $Saida 'Instalar Orcamento.exe'
if (Test-Path $exeSaida) { Remove-Item $exeSaida -Force }
$ico = Join-Path $Fonte 'icons\app.ico'
$args2 = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', "/out:$exeSaida", "/resource:$zipPath,payload.zip",
  '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll',
  '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll', '/r:System.dll', '/r:System.Core.dll',
  (Join-Path $aqui 'instalador\Instalador.cs'))
if (Test-Path $ico) { $args2 += "/win32icon:$ico" }
& $csc $args2
if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar o instalador." }

# 5) arquivos de apoio
Write-Host "[5/5] Finalizando a pasta do pacote..."
$leia = @"
ORCAMENTO COMPROMETIDO - INSTALACAO

1) De dois cliques em "Instalar Orcamento.exe" (nao precisa de administrador).
2) Escolha a pasta (o padrao serve) e avance.
3) Pronto: abra pelo atalho "Orcamento comprometido".

Ao clicar no X da janela o Orcamento vai para a bandeja (perto do relogio) e BLOQUEIA; para fechar de
vez use o icone da bandeja > "Fechar de vez" (ou desligue essa opcao nas Configuracoes).

Desinstalar: Configuracoes do Windows > Aplicativos > "Orcamento comprometido".
Requer Windows 10/11 com o Microsoft Edge WebView2 (ja vem no Windows 11).
"@
[IO.File]::WriteAllText((Join-Path $Saida 'LEIA-ME-INSTALACAO.txt'), $leia, (New-Object System.Text.UTF8Encoding($true)))
Write-Host ""
Write-Host "Pacote pronto em: $Saida"
Get-ChildItem $Saida | ForEach-Object { Write-Host ("  {0}" -f $_.Name) }
