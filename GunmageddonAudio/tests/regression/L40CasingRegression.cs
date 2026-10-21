using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class L40CasingRegression
{
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 public static object Run()
 {
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/Mods/Gunmageddon/L40_AA_Trailer.prefab");
  var gun=prefab.GetComponentInChildren<Gun>(true);var s=new SerializedObject(gun);
  var port=s.FindProperty("ejectionTransform").objectReferenceValue as Transform;
  var casing=s.FindProperty("ejectionPrefab").objectReferenceValue as GameObject;
  Check((int)ForceMode.VelocityChange==2,"Native IL mode 2 must be tested as VelocityChange");
  var native=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blueprinter/_donotship/GameObject/Shell_76mm_casing_PLACEHOLDER.prefab");
  Check(port&&casing&&casing.GetComponent<Rigidbody>()&&casing.GetComponent<Collider>(),"Native ejection has a port and physical casing");
  Check(casing.GetComponent<MeshFilter>().sharedMesh==native.GetComponent<MeshFilter>().sharedMesh,"Uses stock casing mesh");
  Check(casing.GetComponent<MeshRenderer>().sharedMaterial==native.GetComponent<MeshRenderer>().sharedMaterial,"Uses stock brass material");
  Check(casing.transform.localScale==Vector3.one*.45f,"Stock model scaled for 40mm");
  Check(port.parent==gun.transform&&!port.IsChildOf(gun.transform.Find("RecoilPivot")),"Rear port follows aim without moving with barrel recoil");
  Check(port.position.z<-1.5f&&Mathf.Abs(port.position.y-1.5f)<.001f&&Vector3.Dot(port.forward,-gun.transform.forward)>.99f,"Port lies at rear opening and faces backward");
  Check(s.FindProperty("ejectionDelay").floatValue>0&&s.FindProperty("ejectionDelay").floatValue<.5f,"Cycle delay configured");
  Check(s.FindProperty("ejectionLifetime").floatValue==5f,"Native five-second cleanup configured");
  var scene=EditorSceneManager.NewPreviewScene();var physics=scene.GetPhysicsScene();Check(!physics.Equals(Physics.defaultPhysicsScene),"Separate fixture physics");
  bool auto=Physics.autoSimulation;Physics.autoSimulation=false;
  try {
   var stage=new GameObject("Casing physics fixture");SceneManager.MoveGameObjectToScene(stage,scene);
   var casingObject=UnityEngine.Object.Instantiate(casing,stage.transform,false);var rb=casingObject.GetComponent<Rigidbody>();
   casingObject.transform.SetPositionAndRotation(new Vector3(0,3,0),port.rotation);Physics.SyncTransforms();
   // Same native ejection operations and ForceMode as Gun.SpawnEjection.
   rb.velocity=new Vector3(1,0,0);var inherited=rb.velocity;
   rb.AddRelativeForce(s.FindProperty("ejectionLinearVelocity").vector3Value,ForceMode.VelocityChange);
   rb.AddRelativeTorque(s.FindProperty("ejectionRotationalVelocity").vector3Value,ForceMode.VelocityChange);
   physics.Simulate(.02f);
   float rearSpeed=Vector3.Dot(rb.velocity-inherited,port.forward);
   Check(rearSpeed>.3f&&rearSpeed<.8f,"Casing ejects gently backward");
   Check(rb.angularVelocity.magnitude>.1f&&rb.angularVelocity.magnitude<1f,"Case tumbles slowly");
   for(int i=0;i<35;i++)physics.Simulate(.02f);
   Check(rb.position.z<-.2f&&rb.position.z>-.6f&&rb.position.y<2,"Case travels out behind port then falls under gravity");
   Check(rb.velocity.x>.3f,"Native carrier velocity inheritance retained");
   var inactive=new GameObject("Inactive aiming fixture");inactive.SetActive(false);inactive.transform.SetParent(stage.transform,false);
   var turret=UnityEngine.Object.Instantiate(prefab,inactive.transform,false);var movingPort=turret.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CasingEject");
   var initial=movingPort.position;turret.transform.Find("YawPivot").localRotation=Quaternion.Euler(0,90,0);
   Check(Vector3.Distance(initial,movingPort.position)>1,"Ejection port follows turret traverse");
   initial=movingPort.position;turret.GetComponentInChildren<Gun>(true).transform.localRotation=Quaternion.Euler(-30,0,0);
   Check(Vector3.Distance(initial,movingPort.position)>.3f,"Ejection port follows elevation");
   return new{passed=true,rearSpeedMetresPerSecond=rearSpeed,mesh="stock shell76mm_casing",scaledLength=casing.GetComponent<MeshFilter>().sharedMesh.bounds.size.z*.45f,port=port.position,lifetime=5,physics="backward ejection, inherited velocity, tumble and gravity"};
  }finally{Physics.autoSimulation=auto;EditorSceneManager.ClosePreviewScene(scene);}
 }
}
