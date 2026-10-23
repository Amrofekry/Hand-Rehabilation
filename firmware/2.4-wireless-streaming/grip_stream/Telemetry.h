#pragma once
#include <RehabCore.h>
namespace rehab {
inline void encode(const Sample& s,uint32_t sequence,uint8_t out[20]) {
  out[0]=1; out[1]=s.flags; out[2]=s.shape; out[3]=0;
  put32(out+4,sequence); put32(out+8,s.millis);
  put32(out+12,uint32_t(s.raw)); put32(out+16,uint32_t(s.milliNewtons));
}
inline void metadata(const Core& core,uint8_t out[12]) {
  put32(out,core.calibrated()?core.calibration.revision:0);
  put32(out+4,core.calibrated()?core.calibration.crc:0);
  out[8]=core.shape; out[9]=1; out[10]=out[11]=0;
}
}
