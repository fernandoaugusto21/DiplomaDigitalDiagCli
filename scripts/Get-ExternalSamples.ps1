<#
.SYNOPSIS
    Baixa os XMLs de exemplo publicados pela UFSC (diploma fictício "João Carlos") para os testes
    de interoperabilidade (ExternalSampleTests).

.DESCRIPTION
    Fonte: https://diplomas.ufsc.br/exemplos-de-arquivos-do-diploma-digital/
    Os arquivos NÃO são redistribuídos neste repositório: não têm licença explícita e as assinaturas
    usam o certificado e-CPF real (ICP-Brasil) de uma pessoa real. O destino está no .gitignore.
#>
[CmdletBinding()]
param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\code\DiplomaDiagnosticCli.Tests\external-samples')
)

$ErrorActionPreference = 'Stop'

$samples = [ordered]@{
    'ufsc-diploma-v1.00-sem-assinatura.xml' = '1WrEARv-0NmxOTHpS6e1N9AuF8BbvhwGI'
    'ufsc-diploma-v1.00-assinado.xml'       = '1N5rsa0ZlmBPa6Yoh7M98zLkLIzfplacC'
    'ufsc-diploma-v1.01-sem-assinatura.xml' = '1IWI7eEehFf47VSLU1p-IWPlI0kLxRXE4'
    'ufsc-diploma-v1.01-assinado.xml'       = '1IR3dAotrtJfNGMvlUwPuMYGS_SeOsHY4'
}

New-Item -ItemType Directory -Force -Path $Destination | Out-Null

foreach ($name in $samples.Keys) {
    $target = Join-Path $Destination $name
    $uri = "https://drive.usercontent.google.com/download?id=$($samples[$name])&export=download&confirm=t"
    Invoke-WebRequest -Uri $uri -OutFile $target -UseBasicParsing
    $head = Get-Content -Path $target -TotalCount 1
    if (-not "$head".StartsWith('<?xml')) {
        Remove-Item $target
        throw "Download de $name não retornou XML (o Google Drive pode ter mudado a URL de download)."
    }
    Write-Host "OK  $name ($((Get-Item $target).Length) bytes)"
}
