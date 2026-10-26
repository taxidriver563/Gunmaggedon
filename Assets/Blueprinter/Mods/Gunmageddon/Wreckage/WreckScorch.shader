Shader "Gunmaggedon/WreckScorch"
{
 Properties
 {
  [MainTexture] _BaseMap("Original paint",2D)="white"{}
  [MainColor] _BaseColor("Burn tint",Color)=(.32,.32,.32,1)
  [Normal] _BumpMap("Original normals",2D)="bump"{}
  _BumpScale("Normal strength",Float)=1
  _SootMap("Shared soot layer",2D)="gray"{}
  _Metallic("Metallic",Range(0,1))=.05
  _Smoothness("Smoothness",Range(0,1))=.07
  [HideInInspector] _EmissionColor("Emission disabled",Color)=(0,0,0,0)
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
 }
 SubShader
 {
  Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST, _SootMap_ST, _BaseColor;
   half _BumpScale, _Metallic, _Smoothness;
  CBUFFER_END
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
  TEXTURE2D(_SootMap); SAMPLER(sampler_SootMap);
  struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;};
  struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;float4 shadowCoord:TEXCOORD5;};
  Varyings Vert(Attributes input){Varyings o=(Varyings)0;VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(input.normalOS,input.tangentOS);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;o.tangentWS=half4(n.tangentWS,input.tangentOS.w*GetOddNegativeScale());o.uv=input.uv;o.fog=ComputeFogFactor(p.positionCS.z);o.shadowCoord=GetShadowCoord(p);return o;}
  half4 Frag(Varyings input):SV_Target
  {
   half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,TRANSFORM_TEX(input.uv,_BaseMap)),_BumpScale);
   half3 tangent=normalize(input.tangentWS.xyz),normal=normalize(input.normalWS),bitangent=cross(normal,tangent)*input.tangentWS.w;
   normal=NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,half3x3(tangent,bitangent,normal)));
   InputData data=(InputData)0;data.positionWS=input.positionWS;data.normalWS=normal;data.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);data.shadowCoord=input.shadowCoord;data.fogCoord=input.fog;data.vertexLighting=VertexLighting(input.positionWS,normal);data.bakedGI=SampleSH(normal);data.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);data.shadowMask=half4(1,1,1,1);
   SurfaceData surface=(SurfaceData)0;half3 paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,TRANSFORM_TEX(input.uv,_BaseMap)).rgb;half3 soot=SAMPLE_TEXTURE2D(_SootMap,sampler_SootMap,TRANSFORM_TEX(input.uv,_SootMap)).rgb;surface.albedo=paint*_BaseColor.rgb*saturate(soot*2);surface.metallic=_Metallic;surface.specular=half3(.04,.04,.04);surface.smoothness=_Smoothness;surface.normalTS=normalTS;surface.occlusion=1;surface.alpha=1;
   half4 color=UniversalFragmentPBR(data,surface);color.rgb=MixFog(color.rgb,input.fog);return color;
  }
  ENDHLSL
  Pass
  {
   Name "ForwardLit" Tags{"LightMode"="UniversalForwardOnly"} Cull[_Cull] ZWrite On
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile _ _FORWARD_PLUS
   #pragma multi_compile_fog
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} Cull[_Cull] ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex ShadowVert
   #pragma fragment DepthFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   float3 _LightDirection, _LightPosition;
   float4 ShadowVert(Attributes input):SV_POSITION {float3 p=TransformObjectToWorld(input.positionOS.xyz);float3 n=TransformObjectToWorldNormal(input.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
     float3 direction=normalize(_LightPosition-p);
    #else
     float3 direction=_LightDirection;
    #endif
    float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
    #if UNITY_REVERSED_Z
     clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
    #else
     clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
    #endif
    return clip;}
   half4 DepthFrag():SV_Target{return 0;}
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags{"LightMode"="DepthOnly"} Cull[_Cull] ZWrite On ColorMask 0
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex Vert
   #pragma fragment DepthFrag
   half4 DepthFrag():SV_Target{return 0;}
   ENDHLSL
  }
 }
 FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
