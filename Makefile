all: linux windows

linux:
	dotnet publish -p:PublishSingleFile=true --self-contained --os linux

windows:
	dotnet publish -p:PublishSingleFile=true --self-contained --os win
