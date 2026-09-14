# Reference character — editable first modeled pass

`RiceShower.blend` is the editable source; `build_rice_shower.py` reproducibly generates it and `Sunset Glow/Assets/_Game/Art/Characters/RiceShower/RiceShower.fbx` using local Blender 5.2. No downloaded model or image plane is used. The geometry is reference-inspired and not yet a final likeness approval.

## Integration

- Metres, total ear-tip height 1.65 m, shoe sole at ground level. Blender front -Y / Z up; FBX export forward -Z / up Y. Verify Unity facing before attaching the player.
- Six skinned render groups: `Head`, `Hair`, `HeadAccessories`, `Body`, `Clothes`, `Accessories`. Hide the first three from first-person camera (retain shadows). `Accessories` contains the back waist bow and buttons and should remain visible.
- Ten shared material slots: `Skin`, `Hair`, `BlackCloth`, `WhiteCloth`, `Eyes`, `IrisLight`, `IrisDark`, `EyeWhite`, `Lashes`, `Blush`. All opaque. Iris material names must be preserved; mapping every eye material to one color loses the iris/white contrast. Flowers and apron use the same WhiteCloth.
- 61 bones. Root → Hips → Spine → Chest → Neck → Head. Left/RightShoulder, UpperArm, LowerArm, Hand, UpperLeg, LowerLeg, Foot, Toes. Left/RightIndex1–3, Middle1–3, Ring1–3, Little1–3, Thumb1–3. `Skirt0`–`Skirt7` and `HairBack` are secondary bones. Every mesh has explicit vertex groups and an Armature modifier.
- `Idle`: 60-frame 2-second loop with breathing and relaxed arms. `Walk`: 32-frame loop at 30 fps with opposite arms/legs, knee bend and secondary skirt motion. They are in-place bone animations; the controller must remain the only root movement authority. The export bind pose is a relaxed A pose. Generic playback is supported; Unity Humanoid validation is a separate integration check.
- UVs are packed using Smart Project and all exported faces are triangulated. Unity integration adds a procedural woven micro-normal map; no hand-painted texture, authored roughness atlas or character LOD pass is claimed.

## Reference and completion decisions

The user's two images establish asymmetric black hair over the image-left eye, blue-violet eye, horse ears, white floral/frilled headdress, high-neck black long sleeves and white apron with a broad folded skirt. The face, almond eyelids, iris layers, lashes, volumetric hair locks, petal surfaces, real hem frills, hands and separate finger joints are actual editable geometry. The rear bow, dark stockings and Mary Jane shoes are authored completions because those areas were not shown in the references. No tail was invented.

## Review evidence and limitations

`artifacts/character/` contains actual Blender-rendered front, three-quarter, back, face, hand and walk-pose PNGs plus `model-manifest.json`. These are source-model previews, not Unity runtime evidence. Unity capture and animation integration are owned by the main task.

This is the first complete mesh/skin/action pass replacing a block placeholder. The reference's exact facial expression, painted anime shading, complex salon-quality hair topology, production garment stitching, collision-driven skirt dynamics and full locomotion set remain further art work. The hand uses individually modeled overlapping finger/palm surfaces, not a watertight retopologized hand. No automatic IK or clothing collision is present. Back/hair/cloth helper weights are intentionally limited and should be reviewed on slopes, turns and deeper knee flexion before labeling D3 complete.
