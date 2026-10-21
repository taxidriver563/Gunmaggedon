Shader "Gunmaggedon/Searchlight Beam"
{
 Properties
 {
  [HDR] _BeamColor("Beam color",Color)=(1,.94,.82,1)
  _Intensity("Brightness",Range(0,8))=1.8
  _Opacity("Scattering opacity",Range(0,1))=.065
  _Length("Mesh maximum length (m)",Float)=13000
  _BeamRange("Beam reach (m)",Range(8000,13000))=10000
  _VisibleLength("Unobstructed length (m)",Float)=10000
  _StartRadius("Aperture radius (m)",Float)=.58
  _Slope("Radius growth",Float)=.0192
  _DistanceFade("Observer fade distance (m)",Float)=20000
  [ToggleUI] _UseSceneDepth("Clip scattering at opaque geometry",Float)=1
  [HideInInspector] _CloudMap("Native cloud density",2D)="black"{}
  [HideInInspector] _CloudData("Cloud height, half thickness, extinction, enabled",Vector)=(0,0,0,0)
  [HideInInspector] _CloudUV("Cloud UV origin and scale",Vector)=(0,0,0,0)
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Pass
  {
   Name "SearchlightCone"
   Tags {"LightMode"="SRPDefaultUnlit"}
   Blend SrcAlpha One
   ZWrite Off
   ZTest LEqual
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.0
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _BeamColor;
    float4 _CloudData,_CloudUV;
    float _Intensity,_Opacity,_Length,_BeamRange,_VisibleLength,_DistanceFade,_StartRadius,_Slope,_UseSceneDepth;
   CBUFFER_END
   TEXTURE2D(_CloudMap);SAMPLER(sampler_CloudMap);
   struct Attributes {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float3 positionOS:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vert(Attributes v)
   {
    Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float fraction=saturate(_VisibleLength/max(1,_Length));float3 p=v.positionOS.xyz;
    p.xy*=(_StartRadius+_Slope*p.z*fraction)/max(.001,_StartRadius+_Slope*p.z);p.z*=fraction;
    o.positionOS=p;o.positionCS=TransformObjectToHClip(p);return o;
   }
   // Analytic ray/cone interval: no raymarch loop, sampling steps or camera-facing cards.
   bool ConeInterval(float3 o,float3 d,out float enter,out float leave)
   {
    enter=0;leave=1e8;
    if(abs(d.z)<1e-6){if(o.z<0||o.z>_VisibleLength)return false;}
    else {float z0=-o.z/d.z,z1=(_VisibleLength-o.z)/d.z;enter=max(0,min(z0,z1));leave=max(z0,z1);}
    if(leave<=enter)return false;
    float r=_StartRadius+_Slope*o.z;
    float a=dot(d.xy,d.xy)-_Slope*_Slope*d.z*d.z;
    float b=2*(dot(o.xy,d.xy)-r*_Slope*d.z);
    float c=dot(o.xy,o.xy)-r*r;
    if(abs(a)<1e-7){if(abs(b)<1e-7)return c<=0;if(b>0)leave=min(leave,-c/b);else enter=max(enter,-c/b);}
    else {
     float discriminant=b*b-4*a*c;if(discriminant<0)return a<0;
     float q=-.5*(b+(b>=0?1:-1)*sqrt(discriminant));
     float t0=q/a,t1=abs(q)>1e-8?c/q:t0;
     float low=min(t0,t1),high=max(t0,t1);
     if(a>0){enter=max(enter,low);leave=min(leave,high);}
     else {
      float atEnter=(a*enter+b)*enter+c,atLeave=(a*leave+b)*leave+c;
      if(atEnter>0&&atLeave>0)return false;
      if(atEnter>0)enter=max(enter,high);else if(atLeave>0)leave=min(leave,low);
     }
    }
    return leave>enter;
   }
   float CloudTransmission(float3 source,float3 samplePosition)
   {
    if(_CloudData.w<.5)return 1;
    float3 ray=samplePosition-source;
    float lower=_CloudData.x-_CloudData.y,upper=_CloudData.x+_CloudData.y;
    float start=0,end=1;
    if(abs(ray.y)<.001) {if(lower>0||upper<0)return 1;}
    else {
     float a=lower/ray.y,b=upper/ray.y;
     start=max(0,min(a,b));end=min(1,max(a,b));
    }
    if(end<=start)return 1;
    // Sample the first cloud intersection. Keep that obstruction for every
    // point beyond it instead of revealing a faint tail above the layer.
    float2 entryOffset=ray.xz*start;
    float density=SAMPLE_TEXTURE2D(_CloudMap,sampler_CloudMap,frac(_CloudUV.xy+entryOffset*_CloudUV.zw)).r;
    return density>.15?0:1;
   }
   half4 Frag(Varyings i,half facing:VFACE):SV_Target
   {
    UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 cameraOS=TransformWorldToObject(GetCameraPositionWS());
    float cameraRadius=_StartRadius+_Slope*cameraOS.z;
    bool inside=cameraOS.z>=0&&cameraOS.z<=_VisibleLength&&dot(cameraOS.xy,cameraOS.xy)<cameraRadius*cameraRadius;
    // Render only the near wall outside, and the inner wall inside. A double-sided
    // proxy must not add the same scattering twice through its front/back surfaces.
    if(inside?(facing>0):(facing<0))discard;
    float3 direction=normalize(i.positionOS-cameraOS);float enter,leave;
    if(!ConeInterval(cameraOS,direction,enter,leave))discard;
    if(_UseSceneDepth>.5) {
     float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
     #if UNITY_REVERSED_Z
      bool hasDepth=raw>1e-6;
     #else
      bool hasDepth=raw<.999999;
     #endif
     if(hasDepth) {
      float3 worldDirection=normalize(TransformObjectToWorldDir(direction));
      float eyeRate=max(.001,-TransformWorldToViewDir(worldDirection).z);
      float worldPerLocal=length(mul((float3x3)unity_ObjectToWorld,direction));
      leave=min(leave,LinearEyeDepth(raw,_ZBufferParams)/(eyeRate*max(.001,worldPerLocal)));
     }
    }
    if(leave<=enter)discard;
    float middleZ=(cameraOS+direction*((enter+leave)*.5)).z;
    float radius=max(.1,_StartRadius+_Slope*middleZ);
    float chord=(leave-enter)/(2*radius);
    float longitudinal=pow(saturate(1-middleZ/max(1,_VisibleLength)),.8);
    float alpha=_Opacity*(1-exp(-min(chord,8)))*longitudinal;
    if(inside)alpha*=.25;
    float3 source=TransformObjectToWorld(float3(0,0,0));
    float3 middleWS=TransformObjectToWorld(cameraOS+direction*((enter+leave)*.5));
    clip(CloudTransmission(source,middleWS)-.5);
    alpha*=1-smoothstep(_DistanceFade*.7,_DistanceFade,distance(GetCameraPositionWS(),source));
    return half4(_BeamColor.rgb*_Intensity,alpha);
   }
   ENDHLSL
  }
 }
}
