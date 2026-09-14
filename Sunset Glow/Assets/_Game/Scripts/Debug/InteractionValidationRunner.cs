using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using SunsetGlow.Player;
using SunsetGlow.EnvironmentArt;

namespace SunsetGlow.Debugging
{
    public sealed class InteractionValidationRunner : MonoBehaviour
    {
        public FirstPersonController player;
        public WindField wind;
        public SurfaceQuery surfaces;
        public WaterContactSystem water;
        public FootContactEmitter feet;
        public CoastalEnvironmentPresenter presenter;
        string output;
        int errors, alternationErrors;
        bool? lastLeft;
        float dryHeading, deformationBefore, deformationAfter;
        readonly HashSet<SurfaceKind> contacted = new HashSet<SurfaceKind>();
        readonly List<string> checks = new List<string>(), failures = new List<string>();

        void Awake() { Application.logMessageReceived += OnLog; }

        IEnumerator Start()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i=0;i+1<args.Length;i++) if(args[i]=="-interactionCapture") output=args[i+1];
            if(string.IsNullOrEmpty(output)) yield break;
            if(!Path.IsPathRooted(output)) { Debug.LogError("-interactionCapture requires an absolute directory");Application.Quit(2);yield break; }
            if(Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length>0) { Debug.LogError("Interaction capture requires a fresh directory");Application.Quit(2);yield break; }
            Directory.CreateDirectory(output);
            if(player==null||wind==null||surfaces==null||water==null||feet==null||presenter==null) {Check("dependencies",false);Finish();yield break;}
            player.SetPaused(false); player.enabled=false;
            feet.Contact+=OnContact;
            if(Array.IndexOf(args,"-interactionBenchmark")>=0)
            {
                presenter.SetLook(2);
                player.Teleport(new Vector3(60,-.3f,-5),Quaternion.Euler(10,90,0));
                yield break; // BaselineRunner owns warmup, measurement and exit; no tests pollute samples.
            }
            yield return new WaitForSecondsRealtime(12);
            yield return Capture("01-day-baseline");

            // Use a remote point to avoid the movement controller refreshing this cache during fault injection.
            Vector3 queryPoint=new Vector3(76,0,14);
            yield return null;
            bool sample=water.TrySample(queryPoint,out float level);
            Check("native_water_query_finite",sample&&!float.IsNaN(level)&&!float.IsInfinity(level));
            int cacheBefore=water.CacheHitCount;
            water.TrySample(queryPoint,out _);
            Check("nearby_query_reuses_cache",water.CacheHitCount>cacheBefore);
            water.ForceQueryFailure=true;
            yield return new WaitForSecondsRealtime(.08f);
            Check("short_failure_retains_trusted_height",water.TrySample(queryPoint,out _)&&water.LastSampleWasCached);
            yield return new WaitForSecondsRealtime(water.trustworthyCacheSeconds+.08f);
            Check("expired_failure_returns_invalid",!water.TrySample(queryPoint,out _));
            water.ForceQueryFailure=false;
            yield return null;
            Check("query_recovers_after_fault",water.TrySample(queryPoint,out _));
            int budgetBefore=water.BudgetExceededCount;
            for(int i=0;i<water.maxQueriesPerFrame+2;i++)water.TrySample(new Vector3(75+i*2,0,18),out _);
            Check("query_budget_enforced",water.BudgetExceededCount>budgetBefore);
            yield return null;
            Check("query_budget_recovers_next_frame",water.TrySample(queryPoint,out _));

            bool wasWindEnabled=wind.enabled;wind.enabled=false;
            wind.SetTarget(WindField.DirectionFromAngle(359),3,1);wind.Advance(100);
            wind.SetTarget(WindField.DirectionFromAngle(1),3,1);wind.Advance(wind.responseSeconds*.7f);
            Check("wind_359_to_1_uses_short_arc",Mathf.Abs(Mathf.DeltaAngle(wind.CurrentAngle,0))<1.1f);
            Vector3 gustA=wind.SampleVelocity(Vector3.zero),gustB=wind.SampleVelocity(new Vector3(37,0,19));
            Check("gust_has_spatial_phase",(gustA-gustB).sqrMagnitude>.00001f);
            player.SetPaused(true);
            double phase=wind.PresentationTime;wind.Advance(1);
            Check("pause_freezes_wind_phase",wind.PresentationTime==phase);
            player.SetPaused(false);wind.enabled=wasWindEnabled;
            Check("coverage_priority_water_over_snow",SurfaceQuery.Classify(SurfaceKind.Wood,.2f,1,1)==SurfaceKind.Water);
            Check("coverage_snow_then_substrate",SurfaceQuery.Classify(SurfaceKind.Wood,0,1,0)==SurfaceKind.Snow&&SurfaceQuery.Classify(SurfaceKind.Wood,0,0,0)==SurfaceKind.Wood);

            // Find authored dry sand rather than relying on a hand-written label for a material slab.
            Vector3 dry=FindDrySand();
            player.Teleport(dry,Quaternion.Euler(28,dryHeading,0));
            yield return Settle();
            int count=feet.EmittedCount;
            player.BobEnabled=false;
            yield return Move(Vector2.up,30);
            Check("movement_emits_with_bob_disabled",feet.EmittedCount>count);
            Check("dry_sand_actual_contact",contacted.Contains(SurfaceKind.DrySand));
            count=feet.EmittedCount;
            yield return Move(Vector2.zero,50);
            Check("standing_has_no_steps",feet.EmittedCount==count);
            player.SetPaused(true);
            yield return Move(Vector2.up,20);
            Check("paused_has_no_steps",feet.EmittedCount==count);
            player.SetPaused(false);
            player.Teleport(dry,Quaternion.identity);
            yield return null;
            Check("teleport_has_no_steps",feet.EmittedCount==count);
            player.BobEnabled=true;

            player.Teleport(new Vector3(-1.9f,1.5f,8),Quaternion.Euler(0,270,0));
            yield return Move(Vector2.up,90);
            count=feet.EmittedCount;Vector3 wallPosition=player.transform.position;
            yield return Move(Vector2.up,50);
            Check("blocked_wall_has_no_steps",feet.EmittedCount==count&&(player.transform.position-wallPosition).sqrMagnitude<.01f);

            // Real controller traversal onto the authored walkable shelf. No standing-on-water shortcut.
            player.Teleport(new Vector3(48,1,-5),Quaternion.Euler(25,90,0));
            yield return Settle();
            int resets=player.ResetCount;
            for(int i=0;i<1100 && player.transform.position.x<67.5f && player.ResetCount==resets;i++)
            {player.SimulateMove(Vector2.up,1f/60);yield return null;}
            Check("coast_to_shallow_water_no_reset",player.ResetCount==resets&&player.transform.position.x>=67.5f);
            Check("water_actual_foot_contact",contacted.Contains(SurfaceKind.Water));
            player.SetLook(90,30);
            water.EmitRipple(player.transform.position+Vector3.right*2,1.4f);
            yield return new WaitForSecondsRealtime(.35f);
            yield return Capture("02-water-contact-ripples");
            int ripples=water.EmittedRippleCount;
            yield return Move(Vector2.zero,60);
            Check("stationary_water_has_no_new_ripples",water.EmittedRippleCount==ripples);
            for(int i=0;i<45;i++)water.EmitRipple(player.transform.position,1);
            yield return null;
            Check("ripple_pool_is_bounded",water.ActiveRippleCount<=water.rippleCapacity&&water.ActiveRippleCount<=32);
            Check("native_ripples_are_emitted",water.EmittedRippleCount>ripples);
            yield return new WaitForSecondsRealtime(water.rippleLifetime+.2f);
            Check("stationary_ripples_expire",water.ActiveRippleCount==0);
            yield return ReadDeformation(false);
            water.EmitRipple(player.transform.position+Vector3.right*2,2);
            yield return new WaitForSecondsRealtime(.25f);
            yield return ReadDeformation(true);
            Check("native_gpu_deformation_changes",deformationAfter>deformationBefore+.0001f);
            Debug.Log("INTERACTION_DEFORMATION before="+deformationBefore+" after="+deformationAfter);

            presenter.SetLook(2);
            player.Teleport(new Vector3(20,3,0),Quaternion.Euler(50,0,0));
            yield return Settle();
            yield return new WaitForSecondsRealtime(presenter.transitionSeconds+.5f);
            yield return Move(Vector2.up,120);
            Check("snow_actual_foot_contact",contacted.Contains(SurfaceKind.Snow));
            yield return Capture("03-winter-contacts");
            Check("left_right_alternation",alternationErrors==0&&feet.EmittedCount>5);
            resets=player.ResetCount;
            player.Teleport(new Vector3(72,-5,0),Quaternion.identity);
            player.SimulateMove(Vector2.zero,1f/60);
            Check("abnormal_deep_water_recovers",player.ResetCount>resets&&player.transform.position.y>=0);
            Check("global_physics_time_unchanged",Mathf.Approximately(Time.timeScale,1));

            if(Array.IndexOf(args,"-interactionMovie")>=0)
            {
                presenter.SetLook(0);wind.SetDebugPreset(2);
                player.Teleport(new Vector3(-18,2,0),Quaternion.Euler(-12,300,0));
                double start=Time.realtimeSinceStartupAsDouble;
                for(int i=0;i<=30;i++)
                {
                    while(Time.realtimeSinceStartupAsDouble<start+i*2)yield return null;
                    yield return Capture("cloud-wind-"+i.ToString("D3"));
                }
            }
            Check("no_runtime_errors",errors==0);Finish();
        }

        Vector3 FindDrySand()
        {
            // Require a continuous two-metre route including both lateral feet. The first sand
            // point found by the old test lay at the grass boundary, so its first step left sand.
            for(float z=-24;z<=-6;z+=2)
                for(float x=30;x<=51;x+=.5f)
                {
                    Vector3 start=new Vector3(x,0,z);
                    if(!DryAt(start,out Vector3 ground))continue;
                    for(int heading=0;heading<360;heading+=30)
                    {
                        Vector3 direction=Quaternion.Euler(0,heading,0)*Vector3.forward;
                        Vector3 right=Quaternion.Euler(0,heading,0)*Vector3.right;
                        bool valid=true;
                        for(int step=0;step<=8&&valid;step++)
                            valid=DryAt(start+direction*(step*.25f)+right*.12f,out _)&&DryAt(start+direction*(step*.25f)-right*.12f,out _);
                        if(valid){dryHeading=heading;Debug.Log("INTERACTION_DRY_ROUTE "+ground+" heading="+heading);return ground+Vector3.up*.08f;}
                    }
                }
            Check("authored_dry_sand_route_found",false);
            return new Vector3(43,1,-10);
        }
        bool DryAt(Vector3 point,out Vector3 ground)
        {
            ground=default;
            if(!Physics.Raycast(point+Vector3.up*10,Vector3.down,out var hit,15,~LayerMask.GetMask("Player")))return false;
            ground=hit.point;
            return surfaces.TryContact(hit.point+Vector3.up*.03f,out var c)&&c.kind==SurfaceKind.DrySand;
        }
        IEnumerator ReadDeformation(bool after)
        {
            Texture buffer=water.ocean!=null?water.ocean.GetDeformationBuffer():null;
            if(buffer==null||!SystemInfo.supportsAsyncGPUReadback){Check("native_deformation_buffer_available",false);yield break;}
            var request=AsyncGPUReadback.Request(buffer,0,TextureFormat.RFloat);
            double deadline=Time.realtimeSinceStartupAsDouble+5;
            while(!request.done&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!request.done||request.hasError){Check("native_deformation_readback",false);yield break;}
            var pixels=request.GetData<float>();
            float maximum=0;
            for(int i=0;i<pixels.Length;i++)if(!float.IsNaN(pixels[i]))maximum=Mathf.Max(maximum,Mathf.Abs(pixels[i]));
            if(after)deformationAfter=maximum;else deformationBefore=maximum;
        }
        IEnumerator Move(Vector2 input,int frames) {for(int i=0;i<frames;i++){player.SimulateMove(input,1f/60);yield return null;}}
        IEnumerator Settle() {yield return Move(Vector2.zero,90);}
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();
            string file=Path.Combine(output,name+".png");ScreenCapture.CaptureScreenshot(file);
            double deadline=Time.realtimeSinceStartupAsDouble+8;
            while((!File.Exists(file)||new FileInfo(file).Length==0)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Check(name+"_image",File.Exists(file)&&new FileInfo(file).Length>0);
        }
        void OnContact(FootContactEvent e) {if(lastLeft.HasValue&&lastLeft.Value==e.left)alternationErrors++;lastLeft=e.left;contacted.Add(e.surface.kind);}
        void Check(string name,bool passed){checks.Add(name+": "+(passed?"PASS":"FAIL"));if(!passed)failures.Add(name);Debug.Log("INTERACTION_CHECK "+name+" "+passed);}
        void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Assert||type==LogType.Exception)errors++;}
        void Finish()
        {
            File.WriteAllText(Path.Combine(output,"validation.json"),JsonUtility.ToJson(new Report{utc=DateTime.UtcNow.ToString("O"),passed=failures.Count==0,checks=checks.ToArray(),failures=failures.ToArray(),runtimeErrors=errors,contacts=feet!=null?feet.EmittedCount:0,deformationBefore=deformationBefore,deformationAfter=deformationAfter,scope="D1 runtime behavior and preset interaction evidence; character appearance, full weather and extended performance remain separate acceptance."},true));
            Application.Quit(failures.Count==0?0:1);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;if(feet!=null)feet.Contact-=OnContact;}
        [Serializable] sealed class Report{public string utc,scope;public bool passed;public string[] checks,failures;public int runtimeErrors,contacts;public float deformationBefore,deformationAfter;}
    }
}
