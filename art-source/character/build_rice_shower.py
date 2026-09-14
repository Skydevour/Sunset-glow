"""Editable reference-inspired character. Blender 5.2 background entry point.
Coordinate system: metres, Z up, facing -Y. No external asset dependencies.
Back bow, stockings and shoes are authored completions absent from references.
"""
import bpy, math, os, json
from mathutils import Vector
from math import sin, cos, pi
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
OUT = os.path.join(ROOT, 'Sunset Glow/Assets/_Game/Art/Characters/RiceShower')
ART = os.path.join(ROOT, 'artifacts/character')
os.makedirs(OUT, exist_ok=True); os.makedirs(ART, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
mats={}
def material(name, color, rough=.6, metal=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=rough; p.inputs['Metallic'].default_value=metal
    mats[name]=m; return m
material('Skin',(0.87,.655,.56),.7)
material('Hair',(.006,.008,.015),.46)
material('BlackCloth',(.022,.025,.038),.84)
material('WhiteCloth',(.79,.80,.77),.83)
material('Eyes',(.17,.235,.47),.35)
material('IrisLight',(.35,.46,.70),.4)
material('IrisDark',(.045,.06,.16),.4)
material('EyeWhite',(.90,.875,.84),.42)
material('Lashes',(.009,.006,.018),.58)
material('Blush',(.48,.15,.18),.75)
parts={k:[] for k in ['Head','Hair','Body','Clothes','Accessories','HeadAccessories']}
def mesh(name,verts,faces,mat,group,bone='Head',weights=None):
    d=bpy.data.meshes.new(name); d.from_pydata(verts,[],faces); d.update()
    o=bpy.data.objects.new(name,d); bpy.context.collection.objects.link(o); o.data.materials.append(mats[mat])
    for p in d.polygons:p.use_smooth=True
    if weights is None:weights=[{bone:1} for v in verts]
    for i,w in enumerate(weights):
        for bn,value in w.items():
            vg=o.vertex_groups.get(bn) or o.vertex_groups.new(name=bn);vg.add([i],value,'REPLACE')
    parts[group].append(o);return o
def tube(name,points,radii,mat,group,bone='Head',sides=12,weights=None,flatten=1):
    vs=[];fs=[]
    for j,p in enumerate(points):
        p=Vector(p);t=Vector(points[min(j+1,len(points)-1)])-Vector(points[max(j-1,0)])
        t.normalize(); a=t.cross(Vector((0,1,0)))
        if a.length<.01:a=t.cross(Vector((1,0,0)))
        a.normalize();b=t.cross(a).normalized()
        for k in range(sides):vs.append(p+radii[j]*(a*cos(k*2*pi/sides)+b*sin(k*2*pi/sides)*flatten))
        if j:
            for k in range(sides):a0=(j-1)*sides+k; a1=(j-1)*sides+(k+1)%sides;fs.append((a0,a1,a1+sides,a0+sides))
    fs.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+k for k in range(sides))])
    ww=None if weights is None else [weights[j] for j in range(len(points)) for k in range(sides)]
    return mesh(name,vs,fs,mat,group,bone,ww)
def ellipse(name,center,scale,mat,group,bone='Head',rings=12,sides=24):
    vs=[];fs=[]
    for j in range(rings+1):
        t=pi*j/rings
        for k in range(sides):
            a=2*pi*k/sides;vs.append((center[0]+scale[0]*sin(t)*cos(a),center[1]+scale[1]*sin(t)*sin(a),center[2]+scale[2]*cos(t)))
        if j:
            for k in range(sides):a0=(j-1)*sides+k;a1=(j-1)*sides+(k+1)%sides;fs.append((a0,a1,a1+sides,a0+sides))
    return mesh(name,vs,fs,mat,group,bone)
def loft(name,levels,mat,group,bone='Chest',n=64,pleat=0,weights=None,start=0,end=2*pi):
    vs=[];fs=[]; ww=[]
    for j,(z,rx,ry) in enumerate(levels):
        for k in range(n+1):
            a=start+(end-start)*k/n; p=pleat*(.35+.65*j/max(1,len(levels)-1))*cos(a*18)
            vs.append(((rx+p)*sin(a),-(ry+p*.65)*cos(a),z))
            ww.append({bone:1} if weights is None else weights[j](a))
        if j:
            for k in range(n):a=(j-1)*(n+1)+k;fs.append((a,a+1,a+n+2,a+n+1))
    return mesh(name,vs,fs,mat,group,bone,ww)
def ribbon(name,points,widths,mat,group,bone='Head',ruffle=0,segments=8):
    vs=[];fs=[]
    for j,p in enumerate(points):
        for k in range(segments+1):
            q=k/segments*2-1
            vs.append((p[0]+q*widths[j],p[1]-.002*cos(q*pi),p[2]+ruffle*cos(j*pi)*q*q))
        if j:
            for k in range(segments):a=(j-1)*(segments+1)+k;fs.append((a,a+1,a+segments+2,a+segments+1))
    o=mesh(name,vs,fs,mat,group,bone); sol=o.modifiers.new('cloth thickness','SOLIDIFY');sol.thickness=.002
    return o
# A continuous tapered stylized head, with authored forehead, cheek, jaw and nose.
levels=[(1.296,.006,.019),(1.305,.031,.043),(1.325,.057,.061),(1.352,.078,.073),(1.385,.091,.079),(1.42,.095,.081),(1.456,.093,.081),(1.49,.085,.077),(1.518,.064,.061),(1.538,.031,.035),(1.543,.001,.001)]
vs=[];fs=[];n=64
for j,(z,rx,ry) in enumerate(levels):
    for k in range(n):
        a=2*pi*k/n;x=rx*sin(a);y=-ry*cos(a)
        # Front is flatter than the cranium; sculpted small nose and cheek planes.
        front=max(0,cos(a))**8
        nose=.011*math.exp(-((z-1.371)/.018)**2)*math.exp(-(x/.019)**2)
        y-=front*nose
        vs.append((x,y,z))
    if j:
        for k in range(n):a0=(j-1)*n+k;a1=(j-1)*n+(k+1)%n;fs.append((a0,a1,a1+n,a0+n))
face=mesh('SculptedFace',vs,fs,'Skin','Head')
sub=face.modifiers.new('Face surface refinement','SUBSURF');sub.levels=1
# Anatomical ears, neck and subtle lip line, not a stack of spheres.
for s in [-1,1]:
    ellipse('HumanEar',(s*.095,.004,1.379),(.012,.018,.029),'Skin','Head')
loft('Neck',[(1.234,.036,.032),(1.30,.038,.032),(1.32,.035,.03)],'Skin','Body','Neck',32)
tube('Smile',[(-.020,-.071,1.335),(-.010,-.076,1.332),(0,-.078,1.331),(.013,-.074,1.335)],[.0009]*4,'Blush','Head',sides=8)
# Almond shaped eye surfaces: lid-defined geometry, layered colored iris and pupil.
for s in [-1,1]:
    cx=s*.046;cz=1.413
    ev=[(cx,-.087,cz)];ef=[];outline=[]
    for k in range(40):
        a=2*pi*k/40;x=cx+.031*cos(a);z=cz+(.015 if sin(a)>0 else .011)*sin(a)*(0.8+.2*abs(sin(a)))+s*.004*cos(a)
        y=-.087+.17*abs(x-cx);ev.append((x,y,z));outline.append((x,y-.001,z))
    for k in range(40):ef.append((0,k+1,(k+1)%40+1))
    mesh('AlmondEye',ev,ef,'EyeWhite','Head')
    ellipse('IrisOutline',(cx,-.0885,cz+.001),(.0135,.002,.0108),'IrisDark','Head',rings=12,sides=32)
    ellipse('Iris',(cx,-.0905,cz+.001),(.012,.0015,.0095),'Eyes','Head',rings=12,sides=32)
    ellipse('IrisLowerLight',(cx,-.092,cz-.004),(.009,.0008,.0039),'IrisLight','Head',rings=8,sides=24)
    ellipse('Pupil',(cx,-.093,cz+.002),(.0045,.001,.006),'Lashes','Head',rings=10,sides=24)
    ellipse('Catchlight',(cx-.004,-.095,cz+.007),(.003,.001,.0038),'EyeWhite','Head',rings=6,sides=12)
    upper=outline[:21];tube('UpperLash',upper,[.0018+sin(pi*k/20)*.001 for k in range(21)],'Lashes','Head',sides=6)
    tube('LowerLid',outline[20:]+[outline[0]],[.0007]*21,'Blush','Head',sides=6)
    tube('Eyebrow',[(cx-.026,-.077,cz+.03),(cx,-.084,cz+.037),(cx+.025,-.075,cz+.034)],[.001,.0018,.0006],'Hair','Head',sides=6)
    outer=cx+s*.03
    for k in range(3):tube('LashTip',[(outer-s*k*.003,-.084,cz+.003+k*.003),(outer+s*.007-s*k*.002,-.083,cz+.01+k*.003)],[.0013,0],'Lashes','Head',sides=5)
# Black blouse, tailored tapered bodice and fitted high collar.
loft('Bodice',[(.884,.087,.056),(.96,.078,.055),(1.04,.101,.068),(1.14,.126,.073),(1.21,.133,.064),(1.244,.09,.047)],'BlackCloth','Clothes',n=64)
loft('HighCollar',[(1.215,.04,.035),(1.285,.039,.034)],'BlackCloth','Clothes','Neck',48)
for z in [1.24,1.211,1.181]:ellipse('PearlButton',(0,-.070 if z<1.22 else -.037,z),(.0032,.002,.0032),'WhiteCloth','Accessories','Chest',6,12)
# Leg, shoes and arms in relaxed A bind pose. Individual finger bones and weights.
finger_data=[]
for s,label in [(1,'Left'),(-1,'Right')]:
    x=s*.066
    tube('Stocking',[(x,0,.89),(x,0,.63),(x,-.004,.43),(x,.006,.13)],[.048,.041,.029,.025],'BlackCloth','Body',sides=20,weights=[{label+'UpperLeg':1},{label+'UpperLeg':.6,label+'LowerLeg':.4},{label+'LowerLeg':1},{label+'Foot':1}])
    ellipse('MaryJaneShoe',(x,-.031,.065),(.040,.084,.050),'Hair','Body',label+'Foot',12,24)
    ellipse('ShoeSole',(x,-.033,.028),(.041,.086,.015),'BlackCloth','Body',label+'Foot',8,24)
    tube('ShoeStrap',[(x-.033,-.025,.079),(x,-.030,.110),(x+.033,-.025,.079)],[.008]*3,'BlackCloth','Body',label+'Foot',10)
    shoulder=(s*.127,0,1.21);elbow=(s*.271,0,1.105);wrist=(s*.40,-.006,.974)
    tube('GatheredSleeve',[shoulder,(s*.16,0,1.202),(s*.219,0,1.158),elbow,(s*.32,0,1.064),(s*.388,-.005,.986)], [.055,.061,.052,.039,.041,.027],'BlackCloth','Clothes',sides=24,weights=[{label+'UpperArm':1},{label+'UpperArm':1},{label+'UpperArm':1},{label+'UpperArm':.4,label+'LowerArm':.6},{label+'LowerArm':1},{label+'LowerArm':1}])
    tube('WhiteCuff',[(s*.375,-.005,.998),(s*.402,-.006,.971)],[.032,.031],'WhiteCloth','Clothes',label+'LowerArm',24)
    tube('Palm',[wrist,(s*.421,-.008,.947),(s*.450,-.008,.921)],[.021,.027,.022],'Skin','Body',label+'Hand',sides=16,flatten=.52)
    for fi,(fname,length) in enumerate([('Index',.056),('Middle',.061),('Ring',.055),('Little',.043)]):
        base=Vector((s*.438,-.008,.938))+Vector((s*.74,0,.67))*(1.5-fi)*.013;direction=Vector((s*.67,0,-.74))
        points=[base,base+direction*length*.36,base+direction*length*.72+Vector((0,-.003,0)),base+direction*length+Vector((0,-.006,0))]
        names=[label+fname+str(i) for i in [1,2,3]]
        tube(fname+'Finger',points,[.0065,.006,.0053,.0018],'Skin','Body',sides=10,weights=[{names[0]:1},{names[0]:.5,names[1]:.5},{names[1]:.5,names[2]:.5},{names[2]:1}])
        finger_data.append((names,points,label+'Hand'))
    points=[Vector((s*.414,-.023,.952)),Vector((s*.432,-.048,.941)),Vector((s*.449,-.054,.923)),Vector((s*.457,-.052,.91))]
    names=[label+'Thumb'+str(i) for i in [1,2,3]];finger_data.append((names,points,label+'Hand'))
    tube('Thumb',points,[.009,.008,.006,.002],'Skin','Body',sides=10,weights=[{names[0]:1},{names[0]:.5,names[1]:.5},{names[1]:.5,names[2]:.5},{names[2]:1}])
# Full long dress and separate white apron: broad shaped vertical folds, scalloped hem.
def skirt_weights(z):
    def weights(a):
        section=(a%(2*pi))/(2*pi)*8;index=int(section)%8;blend=section-int(section)
        amount=max(0,min(.95,(.90-z)/.45))
        return {'Hips':1-amount,'Skirt'+str(index):amount*(1-blend),'Skirt'+str((index+1)%8):amount*blend}
    return weights
levels=[(.941,.087,.059),(.872,.119,.083),(.764,.18,.13),(.625,.25,.187),(.45,.307,.226),(.265,.36,.262),(.183,.373,.273)]
loft('DressWithLongFolds',levels,'BlackCloth','Clothes','Hips',128,.012,[skirt_weights(z) for z,_,_ in levels])
apron=[(z,rx+.006,ry+.011) for z,rx,ry in levels]
loft('WhiteApronWithSewnFolds',apron,'WhiteCloth','Clothes','Hips',96,.014,[skirt_weights(z) for z,_,_ in levels],-1.64,1.64)
# Scalloped continuous lower frill, visibly layered.
for band in [0,1]:
    vv=[];ff=[]
    for j in range(4):
        for k in range(257):
            a=2*pi*k/256;r=.375+.004*band+.011*sin(k*pi/4)*j/3
            vv.append((r*sin(a),-(r*.73)*cos(a),.18-j*.013+band*.017+.006*cos(a*24)*(j/3)))
        if j:
            for k in range(256):q=(j-1)*257+k;ff.append((q,q+1,q+258,q+257))
    mesh('ScallopedHem',vv,ff,'WhiteCloth','Clothes','Hips',[skirt_weights(v[2])(math.atan2(v[0],-v[1])) for v in vv])
loft('Waistband',[(.924,.091,.065),(.961,.09,.064)],'WhiteCloth','Clothes','Hips',64)
ribbon('ApronBib',[(0,-.060,.959),(0,-.076,1.03),(0,-.080,1.11),(0,-.072,1.172)],[.073,.077,.072,.066],'WhiteCloth','Clothes','Chest')
for s in [-1,1]:
    points=[]
    for k in range(25):
        t=k/24;points.append((s*(.077+.025*sin(t*pi)),-.065-.010*sin(t*pi),.972+t*.264))
    ribbon('ShoulderApronStrap',points,[.018]*25,'WhiteCloth','Clothes','Chest',.006)
    for k in range(25):
        p=points[k];points[k]=(p[0]+s*.022,p[1]-.003,p[2])
    ribbon('ShoulderRuffle',points,[.015]*25,'WhiteCloth','Clothes','Chest',.010)
# Back bow with softly inflated loops and split ends (original completion).
for s in [-1,1]:
    ribbon('BackBowLoop',[(s*.015,.071,.941),(s*.064,.098,.97),(s*.108,.083,.96),(s*.075,.072,.927),(s*.015,.071,.941)],[.006,.019,.025,.02,.006],'WhiteCloth','Accessories','Hips')
    ribbon('BackBowTail',[(s*.019,.078,.93),(s*.043,.105,.851),(s*.052,.152,.755)],[.021,.026,.03],'WhiteCloth','Accessories','Hips')
ellipse('BowKnot',(0,.074,.945),(.023,.019,.02),'WhiteCloth','Accessories','Hips')
# Layered flowing hair strands are closed tapered ribbon volumes, not round tubes.
def hairlock(name,path,widths,depths,bone='Head'):
    vs=[];fs=[];sides=10
    for j,p in enumerate(path):
        for k in range(sides):
            a=2*pi*k/sides;vs.append((p[0]+widths[j]*cos(a),p[1]+depths[j]*sin(a),p[2]))
        if j:
            for k in range(sides):q=(j-1)*sides+k;r=(j-1)*sides+(k+1)%sides;fs.append((q,r,r+sides,q+sides))
    fs.append(tuple(reversed(range(sides))));fs.append(tuple((len(path)-1)*sides+k for k in range(sides)))
    o=mesh(name,vs,fs,'Hair','Hair',bone);mod=o.modifiers.new('Sculpted hair surface','SUBSURF');mod.levels=1;return o
# Crown cap restricted to upper skull to leave the face exposed.
vs=[];fs=[]
for j in range(15):
    t=.015+(1.43-.015)*j/14
    for k in range(64):
        a=2*pi*k/64;t=.015+((1.15 if cos(a)>0 else 1.43)-.015)*j/14;vs.append((.102*sin(t)*sin(a),-.091*sin(t)*cos(a)+.009,1.422+.129*cos(t)))
    if j:
        for k in range(64):q=(j-1)*64+k;r=(j-1)*64+(k+1)%64;fs.append((q,r,r+64,q+64))
mesh('HairCrown',vs,fs,'Hair','Hair')
# Back locks splay apart towards pointed upward curl tips.
for i in range(15):
    a=-1.6+3.2*i/14;x=.094*sin(a);y=.066+.018*cos(a);s=1 if x>=0 else -1
    endx=x*2.55+s*.014*sin(i*3.1);endz=.991+.046*cos(i*1.7)
    hairlock('BackLayer%02d'%i,[(x*.68,y*.65,1.518),(x,y,1.39),(x*1.12,y+.01,1.244),(x*1.43,y+.023,1.112),(endx*.9,y+.013,endz),(endx,y-.005,endz+.035)],[.026,.031,.029,.025,.014,.0005],[.012,.019,.018,.015,.010,.0004],'HairBack')
# Asymmetric broad fringe covering image-left eye; opposite fringe frames visible eye.
for i in range(5):
    x=[-.082,-.060,-.041,-.020,.001][i];w=[.020,.025,.021,.028,.019][i];end=[.018,-.009,.01,-.018,.014][i]
    hairlock('SweptFringe%02d'%i,[(x*.4,-.063,1.536),(x,-.084,1.483),(x-.014,-.097,1.405),(x-.026,-.088,1.329),(x-.041,-.074,1.296+i*.008+end)],[w*.8,w,w*.93,w*.57,.0003],[.007,.012,.009,.007,.0003])
for i in range(4):
    hairlock('PartedFringe%02d'%i,[(.003+i*.012,-.068,1.537),(.055+i*.015,-.078,1.487),(.081+i*.014,-.07,1.439),(.103+i*.013,-.057,1.406+i*.009)],[.021,.018,.011,.0003],[.008,.010,.007,.0003])
for s in [-1,1]:
    for i in range(3):
        hairlock('FaceFramingLock',[(s*.082,-.038+i*.014,1.473),(s*.105,-.042+i*.015,1.33),(s*(.126+i*.013),.079+i*.016,1.198),(s*(.190+i*.023),.102+i*.02,1.135),(s*(.218+i*.025),.112+i*.02,1.162)],[.02,.024,.02,.012,0],[.012,.011,.009,.006,0],'HairBack')
# Sculpted horse ears and inset inner shell.
for s in [-1,1]:
    hairlock('HorseEar',[(s*.063,.003,1.52),(s*.081,.012,1.573),(s*.095,.018,1.632),(s*.103,.021,1.674)],[.027,.029,.017,.0003],[.019,.02,.012,.0003])
    mesh('EarInner',[(s*.061,-.015,1.55),(s*.100,.002,1.651),(s*.095,-.007,1.57)],[(0,1,2)],'BlackCloth','Head')
# White headband with pleated perimeter and rose-like curved petals.
for k in range(37):
    a=-1.28+2.56*k/36
    c=(.10*sin(a),-.032,1.438+.124*cos(a))
    # Successive folded scallops create real silhouette rather than a flat white stripe.
    w=.0105
    mesh('HeadbandPleat',[(c[0]-w,c[1]-.002,c[2]-.008),(c[0],c[1]-.011,c[2]+.003),(c[0]+w,c[1]-.002,c[2]-.008),(c[0]+w,c[1]+.012,c[2]+.016),(c[0],c[1]+.003,c[2]+.023),(c[0]-w,c[1]+.012,c[2]+.016)],[(0,1,4,5),(1,2,3,4)],'WhiteCloth','HeadAccessories')
def flower(cx,cy,cz,r):
    for layer in range(3):
        for p in range(5):
            a=2*pi*p/5+layer*.63;rr=r*(1-.26*layer);verts=[]
            for j in range(5):
                t=j/4
                for k in range(5):
                    u=k/4*2-1;rad=rr*(.18+.82*t);ang=a+u*.65*sin(t*pi*.8)
                    verts.append((cx+rad*cos(ang),cy-.004-layer*.002-rr*.32*sin(t*pi)+rr*.11*u*u,cz+rad*sin(ang)))
            faces=[]
            for j in range(4):
                for k in range(4):q=j*5+k;faces.append((q,q+1,q+6,q+5))
            o=mesh('RosePetal',verts,faces,'WhiteCloth','HeadAccessories'); mod=o.modifiers.new('Soft petals','SUBSURF');mod.levels=1; mod=o.modifiers.new('Petal thickness','SOLIDIFY');mod.thickness=.0006
    ellipse('FlowerHeart',(cx,cy-.008,cz),(.003,.004,.003),'WhiteCloth','HeadAccessories',rings=6,sides=10)
for i in range(11):
    a=-1.40+2.8*i/10
    flower(.104*sin(a),-.058,1.447+.092*cos(a),.016 if i%3 else .023)
# Named armature, explicit per-vertex weights, additional skirt and hair bones.
bpy.ops.object.armature_add();rig=bpy.context.object;rig.name='RiceShowerRig';bpy.ops.object.mode_set(mode='EDIT');rig.data.edit_bones.remove(rig.data.edit_bones[0])
def bone(name,head,tail,parent=None):
    b=rig.data.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=rig.data.edit_bones[parent]
bone('Root',(0,0,0),(0,0,.1));bone('Hips',(0,0,.88),(0,0,.975),'Root');bone('Spine',(0,0,.975),(0,0,1.092),'Hips');bone('Chest',(0,0,1.092),(0,0,1.235),'Spine');bone('Neck',(0,0,1.235),(0,0,1.305),'Chest');bone('Head',(0,0,1.305),(0,0,1.526),'Neck');bone('HairBack',(0,.061,1.40),(0,.083,1.05),'Head')
for s,label in [(1,'Left'),(-1,'Right')]:
    bone(label+'Shoulder',(s*.035,0,1.218),(s*.127,0,1.21),'Chest')
    bone(label+'UpperArm',(s*.127,0,1.21),(s*.271,0,1.105),label+'Shoulder')
    bone(label+'LowerArm',(s*.271,0,1.105),(s*.40,-.006,.974),label+'UpperArm')
    bone(label+'Hand',(s*.40,-.006,.974),(s*.445,-.008,.929),label+'LowerArm')
    bone(label+'UpperLeg',(s*.066,0,.89),(s*.066,-.004,.43),'Hips')
    bone(label+'LowerLeg',(s*.066,-.004,.43),(s*.066,.006,.13),label+'UpperLeg')
    bone(label+'Foot',(s*.066,.006,.13),(s*.066,-.064,.051),label+'LowerLeg')
    bone(label+'Toes',(s*.066,-.064,.051),(s*.066,-.105,.045),label+'Foot')
for names,points,parent in finger_data:
    for j,name in enumerate(names):bone(name,points[j],points[j+1],parent if j==0 else names[j-1])
for k in range(8):
    a=k*2*pi/8;bone('Skirt'+str(k),(.085*sin(a),-.06*cos(a),.902),(.28*sin(a),-.21*cos(a),.24),'Hips')
bpy.ops.object.mode_set(mode='OBJECT')
# Apply refinements, join by first-person visibility group; preserve vertex groups.
joined=[]
for name,objects in parts.items():
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects:
        bpy.context.view_layer.objects.active=ob
        for mod in list(ob.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
        ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();ob=objects[0];ob.name=name
    mod=ob.modifiers.new('CharacterSkin','ARMATURE');mod.object=rig;ob.parent=rig
    # Recalculate consistent exterior normals and add UV projection for future painting.
    bpy.context.view_layer.objects.active=ob;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.uv.smart_project(island_margin=.006);bpy.ops.object.mode_set(mode='OBJECT')
    mod=ob.modifiers.new('Deterministic triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
    if name=='HeadAccessories':
        mod=ob.modifiers.new('Petal surface optimization','DECIMATE');mod.ratio=.48;bpy.ops.object.modifier_apply(modifier=mod.name)
    joined.append(ob)
# Sweep the lower hair behind the sleeve's full initial walk envelope.
for v in bpy.data.objects['Hair'].data.vertices:v.co.y+=.12*max(0,min(1,(1.30-v.co.z)/.15))
# Feet are exactly on ground; fit total ear-tip height to 1.65 m.
fit_scale=1.65/(1.674-.013)
for ob in joined:
    for v in ob.data.vertices:v.co.z-=.013;v.co*=fit_scale
bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for b in rig.data.edit_bones:
    b.head.z-=.013;b.tail.z-=.013;b.head*=fit_scale;b.tail*=fit_scale
bpy.ops.object.mode_set(mode='OBJECT')
# Animation clips retain bone-driven deformation, no root translation.
rig.animation_data_create()
for clip,length in [('Idle',60),('Walk',32)]:
    action=bpy.data.actions.new(clip);rig.animation_data.action=action
    for f in range(1,length+2,2):
        t=(f-1)/length*2*pi
        for p in rig.pose.bones:p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.location=(0,0,0)
        rig.pose.bones['LeftUpperArm'].rotation_euler.z=.72
        rig.pose.bones['RightUpperArm'].rotation_euler.z=-.72
        rig.pose.bones['Chest'].rotation_euler.x=.015*sin(t)
        rig.pose.bones['HairBack'].rotation_euler.x=.018*sin(t+.5)
        if clip=='Walk':
            rig.pose.bones['Hips'].location.z=.008*(1-cos(t*2))
            for sign,label in [(1,'Left'),(-1,'Right')]:
                phase=t+(0 if sign==1 else pi)
                rig.pose.bones[label+'UpperLeg'].rotation_euler.x=.19*sin(phase)
                rig.pose.bones[label+'LowerLeg'].rotation_euler.x=-.22*max(0,-sin(phase))
                rig.pose.bones[label+'Foot'].rotation_euler.x=-.12*sin(phase)
                rig.pose.bones[label+'UpperArm'].rotation_euler.x=-.16*sin(phase)
                rig.pose.bones[label+'LowerArm'].rotation_euler.x=-.10-.05*sin(phase)
            for k in range(8):rig.pose.bones['Skirt'+str(k)].rotation_euler.x=.012*sin(t+k*.4)
        for p in rig.pose.bones:p.keyframe_insert('rotation_euler',frame=f);p.keyframe_insert('location',frame=f)
    action.use_fake_user=True
rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.context.scene.render.fps=30
# Export only character, cameras and lights stay in editable source for review.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for ob in joined:ob.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'RiceShower.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,mesh_smooth_type='FACE',use_tspace=True)
# Studio preview cameras: orthographic front, side, back, face and hands.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=1000;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.world.color=(.16,.16,.16)
scene.view_settings.view_transform='AgX'
def area(name,loc,energy,size):
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=name;o.data.energy=energy;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.9))-o.location).to_track_quat('-Z','Y').to_euler()
area('WarmKey',(-2,-3,4),380,3);area('CoolFill',(2,-2,2),230,2);area('HairRim',(0,2,3),470,2)
bpy.ops.object.camera_add();cam=bpy.context.object;cam.name='CharacterReviewCamera';cam.data.type='ORTHO';scene.camera=cam
world=scene.world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.19,.22,.27,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4
stats={'meshes':{o.name:{'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons)} for o in joined},'bones':len(rig.data.bones),'materials':list(mats),'clips':['Idle','Walk'],'height':1.65,'front':'Blender -Y, FBX -Z forward / Y up','authored_completion':['back apron bow','stockings','Mary Jane shoes']}
with open(os.path.join(ART,'model-manifest.json'),'w') as f:json.dump(stats,f,indent=2)
cam.location=(2.4,-4,1.65);cam.rotation_euler=(Vector((0,0,.85))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.93
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'art-source/character/RiceShower.blend'))
for name,loc,target,scale in [('front',(0,-4,.92),(0,0,.85),1.91),('three-quarter',(2.4,-4,1.65),(0,0,.85),1.93),('back',(0,4,1.15),(0,0,.85),1.91),('face',(.12,-3,1.46),(0,0,1.46),.42),('hand',(.8,-3,1.06),(.43,0,.94),.24)]:
    if name=='hand':
        target=(rig.pose.bones['LeftHand'].head+rig.pose.bones['LeftHand'].tail)/2;loc=target+Vector((.1,-3,.14))
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    scene.render.filepath=os.path.join(ART,name+'.png');bpy.ops.render.render(write_still=True)
rig.animation_data.action=bpy.data.actions['Walk'];scene.frame_set(9)
cam.location=(2.4,-4,1.65);cam.rotation_euler=(Vector((0,0,.85))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.93;scene.render.filepath=os.path.join(ART,'walk-pose.png');bpy.ops.render.render(write_still=True)
print('CHARACTER_EXPORT_SUCCESS '+json.dumps(stats))
