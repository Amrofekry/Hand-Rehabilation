#include <BenchConsole.h>
BenchConsole bench;
uint32_t lastReport=0;
void setup() { bench.begin(); }
void loop() {
  bench.poll();
  uint32_t now=millis();
  if(uint32_t(now-lastReport)>=100) { lastReport=now; bench.printSample(); }
}
