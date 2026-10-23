#include "Telemetry.h"
#include <assert.h>
#include <iostream>
int main() {
  rehab::Sample s{1000,-12345,12345,3,1}; uint8_t out[20]; rehab::encode(s,42,out);
  const uint8_t expected[20]={1,3,1,0,42,0,0,0,232,3,0,0,199,207,255,255,57,48,0,0};
  for(int i=0;i<20;++i) assert(out[i]==expected[i]);
  rehab::Core core; uint8_t info[12]; rehab::metadata(core,info);
  for(int i=0;i<12;++i) assert(info[i]==(i==9?1:0));
  std::cout<<"PASS telemetry: independent little-endian golden vector and invalid-calibration metadata\n";
}
