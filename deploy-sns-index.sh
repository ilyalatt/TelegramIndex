#!/bin/bash
set -e 

./build.sh

ep=root@sns-index.com
ssh $ep "cd sns-index && cp -r bin bin-new"
rsync -r build/. $ep:~/sns-index/bin-new
ssh $ep "cd sns-index && ./stop.sh && rm -rf bin && mv bin-new bin && ./start.sh"

rm -rf build
