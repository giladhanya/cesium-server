
#download the latest planet file from
#https://planet.openstreetmap.org/pbf/planet-latest.osm.pbf?utm_source=chatgpt.com

#install java 21 or higher

#download planetiler.jar from
https://github.com/onthegomap/planetiler/releases/latest/download/planetiler.jar
#to data folder

#entire documentation is available at
https://github.com/onthegomap/planetiler/tree/main

#run
java -Xmx20g -jar data\planetiler.jar --osm-path=data\planet-260824.osm.pbf --bounds=world --output=data\world.mbtiles --storage=mmap --download --nodemap-type=sparsearray --force
java -Xmx20g -jar data\planetiler.jar --osm-path=data\planet-260824.osm.pbf --bounds=world --output=data\world.pmtiles --storage=mmap --download --nodemap-type=sparsearray --force

pause
