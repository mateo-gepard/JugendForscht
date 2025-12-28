# Fixing Pink Plane and Stereo Mismatch in Quest APK

## Issues Identified:
1. **Pink Material** = Shader not found or not included in build
2. **Wrong Position** = Stereo rendering timing or coordinate system issue  
3. **Left/Right Eye Mismatch** = Shader or render settings not VR-compatible

## Solutions Applied:

### 1. Shader Fixes (in MoleculePlaneAlignment.cs):
- Changed from `Transparent/Diffuse` → `Unlit/Transparent` (mobile VR compatible)
- Added fallback chain: `Unlit/Transparent` → `Mobile/Particles/Alpha Blended` → `Unlit/Color`
- These shaders are guaranteed to be in Quest builds

### 2. VRPlaneRendererFix.cs Script:
- Add to any GameObject in scene
- Automatically finds and fixes plane shader issues
- Can run continuously to catch runtime shader problems

### 3. Required Unity Settings:

#### Add Shaders to Build:
1. **Edit → Project Settings → Graphics**
2. Scroll to **Always Included Shaders**
3. Increase **Size** by 3
4. Add these shaders:
   - `Unlit/Transparent`
   - `Unlit/Color`
   - `Mobile/Particles/Alpha Blended`

#### VR Stereo Rendering Mode:
1. **Edit → Project Settings → XR Plug-in Management → Oculus**
2. Set **Stereo Rendering Mode** to `Multiview` or `Single Pass Instanced`
3. Ensure **Symmetric Projection** is enabled

### 4. Test in Editor First:
- Press Play in Unity
- Check plane isn't pink
- Use `adb logcat` to see logs when running APK
- Look for `[PlaneAlignment]` and `[VRPlaneFix]` log messages

### 5. If Still Pink:
Create a Material Asset:
1. Right-click in Project → Create → Material
2. Name it "PlaneMaterial"
3. Set Shader to `Unlit/Transparent`
4. Set Color to RGB(0.6, 0.7, 0.8) Alpha(0.15)
5. In MoleculePlaneAlignment script, drag this material into `planeMaterial` field
6. This ensures the material is always in the build

### 6. If Position/Stereo Still Wrong:
The plane might need to be parented to the camera or use a different coordinate system. Let me know the exact behavior and I can adjust the positioning logic.
