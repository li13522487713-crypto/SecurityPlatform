param(
    [string]$OutputZip = 'SecurityPlatform-frontend-source.zip'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = [System.IO.Path]::GetFullPath($root)
$outZip = [System.IO.Path]::GetFullPath((Join-Path $root $OutputZip))
$stage = Join-Path $env:TEMP ("SecurityPlatform_frontend_pack_{0}" -f ([Guid]::NewGuid().ToString('N')))

$includeExtensions = @(
    '.ts', '.tsx', '.mts', '.cts',
    '.js', '.jsx', '.mjs', '.cjs',
    '.json', '.html', '.htm',
    '.css', '.scss', '.sass', '.less',
    '.md', '.mdx', '.txt', '.xml', '.yaml', '.yml', '.http',
    '.ps1', '.sh', '.cmd', '.bat',
    '.config', '.svg', '.map', '.webmanifest',
    '.vue', '.svelte'
)

$includeNames = @(
    '.editorconfig', '.gitattributes', '.gitignore', '.gitmodules',
    '.npmrc', '.nvmrc', '.node-version',
    '.prettierignore', '.eslintignore', '.dockerignore', '.browserslistrc',
    'Dockerfile', 'Makefile', 'LICENSE', 'LICENCE', '.env.example'
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
    '(^|\\)build(\\|$)',
    '(^|\\)out(\\|$)',
    '(^|\\)publish(\\|$)',
    '(^|\\)artifacts(\\|$)',
    '(^|\\)tmp(\\|$)',
    '(^|\\)temp(\\|$)',
    '(^|\\)coverage(\\|$)',
    '(^|\\)\.playwright-cli(\\|$)',
    '(^|\\)test-results(\\|$)',
    '(^|\\)playwright-report(\\|$)',
    '(^|\\)\.playwright(\\|$)',
    '(^|\\)blob-report(\\|$)',
    '(^|\\)\.turbo(\\|$)',
    '(^|\\)\.vite(\\|$)',
    '(^|\\)\.cache(\\|$)',
    '(^|\\)\.rsbuild-cache(\\|$)',
    '(^|\\)storybook-static(\\|$)',
    '(^|\\)\.pnpm-store(\\|$)',
    '(^|\\)\.vercel(\\|$)',
    '(^|\\)\.netlify(\\|$)'
)

# 遍历时整段跳过，避免对 node_modules 等做全量列举（与上面路径规则一致）
$excludeDirNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
@(
    '.git', '.vs', '.idea', '.cursor', '.dotnet',
    'node_modules', 'bin', 'obj', 'dist', 'build', 'out', 'publish', 'artifacts',
    'tmp', 'temp', 'coverage', '.playwright-cli',
    'test-results', 'playwright-report', '.playwright', 'blob-report',
    '.turbo', '.vite', '.cache', '.rsbuild-cache', 'storybook-static',
    '.pnpm-store', '.vercel', '.netlify'
) | ForEach-Object { [void]$excludeDirNames.Add($_) }

function Get-FrontendSourceFilePaths {
    param([string]$StartDir)
    $files = [System.Collections.Generic.List[string]]::new()
    $stack = [System.Collections.Generic.Stack[string]]::new()
    $stack.Push($StartDir)
    while ($stack.Count -gt 0) {
        $current = $stack.Pop()
        try {
            $dirs = [System.IO.Directory]::GetDirectories($current)
        } catch {
            $dirs = @()
        }
        foreach ($d in $dirs) {
            try {
                $attr = [System.IO.File]::GetAttributes($d)
                if (($attr -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    continue
                }
            } catch { continue }
            $dn = [System.IO.Path]::GetFileName($d)
            if ($excludeDirNames.Contains($dn)) { continue }
            $stack.Push($d)
        }
        try {
            foreach ($f in [System.IO.Directory]::GetFiles($current)) {
                $null = $files.Add($f)
            }
        } catch { }
    }
    return $files
}

try {
    if (Test-Path $outZip) {
        Remove-Item -LiteralPath $outZip -Force
    }

    if (Test-Path $stage) {
        Remove-Item -LiteralPath $stage -Recurse -Force
    }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null

    Write-Host 'Packing frontend source and config files...'

    $rootLen = $root.Length
    foreach ($full in (Get-FrontendSourceFilePaths -StartDir $root)) {
        $relative = $full.Substring($rootLen).TrimStart('\')

        $shouldSkip = $false
        foreach ($pattern in $excludePatterns) {
            if ($relative -match $pattern) {
                $shouldSkip = $true
                break
            }
        }
        if ($shouldSkip) { continue }

        $ext = [System.IO.Path]::GetExtension($full).ToLowerInvariant()
        $name = [System.IO.Path]::GetFileName($full)
        $isIncludedByName = $includeNames -contains $name
        if (($includeExtensions -contains $ext) -or $isIncludedByName) {
            $destination = Join-Path $stage $relative
            $parent = Split-Path -Parent $destination
            if (-not (Test-Path $parent)) {
                New-Item -ItemType Directory -Path $parent -Force | Out-Null
            }
            Copy-Item -LiteralPath $full -Destination $destination -Force
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
