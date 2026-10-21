using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class L40RigPhysicsRegression
{
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 public static object Run()
 {
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/L40_AA_Trailer.prefab");
  var scene=EditorSceneManager.NewPreviewScene();var physics=scene.GetPhysicsScene();
  Check(!physics.Equals(Physics.defaultPhysicsScene),"Fixture must not simulate the user's scene");
  bool auto=Physics.autoSimulation;Physics.autoSimulation=false;
  try {
   var staging=new GameObject("Inactive rig fixture");staging.SetActive(false);SceneManager.MoveGameObjectToScene(staging,scene);
   var model=UnityEngine.Object.Instantiate(prefab,staging.transform,false);
   var yaw=model.transform.Find("YawPivot");var pitch=yaw.Find("PitchPivot");var barrel=pitch.Find("RecoilPivot/barrel")??pitch.Find("barrel");
   Check(barrel&&pitch.Find("gun")&&yaw.Find("traverse"),"Saved prefab retains visible meshes under native pivots");
   Check(yaw.Find("PitchCrankPivot/elevationcrank")&&yaw.Find("YawCrankPivot/yawcrank"),"Saved crank hierarchy");
   var chassis=model.transform.Find("Model/base");var initialBase=chassis.position;var initialRot=chassis.rotation;
   var initialBarrel=barrel.position;yaw.localRotation=Quaternion.Euler(0,90,0);
   Check(Vector3.Distance(initialBarrel,barrel.position)>1,"Yaw actually traverses the visible barrel");
   initialBarrel=barrel.position;pitch.localRotation=Quaternion.Euler(-30,0,0);
   Check(Vector3.Distance(initialBarrel,barrel.position)>.5f,"Pitch actually elevates visible barrel");
   Check(Vector3.Distance(chassis.position,initialBase)<.001f&&Quaternion.Angle(chassis.rotation,initialRot)<.001f,"Chassis stays fixed while aiming");
   var v=prefab.GetComponent<GroundVehicle>();var def=(VehicleDefinition)v.definition;var so=new SerializedObject(v);
   float k=so.FindProperty("springRate").floatValue,d=so.FindProperty("dampingRate").floatValue,travel=so.FindProperty("suspensionTravel").floatValue;
   float preload=k*(travel-def.spawnOffset.y),weight=def.mass*9.81f;
   Check(Mathf.Abs(preload-weight)<10,"Native root spring preload must balance trailer weight at spawn");
   var ground=new GameObject("Flat spawn fixture ground");SceneManager.MoveGameObjectToScene(ground,scene);
   ground.transform.position=new Vector3(0,-.5f,0);ground.AddComponent<BoxCollider>().size=new Vector3(100,1,100);
   float worstRise=0,worstSpeed=0;
   foreach(float offset in new[]{0f,.05f,-.015f}) {
    var bodyObject=new GameObject("Sterile native-collider trailer fixture");SceneManager.MoveGameObjectToScene(bodyObject,scene);
    var rb=bodyObject.AddComponent<Rigidbody>();rb.mass=def.mass;rb.useGravity=true;rb.angularDrag=.5f;
    // Copy actual saved colliders, without activating native network/mission scripts.
    foreach(var source in prefab.GetComponentsInChildren<Collider>(true)) {
     var g=new GameObject(source.name);g.transform.SetParent(bodyObject.transform,false);
     g.transform.localPosition=prefab.transform.InverseTransformPoint(source.transform.position);
     g.transform.localRotation=Quaternion.Inverse(prefab.transform.rotation)*source.transform.rotation;
     var c=(Collider)g.AddComponent(source.GetType());EditorUtility.CopySerialized(source,c);
    }
    bodyObject.transform.position=new Vector3(0,def.spawnOffset.y+offset,0);Physics.SyncTransforms();
    float start=rb.position.y,maxY=start,maxSpeed=0;
    for(int i=0;i<300;i++) {
     // Native GroundVehicleJob_Math2.SuspensionPhysics uses one root spring,
     // damping downward motion only. Exercise that force law with real PhysX
     // gravity and the actual saved contact colliders, not the towing fixture.
     float spring=k*Mathf.Max(travel-rb.position.y,0);
     float damper=spring>0?d*Mathf.Max(-rb.velocity.y,0):0;
     rb.AddForce(Vector3.up*(spring+damper),ForceMode.Force);
     physics.Simulate(.02f);maxY=Mathf.Max(maxY,rb.position.y);maxSpeed=Mathf.Max(maxSpeed,Mathf.Abs(rb.velocity.y));
    }
    Check(maxY-start<.15f,"Trailer launched upward at spawn: "+(maxY-start));
    Check(Mathf.Abs(rb.position.y-def.spawnOffset.y)<.12f,"Trailer failed to settle at ride height: "+rb.position.y);
    worstRise=Mathf.Max(worstRise,maxY-start);worstSpeed=Mathf.Max(worstSpeed,maxSpeed);
    UnityEngine.Object.DestroyImmediate(bodyObject);
   }
   return new{passed=true,yaw="visible meshes move",pitch="visible barrel elevates",preloadNewtons=preload,weightNewtons=weight,worstRiseMetres=worstRise,worstVerticalSpeed=worstSpeed};
  }finally{Physics.autoSimulation=auto;EditorSceneManager.ClosePreviewScene(scene);}
 }
}
