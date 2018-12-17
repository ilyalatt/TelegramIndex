set -e

./build.sh

docker build -t sns-index .
rm -r build

hyper images | awk '{ print $1,$3 }' | grep '<none>' | awk '{ print $2 }' | xargs hyper rmi
hyper load -l sns-index
docker images | awk '{ print $1,$3 }' | grep sns-index | awk '{ print $2 }' | xargs docker rmi
hyper service rolling-update --image sns-index sns-index
