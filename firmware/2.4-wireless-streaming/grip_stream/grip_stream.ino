#include <ArduinoBLE.h>
#include <BenchConsole.h>
#include "Telemetry.h"
BenchConsole bench;
BLEService rehabService("8c180000-5f55-4d3c-8ac7-7d25a3818f42");
BLECharacteristic telemetry("8c180001-5f55-4d3c-8ac7-7d25a3818f42",BLERead|BLENotify,20,true);
BLECharacteristic calibrationInfo("8c180002-5f55-4d3c-8ac7-7d25a3818f42",BLERead,12,true);
bool bleReady=false;
bool wasConnected=false;
uint32_t sequence=0,lastReport=0;
void setup() {
  bench.begin();
  bleReady=BLE.begin();
  if(!bleReady) { Serial.println("ERR BLE init; USB bench remains available"); return; }
  BLE.setLocalName("GripRehab"); BLE.setAdvertisedService(rehabService);
  rehabService.addCharacteristic(telemetry); rehabService.addCharacteristic(calibrationInfo);
  BLE.addService(rehabService);
  uint8_t packet[20]; rehab::encode(bench.core.sample(millis()),sequence++,packet); telemetry.writeValue(packet,20);
  uint8_t info[12]; rehab::metadata(bench.core,info); calibrationInfo.writeValue(info,12);
  BLE.advertise();
}
void loop() {
  if(bleReady) {
    BLE.poll();
    bool connected=BLE.connected();
    if(wasConnected && !connected) { bench.core.stop(); BLE.advertise(); }
    wasConnected=connected;
  }
  bench.poll();
  uint32_t now=millis();
  if(uint32_t(now-lastReport)>=100) {
    lastReport=now;
    if(bleReady) {
      uint8_t packet[20]; rehab::encode(bench.core.sample(now),sequence++,packet);
      telemetry.writeValue(packet,20);
      uint8_t info[12]; rehab::metadata(bench.core,info); calibrationInfo.writeValue(info,12);
    }
    bench.printSample();
  }
}
