using System;using System.Collections;using System.Collections.Generic;using System.Diagnostics;using System.Reflection;using System.Threading.Tasks;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.Profiling;using YuDian;
[InitializeOnLoad] public static class PatrolProfilerResumeChecks {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;static bool started;
 static PatrolProfilerResumeChecks(){if(SessionState.GetBool("PatrolProfilerResumeChecks",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o):o.GetType().GetField(n,F).GetValue(o);}
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolProfilerResumeChecks",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static async void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolProfilerResumeChecks",false);
 try{
  Profiler.logFile=@"E:\codex111\2026-11-15\e-codex111-2026-11-11-new\work\patrol_profiler_resume_stream.data";Profiler.enableBinaryLog=true;Profiler.enabled=true;
  var main=UnityEngine.Object.FindObjectOfType<StationPrototype>();var mission=G(main,"patrol");var story=new StationStory(3);story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);C(mission,"Begin",story);main.enabled=false;
  foreach(int wave in new[]{201,501}){
   if(!(bool)C(mission,"DebugJumpToWave",wave))throw new Exception("jump failed "+wave);
   var elapsed=new List<double>();long before=Profiler.GetMonoUsedSizeLong(),peak=before;int turns=0,maxAlive=0;
   while(!(bool)G(mission,"BetweenWaves")&&turns++<1000){
    if((int)C(mission,"AliveCount")>=6 || (int)G(mission,"PendingCount")==0)
     foreach(Component e in (IEnumerable)G(mission,"Enemies"))if(e&&(bool)G(e,"Alive")){C(e,"TakeHit",int.MaxValue,false,Vector3.forward);break;}
    mission.GetType().GetField("invulnerability",F).SetValue(mission,999f);
    var timer=Stopwatch.StartNew();Profiler.BeginSample("YuDian.Patrol.HighWaveTick");C(mission,"Tick",.6f);Profiler.EndSample();timer.Stop();elapsed.Add(timer.Elapsed.TotalMilliseconds);
    maxAlive=Math.Max(maxAlive,(int)C(mission,"AliveCount"));peak=Math.Max(peak,Profiler.GetMonoUsedSizeLong());
    await Task.Delay(10);
   }
   if(!(bool)G(mission,"BetweenWaves"))throw new Exception("wave stuck "+wave);
   elapsed.Sort();double p95=elapsed[Mathf.Clamp((int)(elapsed.Count*.95f),0,elapsed.Count-1)];
   int retained=((ICollection)G(mission,"Enemies")).Count;
   UnityEngine.Debug.Log("PROFILE RESULT wave="+wave+" iterations="+turns+" retained="+retained+" maxAlive="+maxAlive+" tickP95ms="+p95.ToString("F3")+" tickMaxms="+elapsed[elapsed.Count-1].ToString("F3")+" monoDeltaBytes="+(peak-before));
  }
  C(mission,"Finish",false);
  var driver=typeof(EditorWindow).Assembly.GetType("UnityEditorInternal.ProfilerDriver");
  var save=driver?.GetMethod("SaveProfile",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(string)},null);
  string path=@"E:\codex111\2026-11-15\e-codex111-2026-11-11-new\work\patrol_profiler_resume.data";
  if(save!=null){save.Invoke(null,new object[]{path});UnityEngine.Debug.Log("PROFILE SAVE fileExists="+System.IO.File.Exists(path));}else UnityEngine.Debug.Log("PROFILE SAVE unavailable");
  Profiler.enabled=false;Profiler.enableBinaryLog=false;UnityEngine.Debug.Log("PROFILER CHECKS PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Profiler.enabled=false;Profiler.enableBinaryLog=false;UnityEngine.Debug.LogException(e);EditorApplication.Exit(1);}}
}
