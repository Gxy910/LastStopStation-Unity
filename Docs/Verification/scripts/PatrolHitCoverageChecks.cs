using System;using System.Collections;using System.Reflection;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using YuDian;
[InitializeOnLoad] public static class PatrolHitCoverageChecks {
 const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;static bool started;static int pass;
 static PatrolHitCoverageChecks(){if(SessionState.GetBool("PatrolHitCoverageChecks",false))EditorApplication.update+=Poll;}
 static object G(object o,string n){var p=o.GetType().GetProperty(n,F);return p!=null?p.GetValue(o):o.GetType().GetField(n,F).GetValue(o);}
 static void S(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static void B(object o,string n,object v)=>o.GetType().GetField("<"+n+">k__BackingField",F).SetValue(o,v);
 static object C(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);pass++;Debug.Log("COVERAGE PASS "+label);}
 static void MovePlayer(FirstPersonWalker walker,Transform root,Vector3 local){var p=new GameObject("player test pos").transform;p.SetPositionAndRotation(root.TransformPoint(local),root.rotation);walker.MoveTo(p);UnityEngine.Object.Destroy(p.gameObject);Physics.SyncTransforms();}
 static string Shoot(object weapon,FirstPersonWalker player,Vector3 target){player.ViewCamera.transform.LookAt(target);Physics.SyncTransforms();C(weapon,"Tick",1f);return C(weapon,"Attack").ToString();}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/OldStation.unity");SessionState.SetBool("PatrolHitCoverageChecks",true);EditorApplication.update+=Poll;EditorApplication.isPlaying=true;}
 static void Poll(){if(started||!EditorApplication.isPlaying||UnityEngine.Object.FindObjectOfType<StationPrototype>()==null)return;started=true;SessionState.SetBool("PatrolHitCoverageChecks",false);try{
  var mission=G(UnityEngine.Object.FindObjectOfType<StationPrototype>(),"patrol");var player=UnityEngine.Object.FindObjectOfType<FirstPersonWalker>();var story=new StationStory(3);story.Interact(StationTarget.Alan);story.Interact(StationTarget.ToolBag);story.Interact(StationTarget.Alan);C(mission,"Begin",story);C(mission,"DebugJumpToWave",3);
  var root=((GameObject)G(G(mission,"yard"),"Root")).transform;var weapon=G(mission,"Weapon");B(weapon,"Damage",16);B(weapon,"Loaded",1000);
  Component target=null,front=null;foreach(Component e in (IEnumerable)G(mission,"Enemies")){if((bool)G(e,"Alive")&&G(e,"EnemyKind").ToString()=="Normal"){if(target==null)target=e;else if(front==null)front=e;}e.gameObject.SetActive(false);}if(!target||!front)throw new Exception("normal enemies missing");target.gameObject.SetActive(true);S(target,"health",10000);MovePlayer(player,root,new Vector3(-6.5f,.05f,3));
  var hit=(SphereCollider)G(target,"headHitBox");var ap=target.GetComponentInChildren<AssetAnimationPlayer>();var model=(GameObject)G(ap,"model");var clip=Array.Find(Resources.LoadAll<AnimationClip>("Quaternius/Zombie_Legacy"),x=>x.name=="Zombie|ZombieWalk");
  foreach(float distance in new[]{2f,6f,12f})foreach(float phase in new[]{.0f,.4f,.9f,1.3f}){
   target.transform.position=root.TransformPoint(new Vector3(-6.5f,.05f,3+distance));target.transform.rotation=root.rotation*Quaternion.Euler(0,180,0);clip.SampleAnimation(model,phase);C(target,"SyncHeadHitBox");
   var center=hit.transform.TransformPoint(hit.center);int hp=(int)G(target,"Health");Check(Shoot(weapon,player,center)=="Headshot"&&(int)G(target,"Health")==hp-28,"animated head damage d="+distance+" t="+phase);
   var blood=(GameObject[])G(weapon,"blood");bool atHead=false;foreach(var drop in blood)if(drop.activeSelf&&Vector3.Distance(drop.transform.position,center)<=hit.radius*hit.transform.lossyScale.x+.03f)atHead=true;Check(atHead,"blood at animated head");
  }
  target.transform.position=root.TransformPoint(new Vector3(-6.5f,.05f,9));target.transform.rotation=root.rotation*Quaternion.Euler(0,180,0);C(target,"SyncHeadHitBox");int before=(int)G(target,"Health");
  Check(Shoot(weapon,player,target.transform.TransformPoint(new Vector3(0,1f,0)))=="Hit"&&(int)G(target,"Health")==before-16,"torso receives body damage");
  player.GetComponent<CharacterController>().Move(root.right*.4f);Physics.SyncTransforms();before=(int)G(target,"Health");Check(Shoot(weapon,player,hit.transform.TransformPoint(hit.center))=="Headshot"&&(int)G(target,"Health")==before-28,"headshot after player movement");
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=root.TransformPoint(new Vector3(-6.3f,1.5f,6));wall.transform.rotation=root.rotation;wall.transform.localScale=new Vector3(2f,3f,.25f);Physics.SyncTransforms();before=(int)G(target,"Health");Check(Shoot(weapon,player,hit.transform.TransformPoint(hit.center))=="Miss"&&(int)G(target,"Health")==before,"solid wall blocks headshot");wall.SetActive(false);UnityEngine.Object.Destroy(wall);
  front.gameObject.SetActive(true);S(front,"health",10000);front.transform.position=root.TransformPoint(new Vector3(-6.5f,.05f,6));front.transform.rotation=target.transform.rotation;C(front,"SyncHeadHitBox");target.transform.position=root.TransformPoint(new Vector3(-6.5f,.05f,9));C(target,"SyncHeadHitBox");MovePlayer(player,root,new Vector3(-6.5f,.05f,3));Physics.SyncTransforms();int a=(int)G(front,"Health"),b=(int)G(target,"Health");Shoot(weapon,player,((SphereCollider)G(front,"headHitBox")).transform.position);Check((int)G(front,"Health")<a&&(int)G(target,"Health")==b,"front enemy blocks rear enemy");
  Debug.Log("HIT COVERAGE CHECKS PASSED "+pass);C(mission,"Finish",false);EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
