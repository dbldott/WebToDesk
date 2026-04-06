Option Explicit

Dim shell, scriptPath, command

Set shell = CreateObject("WScript.Shell")
scriptPath = CreateObject("Scripting.FileSystemObject").GetParentFolderName(WScript.ScriptFullName)
command = """" & scriptPath & "\run_me.bat"" __hidden__"

' 0 = hidden window, False = do not wait for exit
shell.Run command, 0, False
