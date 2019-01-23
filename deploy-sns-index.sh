set -e 

./build.sh

ep=root@sns-index.com
ssh $ep "cd sns-index && cp -r bin bin-new"
rsync -r build/. $ep:/root/sns-index/bin-new
rm -rf build
ssh $ep "(pkill dotnet || true) && cd sns-index && rm -rf bin && mv bin-new bin"
ssh $ep "cd sns-index && ./run.sh"
