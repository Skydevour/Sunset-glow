using System;
using System.Collections;
using System.IO;
using UnityEngine;
using SunsetGlow.EnvironmentArt;
using SunsetGlow.Player;

namespace SunsetGlow.Debugging
{
    public sealed class CoastalCaptureRunner:MonoBehaviour
    {
        public CoastalEnvironmentPresenter presenter;
        public FirstPersonController player;
        string output;
        int errors;
        bool transitionPassed=true, shelterPassed, outdoorSnowPassed;
        void Awake(){Application.logMessageReceived+=OnLog;}
        IEnumerator Start()
        {
            var args=System.Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-coastalCapture")output=args[i+1];
            if(string.IsNullOrEmpty(output))yield break;
            if(!Path.IsPathRooted(output)){Debug.LogError("Use absolute coastal capture directory");Application.Quit(2);yield break;}
            if(Directory.Exists(output)&&Directory.GetFiles(output,"*.png").Length>0){Debug.LogError("Capture directory must be fresh");Application.Quit(2);yield break;}
            Directory.CreateDirectory(output);
            player.SetPaused(false);
            if(Array.IndexOf(args,"-coastalBenchmark")>=0){presenter.SetLook(2,true);CoastView();yield break;}
            yield return new WaitForSecondsRealtime(12);
            for(int look=0;look<3;look++)
            {
                presenter.SetLook(look,look==0);
                CoastView();
                if(look>0)
                {
                    yield return new WaitForSecondsRealtime(4);
                    if(look==2)
                    {
                        transitionPassed &= presenter.SnowCoverage>0 && presenter.SnowCoverage<1;
                        string intermediate=Path.Combine(output,"transition-textures");Directory.CreateDirectory(intermediate);
                        presenter.CaptureSeasonalTextures(intermediate);
                    }
                    transitionPassed &= Time.timeScale==1;
                    yield return Capture(presenter.looks[look].displayName+"-transition");
                }
                yield return new WaitForSecondsRealtime(10);
                yield return Capture(presenter.looks[look].displayName+"-coast");
                player.Teleport(new Vector3(-42,1.3f,-26),Quaternion.Euler(3,345,0));
                yield return new WaitForSecondsRealtime(3);
                yield return Capture(presenter.looks[look].displayName+"-shoreline");
                player.Teleport(player.observationPoints[3].position,Quaternion.Euler(9,235,0));
                yield return new WaitForSecondsRealtime(5);
                yield return Capture(presenter.looks[look].displayName+"-island");
                if(look==2)outdoorSnowPassed=presenter.IsSnowing && presenter.snow.emission.rateOverTime.constant>0 && presenter.snow.particleCount>0;
            }
            player.Teleport(player.observationPoints[4].position,player.observationPoints[4].rotation);
            yield return new WaitForSecondsRealtime(5);
            yield return Capture("Winter-cabin");
            presenter.CaptureSeasonalTextures(output);
            shelterPassed=presenter.IsSnowing && presenter.snow.emission.rateOverTime.constant==0 && presenter.snow.particleCount==0;
            if(!transitionPassed||!shelterPassed||!outdoorSnowPassed)Debug.LogError("Coastal transition or snow shelter regression");
            File.WriteAllText(Path.Combine(output,"capture.json"),JsonUtility.ToJson(new Result{errors=errors,unity=Application.unityVersion,
                width=Screen.width,height=Screen.height,timeScale=Time.timeScale,transitionPassed=transitionPassed,shelterPassed=shelterPassed,outdoorSnowPassed=outdoorSnowPassed,scope="Art preview only; inspect PNGs. Does not certify world calendar, weather simulation or 48-minute P0."},true));
            Application.Quit(errors==0?0:1);
        }
        void CoastView(){player.Teleport(new Vector3(-46,1.2f,-9),Quaternion.Euler(5,285,0));}
        IEnumerator Capture(string name)
        {
            string path=Path.Combine(output,name+".png");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path);
            double end=Time.realtimeSinceStartupAsDouble+10;
            while((!File.Exists(path)||new FileInfo(path).Length==0)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            if(!File.Exists(path)||new FileInfo(path).Length==0)Debug.LogError("Missing or empty screenshot "+name);
            Debug.Log("COASTAL_CAPTURE "+name);
        }
        void OnLog(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
        [Serializable]class Result{public int errors,width,height;public float timeScale;public string unity,scope;public bool transitionPassed,shelterPassed,outdoorSnowPassed;}
    }
}
