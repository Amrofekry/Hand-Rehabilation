#pragma once
#include <Arduino.h>
#include <FlashStorage.h>
#include <stdlib.h>
#include "RehabCore.h"
#include "HX711Ready.h"

struct NanoHX711IO {
  bool dataHigh() { return digitalRead(2)==HIGH; }
  void clock(bool high) { digitalWrite(3,high?HIGH:LOW); }
  void pause() { delayMicroseconds(1); }
  uint32_t lock() { uint32_t state=__get_PRIMASK(); __disable_irq(); return state; }
  void unlock(uint32_t state) { __set_PRIMASK(state); }
};

// This header is used in one translation unit per sketch.
FlashStorage(gripCalibration, rehab::Calibration);
class BenchConsole {
 public:
  rehab::Core core;
  NanoHX711IO adc;
  void begin() {
    Serial.begin(115200); // Never wait for USB: headless firmware must boot.
    pinMode(3,OUTPUT); digitalWrite(3,LOW);
    pinMode(2,INPUT_PULLUP); // Detached DOUT defaults to not-ready at MCU logic voltage.
    core.restore(gripCalibration.read());
  }
  void poll() {
    uint32_t now=millis(); core.tick(now);
    int32_t reading;
    if(rehab::readHX711Ready(adc,reading)) core.ingest(reading,millis());
    // Bound command processing so a noisy USB host cannot starve acquisition/BLE.
    for(unsigned n=0;n<64 && Serial.available();++n) {
      char ch=char(Serial.read());
      if(ch=='\r') continue;
      if(ch=='\n') {
        line[length]=0;
        if(overflow) Serial.println("ERR command too long");
        else command(line);
        length=0; overflow=false;
      } else if(length<sizeof(line)-1 && !overflow) line[length++]=ch;
      else overflow=true;
    }
    if(core.result!=rehab::Result::NONE) {
      Serial.print("RESULT "); Serial.println(int(core.result));
      core.result=rehab::Result::NONE;
    }
  }
  void status() {
    Serial.print("STATUS shape="); Serial.print(core.shape);
    Serial.print(" calibrated="); Serial.print(core.calibrated());
    Serial.print(" revision="); Serial.print(core.calibration.revision);
    Serial.print(" offset="); Serial.print(core.calibration.offset);
    Serial.print(" counts_per_N="); Serial.print(core.calibration.countsPerNewton,6);
    Serial.print(" crc="); Serial.print(core.calibration.crc,HEX);
    Serial.print(" running="); Serial.println(core.running);
  }
  void printSample() {
    if(!Serial || Serial.availableForWrite()<64) return;
    auto s=core.sample(millis());
    Serial.print("DATA,"); Serial.print(s.millis); Serial.print(',');
    Serial.print(s.raw); Serial.print(',');
    if(s.flags&rehab::VALID) Serial.print(float(s.milliNewtons)/1000.0f,3);
    else Serial.print("NA");
    Serial.print(','); Serial.print(s.flags); Serial.print(','); Serial.println(s.shape);
  }
 private:
  char line[80]{}; uint8_t length=0; bool overflow=false;
  void command(const char* cmd) {
    if(!strcmp(cmd,"help")) Serial.println("shape 1|2|3; tare; cal NEWTONS; save; start; stop; status");
    else if(!strcmp(cmd,"status")) status();
    else if(!strcmp(cmd,"stop")) { core.stop(); Serial.println("OK stopped"); }
    else if(!strcmp(cmd,"start")) Serial.println(core.start(millis())?"OK started":"ERR need fresh calibrated sample");
    else if(!strcmp(cmd,"tare")) Serial.println(core.beginTare(millis())?"OK collecting tare":"ERR select shape / collection busy");
    else if(!strncmp(cmd,"shape ",6)) {
      char* end=nullptr; long shape=strtol(cmd+6,&end,10);
      if(end==cmd+6 || *end || shape<1 || shape>3) Serial.println("ERR shape 1..3");
      else { core.selectShape(uint8_t(shape)); Serial.println("OK shape; recalibrate if changed"); }
    } else if(!strncmp(cmd,"cal ",4)) {
      char* end=nullptr; float n=strtof(cmd+4,&end);
      if(end==cmd+4 || *end) Serial.println("ERR cal NEWTONS");
      else Serial.println(core.beginScale(n,millis())?"OK collecting known load":"ERR reference / tare / busy");
    } else if(!strcmp(cmd,"save")) {
      if(core.running || core.collection!=rehab::Collection::NONE || !core.calibrated()) {
        Serial.println("ERR stop and calibrate before save"); return;
      }
      auto old=gripCalibration.read();
      if(!rehab::calibrationValid(old) || old.crc!=core.calibration.crc) gripCalibration.write(core.calibration);
      auto check=gripCalibration.read();
      if(rehab::calibrationValid(check) && check.crc==core.calibration.crc) Serial.println("OK saved and verified");
      else { core.restore(rehab::Calibration{}); Serial.println("ERR flash readback; calibration invalidated"); }
    } else if(*cmd) Serial.println("ERR unknown command; help");
  }
};
