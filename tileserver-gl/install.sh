
#For linux

#Install docker
sudo apt-get update
sudo apt install -y docker.io
sudo systemctl enable --now docker

#Check docker
docker --version
sudo docker run hello-world

#Run docker
sudo docker run --rm -p 127.0.0.1:8081:8080 -v /home/gilad/dev/cesium/data:/data -v /home/gilad/dev/cesium/cesium-server/tileserver-gl:/config maptiler/tileserver-gl --config /config/config.json

#For windows
docker run --rm -p 127.0.0.1:8081:8080 -v C:/Users/gilad/source/repos/Cesium/data:/data -v C:/Users/gilad/source/repos/Cesium/cesium-server/tileserver-gl:/config maptiler/tileserver-gl --config /config/config.json

#Test tileserver-gl
curl http://127.0.0.1:8081/health

