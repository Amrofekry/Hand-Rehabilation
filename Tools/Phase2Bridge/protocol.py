"""Strict WBS 2.4 wire decoder and connection-scoped continuity tracking."""
import struct
import math
SERVICE='8c180000-5f55-4d3c-8ac7-7d25a3818f42'
TELEMETRY='8c180001-5f55-4d3c-8ac7-7d25a3818f42'
METADATA='8c180002-5f55-4d3c-8ac7-7d25a3818f42'
VALID=1

def decode(data):
    if len(data)!=20: raise ValueError('Expected exactly 20 bytes')
    version,flags,shape,reserved,seq,ms,raw,mn=struct.unpack('<BBBBIIii',data)
    if version!=1 or reserved or flags&0x80 or shape>3: raise ValueError('Unsupported version/reserved/shape')
    if not -8388608<=raw<=8388607: raise ValueError('ADC count outside signed 24-bit range')
    if flags&2 and shape==0: raise ValueError('Calibration requires a known shape')
    if flags&VALID and (flags!=3 or shape==0 or raw in (-8388608,8388607) or abs(mn)>980665):
        raise ValueError('Inconsistent valid force flags/value')
    if not flags&VALID and mn!=0: raise ValueError('Invalid force must have zero wire value')
    return dict(sequence=seq,device_ms=ms,raw_counts=raw,force_n=mn/1000 if flags&VALID else None,flags=flags,shape_id=shape)

def decode_metadata(data):
    if len(data)!=12: raise ValueError('Expected 12-byte metadata')
    revision,crc,shape,version,reserved=struct.unpack('<IIBBH',data)
    if version!=1 or reserved or shape>3 or (revision and not shape) or (revision==0 and crc!=0): raise ValueError('Invalid metadata')
    return dict(calibration_id=revision,calibration_crc=crc,shape_id=shape)

class Tracker:
    def __init__(self):
        self.last_seq=None; self.last_ms=None; self.last_receive=None
        self.last_sample_receive=None; self.last_values=None
    def accept(self,packet,received):
        if not math.isfinite(received): raise ValueError('Host timestamp must be finite')
        row=decode(packet); dropped=0
        if self.last_receive is not None and received<self.last_receive: raise ValueError('Host clock moved backwards')
        if self.last_seq is not None:
            delta=(row['sequence']-self.last_seq)&0xffffffff
            if delta==0: raise ValueError('Duplicate sequence')
            if delta>=0x80000000: raise ValueError('Backward sequence: reconnect/reset continuity')
            dropped=delta-1
        # Invalid heartbeat timestamps do not participate in sample-time continuity.
        new_sample=False
        if row['force_n'] is not None:
            if self.last_ms is None: new_sample=True
            else:
                advance=(row['device_ms']-self.last_ms)&0xffffffff
                if advance>=0x80000000: raise ValueError('Backward device clock: reset continuity')
                new_sample=advance!=0
                if not new_sample and self.last_values!=(row['raw_counts'],row['force_n'],row['shape_id']):
                    raise ValueError('One sample timestamp has conflicting values')
            if new_sample: self.last_sample_receive=received
            self.last_ms=row['device_ms']
            self.last_values=(row['raw_counts'],row['force_n'],row['shape_id'])
        self.last_seq=row['sequence']; self.last_receive=received
        sample_stale=row['force_n'] is not None and (self.last_sample_receive is None or received-self.last_sample_receive>=.5)
        if sample_stale: row['force_n']=None
        return dict(row,dropped_packets=dropped,new_sample=new_sample,sample_stale=sample_stale)
    def stale(self,now): return self.last_receive is None or now-self.last_receive>=.5
