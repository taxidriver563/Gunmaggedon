Shader "Gunmaggedon/Searchlight Dazzler"
{
 Properties
 {
  [HDR] _GlareColor("Glare color",Color)=(1,.95,.83,1)
  _Intensity("Dazzle intensity",Range(0,12))=4
  _Size("On-axis radius (screen heights)",Range(0,2))=.7
  _ApertureRadius("Source mesh radius",Float)=.58
  _InnerAngle("Full dazzle angle (degrees)",Float)=1
  _OuterAngle("Off-axis fade angle (degrees)",Float)=8
  _DistanceFade("Observer fade distance (m)",Float)=20000
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Pass
  {
   Name "LensDazzler"
   Tags {"LightMode"="SRPDefaultUnlit"}
   Blend One One
   ZWrite Off
   ZTest Always
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.0
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
    float4 _GlareColor;
    float _Intensity,_Size,_ApertureRadius,_InnerAngle,_OuterAngle,_DistanceFade;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 sourceScreen:TEXCOORD1;float sourceEye:TEXCOORD2;float gain:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO};
   Varyings Vert(Attributes v)
   {
    Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 source=TransformObjectToWorld(float3(0,0,0));float3 delta=GetCameraPositionWS()-source;float distanceToCamera=length(delta);
    float3 toCamera=delta/max(.001,distanceToCamera);
    float alignment=dot(normalize(TransformObjectToWorldDir(float3(0,0,1))),toCamera);
    float beam=smoothstep(cos(radians(_OuterAngle)),cos(radians(_InnerAngle)),alignment);
    float look=dot(-UNITY_MATRIX_V[2].xyz,-toCamera);
    float looking=smoothstep(cos(radians(25)),cos(radians(5)),look);
    float fade=1-smoothstep(_DistanceFade*.7,_DistanceFade,distanceToCamera);
    float nearFade=smoothstep(.15,1,distanceToCamera);
    o.gain=beam*looking*fade*nearFade;
    float4 center=TransformWorldToHClip(source);o.sourceScreen=ComputeScreenPos(center);o.sourceEye=-TransformWorldToView(source).z;
    o.uv=v.positionOS.xy/max(.001,_ApertureRadius);
    // Screen-size halo: a distant lamp can still overwhelm the view on axis.
    // Uses the lens renderer, so its ordinary visibility/night gate controls glare too.
    float radius=_Size*lerp(.45,1,beam)*lerp(.65,1,rsqrt(1+distanceToCamera/2500));
    center.xy+=o.uv*float2(_ScaledScreenParams.y/_ScaledScreenParams.x,1)*(2*radius)*center.w;
    o.positionCS=center;return o;
   }
   half4 Frag(Varyings i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    if(i.gain<.0001||i.sourceEye<=0)discard;
    float2 sourceUV=i.sourceScreen.xy/i.sourceScreen.w;
    if(any(sourceUV<0)||any(sourceUV>1))discard;
    // Test the emitter, not each halo pixel, to prevent a flare shining around
    // the silhouette of an opaque object that hides the actual lens.
    float raw=SampleSceneDepth(sourceUV);
    #if UNITY_REVERSED_Z
     bool depthValid=raw>1e-6;
    #else
     bool depthValid=raw<.999999;
    #endif
    if(depthValid&&LinearEyeDepth(raw,_ZBufferParams)<i.sourceEye-max(.1,i.sourceEye*.00005))discard;
    float r=length(i.uv),r2=r*r;
    float core=12*exp(-r2*1500);
    float bloom=3*exp(-r2*30);
    float halo=.8*pow(saturate(1-r),3);
    float horizontal=.6*exp(-abs(i.uv.y)*85-abs(i.uv.x)*3);
    float vertical=.15*exp(-abs(i.uv.x)*180-abs(i.uv.y)*5);
    float brightness=(core+bloom+halo+horizontal+vertical)*_Intensity*i.gain*i.gain;
    brightness*=1-smoothstep(.8,1,r);
    return half4(_GlareColor.rgb*brightness,0);
   }
   ENDHLSL
  }
 }
}
