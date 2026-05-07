param(
    [string]$ProjectDir
)

# 如果 VS 没传 ProjectDir，就使用脚本所在目录
if ([string]::IsNullOrWhiteSpace($ProjectDir)) {
    $ProjectDir = $PSScriptRoot
}

# 清理 VS 传入路径里可能带的多余引号
$ProjectDir = $ProjectDir.Trim()
$ProjectDir = $ProjectDir.Trim('"')

# 转成绝对路径
$ProjectDir = [System.IO.Path]::GetFullPath($ProjectDir)

$buildTime = Get-Date -Format "yyyyMMdd_HHmmss"
$path = [System.IO.Path]::Combine($ProjectDir, "BuildInfo.cs")

Write-Host "ProjectDir = $ProjectDir"
Write-Host "BuildInfo path = $path"
Write-Host "Build time = $buildTime"

$content = @"
namespace Aron_V2
{
	public static class BuildInfo
	{
		public const string BuildDate = "$buildTime";
	}
}
"@

# 用 .NET 写文件，比 Set-Content 更稳，避免中文路径或编码问题
$encoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($path, $content, $encoding)

Write-Host "BuildInfo generated successfully."