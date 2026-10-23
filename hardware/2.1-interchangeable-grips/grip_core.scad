// WBS 2.1, configurable fit prototype, millimetres. NOT a load-rated release.
// Render examples: openscad -o sphere_top.stl -D 'part="sphere_top"' grip_core.scad
part = "assembly";
$fn = 64;
core_l = 86;
core_w = 32;
core_h = 24;
wall = 3;
clearance = 0.6;
travel = 2;
spring_od = 6;
spring_depth = 3;
// These are provisional envelopes, not dimensions of an identified load cell.
cell_l = 80;
cell_w = 13;
cell_h = 13;
assert(core_l > cell_l && core_w > cell_w && wall >= 2);
assert(clearance > 0 && travel > 0);

module rounded_box(size, radius=2) {
    hull() for(x=[-1,1],y=[-1,1],z=[-1,1])
        translate([x*(size[0]/2-radius),y*(size[1]/2-radius),z*(size[2]/2-radius)]) sphere(r=radius);
}
module screw_holes(zheight) {
    for(x=[-core_l/2+7,core_l/2-7],y=[-core_w/2+5,core_w/2-5])
        translate([x,y,0]) cylinder(d=3.4,h=zheight,center=true);
}
module tray() {
    difference() {
        rounded_box([core_l,core_w,core_h]);
        translate([0,0,wall]) rounded_box([core_l-2*wall,core_w-2*wall,core_h]);
        translate([-core_l/2,0,4]) cube([12,14,10],center=true); // USB/service access
        screw_holes(80);
    }
    // Lid attachment bosses, through holes accept M3 hardware.
    difference() {
        for(x=[-core_l/2+7,core_l/2-7],y=[-core_w/2+5,core_w/2-5])
            translate([x,y,-core_h/2+wall]) cylinder(d=7,h=core_h-wall);
        screw_holes(80);
    }
}
module lid() {
    difference() {
        rounded_box([core_l,core_w,wall],1);
        screw_holes(10);
        for(x=[-20:5:20]) translate([x,0,0]) cube([2,12,10],center=true);
    }
}
module outer_shape(shape) {
    if(shape=="sphere") sphere(r=55);
    if(shape=="cylinder") rotate([0,90,0]) cylinder(r=31,h=108,center=true);
    // Pencil-like hexagonal training grip; not an everyday-pencil diameter.
    if(shape=="pencil") rotate([0,90,0]) cylinder(r=31,h=124,center=true,$fn=6);
}
module spring_pockets() {
    for(x=[-30,30],y=[-21,21]) translate([x,y,0])
        cylinder(d=spring_od+clearance,h=2*spring_depth,center=true);
}
module shell(shape,top=true) {
    difference() {
        intersection() {
            outer_shape(shape);
            translate([0,0,top?75+travel/2:-75-travel/2]) cube([200,200,150],center=true);
        }
        rounded_box([core_l+2*clearance,core_w+2*clearance,core_h+2*clearance],2);
        spring_pockets();
        // Through guides outside the core. Guide rods require clearance and retainers.
        for(x=[-36,36],y=[-23,23]) translate([x,y,0]) cylinder(d=3.6,h=150,center=true);
        // Shared sensor bay on bottom; replace with measured adapter before a load test.
        translate([0,0,-core_h/2-cell_h/2]) cube([cell_l+clearance,cell_w+clearance,cell_h],center=true);
    }
}
module platen() {
    difference() {
        rounded_box([core_l-10,core_w-8,4],1);
        for(x=[-25,25]) translate([x,0,0]) hull() {
            translate([-4,0,0]) cylinder(d=4.4,h=12,center=true);
            translate([4,0,0]) cylinder(d=4.4,h=12,center=true);
        }
    }
}
if(part=="tray") tray();
else if(part=="lid") lid();
else if(part=="platen") platen();
else if(part=="sphere_top") shell("sphere",true);
else if(part=="sphere_bottom") shell("sphere",false);
else if(part=="cylinder_top") shell("cylinder",true);
else if(part=="cylinder_bottom") shell("cylinder",false);
else if(part=="pencil_top") shell("pencil",true);
else if(part=="pencil_bottom") shell("pencil",false);
else if(part=="assembly") {
    color("lightblue",0.35) shell("cylinder",false);
    color("grey") tray();
    translate([0,0,20]) color("silver") lid();
    translate([0,0,55]) color("lightblue",0.5) shell("cylinder",true);
} else assert(false,"Unknown part");
