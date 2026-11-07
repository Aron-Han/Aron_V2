param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference = 'Stop'
# Static byte scan only. Never deserialize, execute or save a VPP.
$files = @(Get-ChildItem -LiteralPath $Root -Recurse -File -Filter '*.vpp')
$result = foreach ($file in $files) {
    $stream = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $hash = [Security.Cryptography.SHA256]::Create()
    $identities = New-Object 'System.Collections.Generic.HashSet[string]'
    $buffer = New-Object byte[] 1048576
    $tail = ''
    try {
        while (($count = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            [void]$hash.TransformBlock($buffer, 0, $count, $buffer, 0)
            $text = $tail + [Text.Encoding]::ASCII.GetString($buffer, 0, $count)
            foreach ($match in [regex]::Matches($text, 'Cognex\.[A-Za-z0-9_.]+, Version=[0-9.]+, Culture=[A-Za-z0-9_-]+, PublicKeyToken=[a-fA-F0-9]+')) { [void]$identities.Add($match.Value) }
            $tail = $text.Substring([Math]::Max(0, $text.Length - 1024))
        }
        [void]$hash.TransformFinalBlock((New-Object byte[] 0), 0, 0)
        [pscustomobject]@{
            Path = $file.FullName
            Bytes = $file.Length
            Sha256 = ([BitConverter]::ToString($hash.Hash)).Replace('-', '')
            VisibleAssemblyReferences = @($identities | Sort-Object)
            ForeignVersions = @($identities | Where-Object { $_ -notmatch 'Version=71\.2\.0\.0,' })
            Note = 'Readable serialized identity strings only; not a structural integrity or execution test.'
        }
    } finally { $stream.Dispose(); $hash.Dispose() }
}
ConvertTo-Json -InputObject @($result) -Depth 5
