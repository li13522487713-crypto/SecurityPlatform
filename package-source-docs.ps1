param(
    [string]$OutputZip = 'SecurityPlatform-source-docs.zip'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = [System.IO.Path]::GetFullPath($root)
$outZip = [System.IO.Path]::GetFullPath((Join-Path $root $OutputZip))
$stage = Join-Path $env:TEMP ("SecurityPlatform_pack_{0}" -f ([Guid]::NewGuid().ToString('N')))

$includeExtensions = @(
    '.cs', '.csproj', '.sln', '.slnx', '.props', '.targets',
    '.json', '.js', '.ts', '.tsx', '.html', '.css', '.scss', '.less',
    '.md', '.txt', '.xml', '.yaml', '.yml', '.http', '.sql', '.cmd', '.bat', '.ps1', '.sh',
    '.config', '.cshtml', '.razor'
)

$excludePatterns = @(
    '(^|\\)\.git(\\|$)',
    '(^|\\)\.vs(\\|$)',
    '(^|\\)\.idea(\\|$)',
    '(^|\\)\.cursor(\\|$)',
    '(^|\\)\.dotnet(\\|$)',
    '(^|\\)node_modules(\\|$)',
    '(^|\\)bin(\\|$)',
    '(^|\\)obj(\\|$)',
    '(^|\\)dist(\\|$)',
    '(^|\\)publish(\\|$)',
    '(^|\\)artifacts(\\|$)',
    '(^|\\)tmp(\\|$)',
    '(^|\\)coverage(\\|$)',
    '(^|\\)\.playwright-cli(\\|$)'
)

try {
    if (Test-Path $outZip) {
        Remove-Item -LiteralPath $outZip -Force
    }

    if (Test-Path $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null

    Write-Host 'Packing source and document files...'

    Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($root.Length).TrimStart('\')

        $shouldSkip = $false
        foreach ($pattern in $excludePatterns) {
            if ($relative -match $pattern) {
                $shouldSkip = $true
                break
            }
        }

        if ($shouldSkip) {
            return
        }

        $ext = $_.Extension.ToLowerInvariant()
        $isSpecialRootFile = $_.Name -in @('.gitignore', '.gitattributes', '.editorconfig')

        if (($includeExtensions -contains $ext) -or $isSpecialRootFile) {
            $destination = Join-Path $stage $relative
            $parent = Split-Path -Parent $destination

            if (-not (Test-Path $parent)) {
                New-Item -ItemType Directory -Path $parent -Force | Out-Null
            }

            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
        }
    }

    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $outZip -CompressionLevel Optimal
    Write-Host "打包完成：$outZip"
}
finally {
    if (Test-Path $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
}
