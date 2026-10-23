#include <RehabCore.h>
#include <assert.h>
#include <iostream>
using namespace rehab;
void calibrate(Core& c,int32_t delta=10000) {
  c.selectShape(1); assert(c.beginTare(0));
  for(int i=0;i<20;++i)c.ingest(1000,uint32_t(i*100));
  assert(c.result==Result::TARED); assert(!c.calibrated());
  assert(c.beginScale(10.0f,2000));
  for(int i=0;i<20;++i)c.ingest(1000+delta,2000+uint32_t(i*100));
  assert(c.calibrated()); assert(c.result==Result::CALIBRATED_OK);
}
int main() {
  Core c; assert(!c.start(0)); assert(c.sample(0).flags&SENSOR_TIMEOUT);
  assert(!c.beginTare(0)); calibrate(c);
  assert(c.start(3900)); assert(c.sample(3900).milliNewtons==10000);
  c.ingest(6000,4000); assert(c.sample(4000).milliNewtons==5000);
  assert(!(c.sample(4500).flags&VALID)); assert(c.sample(4500).flags&SENSOR_TIMEOUT);
  c.ingest(8388607,4600); assert(c.sample(4600).flags&ADC_SATURATED);
  c.ingest(2000000,4700); assert(c.sample(4700).flags&OVER_RANGE);
  c.ingest(1000,4800); assert(c.sample(4800).flags&VALID); assert(c.sample(4800).milliNewtons==0);
  c.stop(); assert(!(c.sample(4800).flags&VALID));
  Calibration saved=c.calibration; Core restored; assert(restored.restore(saved));
  assert(!restored.running); assert(!restored.start(0));
  saved.offset++; assert(!restored.restore(saved)); assert(!restored.calibrated());
  saved=c.calibration; saved.version=2; saved.crc=recordCRC(saved); assert(!calibrationValid(saved));
  saved=c.calibration; saved.countsPerNewton=NAN; saved.crc=recordCRC(saved); assert(!calibrationValid(saved));
  c.selectShape(2); assert(!c.calibrated()); assert(!c.start(4800));
  Core reverse; calibrate(reverse,-10000); assert(reverse.start(3900)); assert(reverse.sample(3900).milliNewtons==10000);
  reverse.ingest(6000,4000); assert(reverse.sample(4000).milliNewtons==-5000);
  reverse.stop(); assert(reverse.beginTare(4100)); assert(!reverse.calibrated());
  reverse.tick(9100); assert(reverse.result==Result::TIMEOUT); assert(!reverse.tared);
  Core unstable; unstable.selectShape(1); assert(unstable.beginTare(0));
  for(int i=0;i<20;++i)unstable.ingest(i%2?0:1000,i*100);
  assert(unstable.result==Result::UNSTABLE); assert(!unstable.tared);
  assert(!unstable.beginScale(10,2000));
  Core wrap; calibrate(wrap); wrap.ingest(6000,0xfffffff0u); assert(wrap.start(0xfffffff0u));
  assert(wrap.sample(0x20u).flags&VALID); assert(wrap.sample(0x300u).flags&SENSOR_TIMEOUT);
  Core saturated; saturated.selectShape(1); assert(saturated.beginTare(0)); saturated.ingest(-8388608,1);
  assert(saturated.result==Result::SATURATED);
  Core low; low.selectShape(1); assert(low.beginTare(0)); for(int i=0;i<20;++i)low.ingest(1000,i*100);
  assert(!low.beginScale(NAN,2000)); assert(!low.beginScale(0,2000)); assert(!low.beginScale(1000,2000));
  assert(low.beginScale(10,2000)); for(int i=0;i<20;++i)low.ingest(1010,2000+i*100);
  assert(!low.calibrated()); assert(low.result==Result::BAD_REFERENCE);
  std::cout << "PASS calibration: conversion, reverse polarity, tare, corruption, faults, shape binding, rollover, timeout, unstable/reference rejection\n";
}
