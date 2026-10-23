// WBS 2.1-R2: dimensioned bench prototype. Metal parts are not printable substitutes.
include <parameters.scad>
part="assembly";
flat=false;
$fn=40;
module box_at(p,s) { translate(p) cube(s); }
module zhole(x,y,d,z,h) { translate([x,y,z]) cylinder(d=d,h=h); }
module guides(d,z,h) { for(x=guide_x,y=guide_y) zhole(x,y,d,z,h); }
module rounded_rect(l,w,h,r=2) {
    linear_extrude(h) hull() for(x=[-l/2+r,l/2-r],y=[-w/2+r,w/2-r]) translate([x,y]) circle(r=r);
}
module plate(top=false) {
    z=top?top_z:base_z;
    difference() {
        translate([0,0,z]) rounded_rect(base_size[0],base_size[1],base_size[2]);
        guides(top?guide_clear:4.2,z-1,6);
        for(x=top?active_x:fixed_x) zhole(x,0,top?4.5:5.5,z-1,6);
        // Shape attachment pattern, separate from the sensor fasteners and guides.
        for(x=[-60,60],y=[-33,33]) zhole(x,y,4.5,z-1,6);
        if(!top) translate([42,-6,z-1]) cube([5,12,6]); // isolated wire exit
    }
}
module spacer(active=false) {
    p=active?active_min:fixed_min; s=active?active_size:fixed_size;
    difference() {
        box_at(p,s);
        for(x=active?active_x:fixed_x) zhole(x,0,active?4.5:5.5,p[2]-1,6);
    }
}
module tray() {
    difference() {
        rounded_rect(106,60,30);
        translate([0,0,3]) rounded_rect(100,54,29,1);
        // Both ends are service openings, verify plug and strain-relief fit.
        translate([-56,-25,14]) cube([12,24,15]);
        translate([44,-25,14]) cube([12,24,15]);
        guides(7,-1,33);
    }
    // Deck supports do not touch the battery body.
    for(x=[-48,48],y=[-20,20]) translate([x,y,3]) cube([2,4,11],center=false);
}
module deck() {
    difference() {
        translate([0,0,14]) rounded_rect(98,52,1.5,1);
        for(x=[-40,-20,0,20,40],y=[-22,22]) translate([x-4,y-1,13]) cube([8,2,4]);
        guides(7,12,5);
    }
}
module stop_sleeve() {
    difference() {
        cylinder(d=8,h=top_z-(base_z+4)-stop_gap);
        translate([0,0,-1]) cylinder(d=4.6,h=30);
    }
}
module outer(shape) {
    translate([0,0,30]) {
        if(shape=="sphere") sphere(d=sphere_d);
        if(shape=="cylinder") rotate([0,90,0]) cylinder(d=cylinder_d,h=cylinder_l,center=true);
        if(shape=="pencil") rotate([0,90,0]) cylinder(d=pencil_d,h=pencil_l,center=true,$fn=6);
    }
}
module shell(shape,top=false) {
    difference() {
        intersection() {
            outer(shape);
            translate([-100,-100,top?44.6:-100]) cube([200,200,top?120:144]);
        }
        // Independent upper/lower shell: no shell contact across the sensing travel.
        translate([0,0,-2]) rounded_rect(134,82,60.7,2);
        for(x=[-60,60],y=[-33,33]) zhole(x,y,4.5,-100,240);
        // USB/service openings with generous cable clearance.
        for(x=[-110,45]) translate([x,-27,12]) cube([65,28,19]);
    }
    // Attachment lands meet only their own metal plate; through-bolts clamp each shell separately.
    for(x=[-60,60],y=[-33,33]) difference() {
        translate([x,y,top?58.7:-5]) cylinder(d=12,h=top?7.3:35);
        zhole(x,y,4.5,top?57:-6,top?12:38);
    }
}
module cell_mock() {
    difference() {
        box_at(cell_min,cell_size);
        for(x=active_x) zhole(x,0,4,37,15);
        for(x=fixed_x) zhole(x,0,5,37,15);
        // Visual sensing section only: purchase the real sensor, never fabricate this mock.
        translate([0,-7,44.35]) rotate([-90,0,0]) cylinder(d=8,h=14);
    }
}
module chosen() {
    if(part=="tray") tray();
    else if(part=="deck") deck();
    else if(part=="base_plate") plate(false);
    else if(part=="top_plate") plate(true);
    else if(part=="fixed_spacer") spacer(false);
    else if(part=="active_spacer") spacer(true);
    else if(part=="stop_sleeve") stop_sleeve();
    else if(part=="sphere_bottom") shell("sphere",false);
    else if(part=="sphere_top") shell("sphere",true);
    else if(part=="cylinder_bottom") shell("cylinder",false);
    else if(part=="cylinder_top") shell("cylinder",true);
    else if(part=="pencil_bottom") shell("pencil",false);
    else if(part=="pencil_top") shell("pencil",true);
    else if(part=="assembly") {
        color("slategray") tray(); color("gray") deck();
        for(i=[0:len(component_boxes)-1]) color(i==0?"gold":i==1?"teal":"green") box_at(component_boxes[i][0],component_boxes[i][1]);
        color("silver") plate(false); color("silver") spacer(false);
        color("blue") cell_mock(); color("silver") spacer(true);
        translate([0,0,12]) color("silver") plate(true);
        for(x=guide_x,y=guide_y) color("gray") translate([x,y,34]) stop_sleeve();
    } else assert(false,"Unknown part");
}
if(flat) projection(cut=false) chosen(); else chosen();
