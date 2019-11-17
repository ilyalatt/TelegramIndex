#!/bin/bash
set -e

rm -rf build
cd src/api
dotnet publish --configuration Release --runtime linux-x64 --output ../../build

cd ../web
rm -rf dist
yarn nps build
mv dist ../../build/wwwroot
cd ../..
