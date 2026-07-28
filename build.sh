#!/bin/sh
set -eu

curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh -c 9.0 -InstallDir ./dotnet

./dotnet/dotnet --info
./dotnet/dotnet publish -c Release -o output

# Garante SPA fallback para rotas do Blazor
printf '/*    /index.html   200\n' > output/wwwroot/_redirects
