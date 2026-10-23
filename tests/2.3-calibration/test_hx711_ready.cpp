#include <HX711Ready.h>
#include <assert.h>
#include <iostream>
struct FakeIO {
  uint32_t bits=0; unsigned clocks=0; unsigned checks=0; unsigned pauses=0;
  bool ready=true,vanish=false,locked=false,high=false;
  bool dataHigh() {
    if(clocks==0) { ++checks; return !ready || (vanish && checks==2); }
    assert(locked && high && clocks<=24);
    return (bits>>(24-clocks))&1u;
  }
  unsigned lock() { assert(!locked); locked=true; return 7; }
  void unlock(unsigned state) { assert(locked && state==7); locked=false; }
  void clock(bool value) { assert(locked); high=value; if(value)++clocks; }
  void pause() { ++pauses; }
};
int main() {
  int32_t value=123;
  FakeIO absent; absent.ready=false; assert(!rehab::readHX711Ready(absent,value)); assert(value==123 && absent.clocks==0);
  FakeIO race; race.vanish=true; assert(!rehab::readHX711Ready(race,value)); assert(!race.locked && race.clocks==0);
  for(uint32_t bits: {0u,0x7fffffu,0x800000u,0xffffffu,0xffcfc7u}) {
    FakeIO io; io.bits=bits; assert(rehab::readHX711Ready(io,value));
    int32_t expected=(bits&0x800000u)?int32_t(bits)-16777216:int32_t(bits);
    assert(value==expected); assert(io.clocks==25 && io.pauses==50 && !io.locked && !io.high);
  }
  std::cout<<"PASS HX711: absent/vanishing data ready, fixed 25 pulses, sign extension, restored interrupt state\n";
}
