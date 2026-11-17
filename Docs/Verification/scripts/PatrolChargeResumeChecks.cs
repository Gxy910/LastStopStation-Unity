using System;using System.Collections;using System.Reflection;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using YuDian;
[InitializeOnLoad] public static class PatrolChargeResumeChecks{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static bool started;
 static PatrolChargeResumeChecks(){if(SessionState.GetBool("PatrolChargeResumeChecks",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o):o.GetType().GetField(n,F).GetValue(o);}
 static void S(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static void Check(bool b,string text){if(!b)throw new Exception(text);Debug.Log("CHARGE RESUME PASS "+text);}
 static void Move(FirstPersonWalker walker,Transform root,Vector3 local){var p=new GameObject("charge player").transform;p.SetPositionAndRotation(root.TransformPoint(local),root.rotation);walker.MoveTo(p);UnityEngine.Object.Destroy(p.gameObject);Physics.SyncTransforms();}
 static Component Setup(object mission,FirstPersonWalker walker,Transform root,int wave,int armor){
  C(mission,"DebugJumpToWave",wave);Component elite=null;foreach(Component e in (IEnumerable)G(mission,"Enemies")){if((bool)G(e,"Alive")&&G(e,"EnemyKind").ToString()=="Charger"&&elite==null)elite=e;else if((bool)G(e,"Alive"))C(e,"TakeHit",int.MaxValue,false,Vector3.forward);}if(!elite)throw new Exception("elite missing");
  Move(walker,root,new Vector3(-6.5f,.05f,4));var body=elite.GetComponent<CharacterController>();body.enabled=false;elite.transform.position=root.TransformPoint(new Vector3(-6.5f,.05f,7));body.enabled=true;Physics.SyncTransforms();S(mission,"health",3);S(mission,"armor",armor);S(mission,"invulnerability",0f);return elite;
 }
 static void Charge(Component elite){C(elite,"Tick",.01f);Check(G(elite,"chargePhase").ToString()=="Windup","actual approach triggers windup");C(elite,"Tick",1.6f);Check(G(elite,"chargePhase").ToString()=="Charge","windup becomes charge");C(elite,"Tick",.35f);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolChargeResumeChecks",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolChargeResumeChecks",false);try{
  var mission=G(UnityEngine.Object.FindObjectOfType<StationPrototype>(),"patrol");var walker=UnityEngine.Object.FindObjectOfType<FirstPersonWalker>();var story=new StationStory(3);story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);C(mission,"Begin",story);var root=((GameObject)G(G(mission,"yard"),"Root")).transform;
  var e=Setup(mission,walker,root,20,0);Charge(e);Check((int)G(mission,"Health")==2,"wave20 actual charge costs one health");
  e=Setup(mission,walker,root,25,0);Charge(e);Check((int)G(mission,"Health")==1,"wave25 actual charge costs two health");
  e=Setup(mission,walker,root,25,1);Charge(e);Check((int)G(mission,"Health")==3&&(int)G(mission,"Armor")==0,"armor blocks full actual charge");
  e=Setup(mission,walker,root,25,0);C(e,"Tick",.01f);C(e,"Tick",1.6f);Move(walker,root,new Vector3(-4.5f,.05f,4));C(e,"Tick",.35f);Check((int)G(mission,"Health")==3,"side-step avoids actual charge");
  C(mission,"Finish",false);Debug.Log("CHARGE RESUME CHECKS PASSED");EditorApplication.Exit(0);
 }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}}
}
