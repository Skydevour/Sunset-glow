"""Read-only source validation, run with Blender --background RiceShower.blend."""
import bpy, json, os
root=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
rig=bpy.data.objects['RiceShowerRig'];bones=set(rig.data.bones.keys());bad=[];count=0;influences=0
for ob in bpy.data.objects:
    if ob.type!='MESH':continue
    for v in ob.data.vertices:
        weights=[g for g in v.groups if g.weight>1e-7];count+=1;influences=max(influences,len(weights))
        if not weights or abs(sum(g.weight for g in weights)-1)>.001:bad.append([ob.name,v.index,'weight sum'])
        if any(ob.vertex_groups[g.group].name not in bones for g in weights):bad.append([ob.name,v.index,'missing bone'])
result={'passed':not bad,'skinnedVertices':count,'maxInfluences':influences,'boneCount':len(bones),'actions':list(bpy.data.actions.keys()),'issues':bad[:20]}
with open(os.path.join(root,'artifacts/character/skin-validation.json'),'w') as f:json.dump(result,f,indent=2)
print(json.dumps(result))
if bad:raise RuntimeError('Skin validation failed')
