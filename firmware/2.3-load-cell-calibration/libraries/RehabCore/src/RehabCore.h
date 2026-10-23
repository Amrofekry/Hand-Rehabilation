#pragma once
#include <stdint.h>
#include <math.h>
#include <string.h>

namespace rehab {
constexpr float CAPACITY_N = 980.665f;
constexpr uint32_t STALE_MS = 500;
enum Flag : uint8_t { VALID=1, CALIBRATED=2, SENSOR_TIMEOUT=4,
  ADC_SATURATED=8, OVER_RANGE=16, CALIBRATING=32, STREAM_STOPPED=64 };
inline bool shapeValid(uint8_t s) { return s>=1 && s<=3; }
inline bool rawValid(int32_t raw) { return raw>-8388608 && raw<8388607; }
inline void put32(uint8_t* p,uint32_t n) {
  for(uint8_t i=0;i<4;++i) p[i]=uint8_t(n>>(8*i));
}
inline uint32_t crc32(const uint8_t* data,unsigned count) {
  uint32_t c=0xffffffffu;
  for(unsigned i=0;i<count;++i) {
    c^=data[i];
    for(unsigned j=0;j<8;++j) c=(c>>1)^(0xedb88320u & (0u-(c&1u)));
  }
  return ~c;
}
struct Calibration {
  uint32_t magic;
  uint16_t version;
  uint8_t shape;
  uint8_t reserved;
  uint32_t revision;
  int32_t offset;
  float countsPerNewton;
  float maxForce;
  uint32_t crc;
};
static_assert(sizeof(float)==4,"Requires binary32 float");
static_assert(sizeof(Calibration)==28,"Unexpected record layout");
inline uint32_t recordCRC(const Calibration& c) {
  uint8_t b[24]={}; uint32_t f;
  put32(b,c.magic); b[4]=uint8_t(c.version); b[5]=uint8_t(c.version>>8);
  b[6]=c.shape; b[7]=c.reserved; put32(b+8,c.revision); put32(b+12,uint32_t(c.offset));
  memcpy(&f,&c.countsPerNewton,4); put32(b+16,f);
  memcpy(&f,&c.maxForce,4); put32(b+20,f);
  return crc32(b,sizeof b);
}
inline bool calibrationValid(const Calibration& c) {
  return c.magic==0x47525031u && c.version==1 && c.reserved==0 &&
    shapeValid(c.shape) && c.revision>0 && rawValid(c.offset) &&
    isfinite(c.countsPerNewton) && fabsf(c.countsPerNewton)>=0.001f &&
    fabsf(c.countsPerNewton)<=1e9f && isfinite(c.maxForce) &&
    c.maxForce>0 && c.maxForce<=CAPACITY_N && c.crc==recordCRC(c);
}
struct Sample { uint32_t millis; int32_t raw; int32_t milliNewtons; uint8_t flags; uint8_t shape; };
enum class Collection { NONE, TARE, SCALE };
enum class Result { NONE, TARED, CALIBRATED_OK, UNSTABLE, TIMEOUT, BAD_REFERENCE, NEED_TARE, SATURATED };

class Core {
 public:
  Calibration calibration{};
  uint8_t shape=0;
  bool running=false;
  bool hasReading=false;
  bool tared=false;
  int32_t raw=0;
  uint32_t sampleMs=0;
  Collection collection=Collection::NONE;
  Result result=Result::NONE;

  bool calibrated() const { return calibrationValid(calibration) && calibration.shape==shape; }
  bool restore(const Calibration& c) {
    running=false; tared=false; hasReading=false; collection=Collection::NONE;
    if(!calibrationValid(c)) { calibration={}; shape=0; return false; }
    calibration=c; shape=c.shape; return true;
  }
  void selectShape(uint8_t s) {
    if(!shapeValid(s)) return;
    running=false; collection=Collection::NONE; result=Result::NONE;
    if(shape!=s) { shape=s; invalidate(); tared=false; }
  }
  void stop() { running=false; }
  bool start(uint32_t now) {
    if(!calibrated() || collection!=Collection::NONE || !hasReading ||
       uint32_t(now-sampleMs)>=STALE_MS || !rawValid(raw)) return false;
    float force=(float(raw)-float(calibration.offset))/calibration.countsPerNewton;
    if(!isfinite(force) || fabsf(force)>calibration.maxForce) return false;
    running=true; return true;
  }
  bool beginTare(uint32_t now) {
    if(!shapeValid(shape) || collection!=Collection::NONE) return false;
    running=false; invalidate(); tared=false; begin(Collection::TARE,now); return true;
  }
  bool beginScale(float newtons,uint32_t now) {
    if(collection!=Collection::NONE) return false;
    running=false;
    if(!tared) { result=Result::NEED_TARE; return false; }
    if(!isfinite(newtons) || newtons<=0 || newtons>CAPACITY_N) { result=Result::BAD_REFERENCE; return false; }
    reference=newtons; invalidate(); begin(Collection::SCALE,now); return true;
  }
  void tick(uint32_t now) {
    if(collection!=Collection::NONE && uint32_t(now-started)>=5000u) {
      collection=Collection::NONE; result=Result::TIMEOUT; tared=false; invalidate();
    }
  }
  void ingest(int32_t value,uint32_t now) {
    tick(now); raw=value; sampleMs=now; hasReading=true;
    if(collection==Collection::NONE) return;
    if(!rawValid(value)) { collection=Collection::NONE; result=Result::SATURATED; tared=false; invalidate(); return; }
    sum+=value; if(value<minimum) minimum=value; if(value>maximum) maximum=value;
    if(++count<20) return;
    float mean=float(double(sum)/count);
    float limit=collection==Collection::TARE?100.0f:fmaxf(100.0f,fabsf(mean-float(calibration.offset))*0.02f);
    if(float(int64_t(maximum)-minimum)>limit) {
      collection=Collection::NONE; result=Result::UNSTABLE; tared=false; invalidate(); return;
    }
    if(collection==Collection::TARE) {
      calibration.offset=int32_t(lroundf(mean)); tared=true; result=Result::TARED;
    } else {
      float delta=mean-float(calibration.offset);
      if(fabsf(delta)<100.0f) { result=Result::BAD_REFERENCE; invalidate(); }
      else {
        calibration.magic=0x47525031u; calibration.version=1; calibration.reserved=0;
        calibration.shape=shape; calibration.countsPerNewton=delta/reference;
        calibration.maxForce=CAPACITY_N;
        ++calibration.revision; if(calibration.revision==0) calibration.revision=1;
        calibration.crc=recordCRC(calibration);
        result=calibrated()?Result::CALIBRATED_OK:Result::BAD_REFERENCE;
        tared=false;
      }
    }
    collection=Collection::NONE;
  }
  Sample sample(uint32_t now) const {
    Sample s{sampleMs,hasReading?raw:0,0,0,shape};
    if(calibrated()) s.flags|=CALIBRATED;
    if(!running) s.flags|=STREAM_STOPPED;
    if(collection!=Collection::NONE) s.flags|=CALIBRATING;
    if(!hasReading || uint32_t(now-sampleMs)>=STALE_MS) s.flags|=SENSOR_TIMEOUT;
    if(hasReading && !rawValid(raw)) s.flags|=ADC_SATURATED;
    if(calibrated() && hasReading && rawValid(raw)) {
      float force=(float(raw)-float(calibration.offset))/calibration.countsPerNewton;
      if(!isfinite(force) || fabsf(force)>calibration.maxForce) s.flags|=OVER_RANGE;
      else if(s.flags==CALIBRATED) { s.flags|=VALID; s.milliNewtons=int32_t(lroundf(force*1000.0f)); }
    }
    if(!(s.flags&VALID)) s.millis=now;
    return s;
  }
 private:
  int64_t sum=0;
  uint16_t count=0;
  int32_t minimum=8388607,maximum=-8388608;
  uint32_t started=0;
  float reference=0;
  void invalidate() { calibration.magic=0; calibration.crc=0; }
  void begin(Collection mode,uint32_t now) {
    collection=mode; result=Result::NONE; sum=0; count=0;
    minimum=8388607; maximum=-8388608; started=now;
  }
};
} // namespace rehab
