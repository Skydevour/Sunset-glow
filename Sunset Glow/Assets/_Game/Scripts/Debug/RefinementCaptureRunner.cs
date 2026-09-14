using System;
using System.Collections;
using System.IO;
using UnityEngine;
using SunsetGlow.Player;
using SunsetGlow.EnvironmentArt;

namespace SunsetGlow.Debugging
{
    public sealed class RefinementCaptureRunner : MonoBehaviour
    {
        string output;
        int errors;
        [Serializable] class Report { public int runtimeErrors, skinnedMeshes, animatedVegetation; public bool characterPresent, animatorPresent; public string scope = "Actual Player front/side/back/face, first-person and vegetation stills. Images require visual review; not final character art approval."; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i=0;i+1<args.Length;i++) if(args[i]=="-refinementCapture")
                new GameObject("Refinement capture").AddComponent<RefinementCaptureRunner>().output=args[i+1];
        }
        IEnumerator Start()
        {
            if (!Path.IsPathRooted(output) || Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length > 0) { Debug.LogError("Use a fresh absolute capture directory"); Application.Quit(2); yield break; }
            Directory.CreateDirectory(output); Application.logMessageReceived += Log;
            yield return null;
            var player = FindFirstObjectByType<FirstPersonController>();
            var presenter = FindFirstObjectByType<CoastalEnvironmentPresenter>();
            var character = FindFirstObjectByType<ReferenceCharacter>();
            player.enabled = false; player.SetPaused(false);
            if (Array.IndexOf(System.Environment.GetCommandLineArgs(), "-refinementBenchmark") >= 0)
            {
                presenter.SetLook(2,true);
                player.Teleport(player.observationPoints[2].position, player.observationPoints[2].rotation);
                yield break; // BaselineRunner measures the settled forest with character and vegetation enabled.
            }
            presenter.SetLook(0,true);
            player.Teleport(new Vector3(35,2,-10),Quaternion.Euler(0,100,0));
            for(int i=0;i<90;i++) { player.SimulateMove(Vector2.zero,Time.deltaTime); yield return null; }
            yield return new WaitForSecondsRealtime(10);
            if (character != null)
            {
                character.SetShowcase(true);
                character.SetView(0,2.8f,.95f); yield return Capture("character-front");
                character.SetView(90,2.8f,.95f); yield return Capture("character-side");
                character.SetView(180,2.8f,.95f); yield return Capture("character-back");
                character.SetView(0,.60f,1.43f); yield return Capture("character-face");
                for(int i=0;i<75;i++) { player.SimulateMove(Vector2.up,Time.deltaTime); character.SetView(20,2.8f,.95f); yield return null; }
                yield return Capture("character-walk",.01f);
                player.SimulateMove(Vector2.zero,Time.deltaTime);
                character.SetShowcase(false);
            }
            player.SetLook(100,75); yield return Capture("first-person-body");
            if(player.observationPoints.Length>2) player.Teleport(player.observationPoints[2].position,player.observationPoints[2].rotation);
            yield return Capture("forest-day");
            yield return new WaitForSecondsRealtime(3); yield return Capture("forest-wind");
            presenter.SetLook(2); yield return new WaitForSecondsRealtime(10); yield return Capture("forest-winter");
            presenter.SetLook(1); yield return new WaitForSecondsRealtime(10); yield return Capture("forest-sunset");
            var vegetation=FindObjectsByType<VegetationMeshWind>(FindObjectsSortMode.None);
            int active=0; foreach(var v in vegetation) if(v.UpdateCount>0)active++;
            var report=new Report{runtimeErrors=errors, characterPresent=character!=null, animatorPresent=character!=null&&character.animator!=null&&character.animator.runtimeAnimatorController!=null,skinnedMeshes=character!=null?character.GetComponentsInChildren<SkinnedMeshRenderer>().Length:0,animatedVegetation=active};
            File.WriteAllText(Path.Combine(output,"validation.json"),JsonUtility.ToJson(report,true));
            Application.Quit(errors==0&&report.characterPresent&&report.animatorPresent&&active>0?0:2);
        }
        IEnumerator Capture(string name,float settle=.75f) { yield return new WaitForSecondsRealtime(settle); yield return new WaitForEndOfFrame(); var image=ScreenCapture.CaptureScreenshotAsTexture(); File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());Destroy(image); }
        void Log(string message,string stack,LogType type) { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++; }
        void OnDestroy() {Application.logMessageReceived-=Log;}
    }
}
