#pragma once
#include <stdint.h>
namespace rehab {
// Fixed channel A / gain 128. No readiness wait can occur after the first check.
// IO owns GPIO/timing and interrupt-state preservation; testable without Arduino.
template<class IO> bool readHX711Ready(IO& io,int32_t& result) {
  if(io.dataHigh()) return false;
  auto state=io.lock();
  if(io.dataHigh()) { io.unlock(state); return false; }
  uint32_t bits=0;
  for(unsigned i=0;i<24;++i) {
    io.clock(true); io.pause();
    bits=(bits<<1)|(io.dataHigh()?1u:0u);
    io.clock(false); io.pause();
  }
  io.clock(true); io.pause(); // 25th pulse selects A/128 for next conversion.
  io.clock(false); io.pause();
  io.unlock(state);
  result=(bits&0x800000u)?int32_t(bits)-16777216:int32_t(bits);
  return true;
}
}
