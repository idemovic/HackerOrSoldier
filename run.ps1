$ErrorActionPreference = "Stop"

# Godot only compiles C# from the editor; running the game loads the prebuilt
# assembly from .godot/mono/temp/bin. Build first so we never run a stale DLL.
dotnet build "$PSScriptRoot\HackerOrSoldier.csproj"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& "C:\code\Godot\Godot_v4.7-stable_mono_win64_console.exe" --path $PSScriptRoot
