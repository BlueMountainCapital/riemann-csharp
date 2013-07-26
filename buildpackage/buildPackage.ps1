
function ReadLinesFromFile([string] $fileName)
{
 [string]::join([environment]::newline, (get-content -path $fileName))
}

function BuildSolution
{
  [CmdletBinding()]
  param()
  C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe ..\Riemann.sln /t:build /p:Configuration=Debug
}

function GetLatestFullVersionOnNuget()
{
  [CmdletBinding()]
  param()

   $packageDetails = &nuget list riemann-csharp
   $parts = $packageDetails.Split(' ')
   [string]$parts[1]
}

function GetLastVersionNumber()
{
  [CmdletBinding()]
  param()

  $fullVersion = GetLatestFullVersionOnNuget
  $parts = $fullVersion.Split('.')
  [int]$parts[2]
}

function CleanupBuildArtifacts
{
  [CmdletBinding()]
  param()

  del riemann-csharp.nuspec
  del *.nupkg
}

BuildSolution

$nextVersionNumber = (GetLastVersionNumber)
$fullVersion = "1.0.$nextVersionNumber"
write-output "Next package version: $fullVersion"

# make the nuspec file with the target version number
$nuspecTemplate = ReadLinesFromFile "riemann-csharp.nuspec.template"
$nuspecWithVersion = $nuspecTemplate.Replace("#version#", $fullVersion)
$nuspecWithVersion > riemann-csharp.nuspec

nuget pack riemann-csharp.nuspec 

# push to nuget:
$pushCommand = "NuGet Push riemann-csharp.$fullVersion.nupkg"
Invoke-Expression $pushCommand
write-output "Pushed package version $fullVersion"

CleanupBuildArtifacts

write-output "Done"
