"""Generate a labelled nominal packaging drawing from layout.json (Pillow)."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
BASE=Path(__file__).resolve().parent


def main():
    d=json.loads((BASE/'layout.json').read_text())
    img=Image.new('RGB',(1600,1000),'#f2f5f9'); draw=ImageDraw.Draw(img)
    def font(n):
        for name in ['C:/Windows/Fonts/segoeui.ttf','DejaVuSans.ttf']:
            try: return ImageFont.truetype(name,n)
            except OSError: pass
        return ImageFont.load_default()
    draw.text((35,24),'2.1-R2 | Common-core packaging drawing',fill='#18354f',font=font(34))
    draw.text((35,78),'Nominal millimetres. Components are envelopes; actual assembly has not been measured.',fill='#526476',font=font(22))
    colors=['#e8b54c','#469bb2','#75a878','#ab7dac','#b5bcc6']
    for panel,title in enumerate(['Battery layer | z = 4–13','Board layer | z = 16–28','Load cell and plate | top view']):
        left=30+panel*525; cx=left+245; cy=395; scale=3.25
        draw.rounded_rectangle((left,145,left+505,805),radius=16,fill='white',outline='#d5dfe8',width=2)
        draw.text((left+18,165),title,fill='#18354f',font=font(22))
        def rect(x,y,w,h,color,outline='#18354f'):
            draw.rectangle((cx+x*scale,cy+y*scale,cx+(x+w)*scale,cy+(y+h)*scale),fill=color,outline=outline,width=2)
        if panel<2:
            rect(-53,-30,106,60,'#e3e9ee'); rect(-50,-27,100,54,'#ffffff')
            selected=[(i,c) for i,c in enumerate(d['components']) if (i==0)==(panel==0)]
            for i,c in selected:
                rect(c['min'][0],c['min'][1],c['size'][0],c['size'][1],colors[i])
                draw.text((cx+(c['min'][0]+2)*scale,cy+(c['min'][1]+2)*scale),str(i+1),fill='#152f40',font=font(20))
            draw.text((left+20,540),'Bay: 100 × 54 mm; outside: 106 × 60 mm',fill='#526476',font=font(19))
            for row,(i,c) in enumerate(selected):
                draw.text((left+20,590+row*40),f"{i+1}. {c['part']}",fill='#18354f',font=font(18))
        else:
            rect(-66,-40,132,80,'#d1dbe4')
            c=d['cell'];rect(c['min'][0],c['min'][1],80,12.7,'#719fbb')
            for group,color in [('active_holes_x','#bb4539'),('fixed_holes_x','#245b31')]:
                for x in c[group]:
                    px=cx+x*scale;draw.ellipse((px-6,cy-6,px+6,cy+6),fill=color)
            for x in [-60,60]:
                for y in [-33,33]:
                    px=cx+x*scale;py=cy+y*scale;draw.ellipse((px-5,py-5,px+5,py+5),fill='#ffffff',outline='#18354f',width=2)
            draw.text((left+20,560),'Plate: 132 × 80 mm; thickness 4 mm',fill='#526476',font=font(19))
            draw.text((left+20,605),'Red: active M4, x = −35 / −20',fill='#bb4539',font=font(19))
            draw.text((left+20,644),'Green: fixed M5, x = 20 / 35',fill='#245b31',font=font(19))
            draw.text((left+20,683),'White: shell fasteners at ±60, ±33',fill='#18354f',font=font(19))
    draw.text((35,846),'Force path: upper shell → upper plate → active spacer → cell → fixed spacer → base → lower shell',fill='#18354f',font=font(24))
    draw.text((35,899),'Bench prototype only. Oversized shells, fasteners, stops, cable routing and ergonomics need physical review.',fill='#9a3412',font=font(23))
    img.save(BASE/'packaging.png');print(BASE/'packaging.png')


if __name__=='__main__':main()
