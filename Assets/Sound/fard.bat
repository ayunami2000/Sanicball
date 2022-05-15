@echo off
for /R .\ %%a in (*.ogg) do (
ffmpeg -i "%%a" -y -c:a libvorbis -ab 32k -ar 22050  -map a "%%a.fard.ogg"
del "%%a"
move "%%a.fard.ogg" "%%a"
)
