#!/bin/bash
set -e

rm -rf build
cd src/api
dotnet publish --configuration Release --output ../../build

cd ../web
rm -rf dist
nps build
mv dist ../../build/wwwroot
cd ../..
