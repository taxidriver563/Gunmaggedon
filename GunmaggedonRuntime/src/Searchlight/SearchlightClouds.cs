using HarmonyLib;
using UnityEngine;

namespace GunmageddonAudio
{
    // Borrow the game's weather sampler; no texture copies or cloud particles
    // are generated. All positions are relative to the lamp/global cloud map,
    // so camera changes and floating-origin shifts cannot move the fade plane.
    internal static class SearchlightClouds
    {
        static readonly System.Reflection.FieldInfo Layer=AccessTools.Field(typeof(LevelInfo),"cloudLayer");
        static readonly System.Reflection.FieldInfo Weather=AccessTools.Field(typeof(CloudLayer),"currentWeatherSet");
        static readonly System.Reflection.FieldInfo Map=AccessTools.Field(Weather.FieldType,"particleSampler");
        static readonly System.Reflection.FieldInfo Coverage=AccessTools.Field(Weather.FieldType,"coverage");
        static readonly System.Reflection.FieldInfo Scale=AccessTools.Field(typeof(CloudLayer),"densityMapScale");
        static readonly System.Reflection.FieldInfo Thickness=AccessTools.Field(typeof(CloudLayer),"layerThickness");
        static readonly System.Reflection.FieldInfo Wind=AccessTools.Field(typeof(CloudLayer),"windDisplacement");
        static readonly int MapId=Shader.PropertyToID("_CloudMap"),DataId=Shader.PropertyToID("_CloudData"),UvId=Shader.PropertyToID("_CloudUV");

        internal static void Apply(MaterialPropertyBlock properties,LevelInfo level,Transform source)
        {
            properties.SetVector(DataId,Vector4.zero);
            var layer=level?Layer.GetValue(level) as CloudLayer:null;
            if(!layer||!layer.isActiveAndEnabled||!source)return;
            var weather=Weather.GetValue(layer) as Object;
            if(!weather||(float)Coverage.GetValue(weather)<=.001f)return;
            var map=Map.GetValue(weather) as Texture2D;
            float scale=(float)Scale.GetValue(layer);
            if(!map||scale<=0f)return;
            var wind=(Vector2)Wind.GetValue(layer);
            var global=source.position.ToGlobalPosition();
            float halfThickness=Mathf.Max(150f,Mathf.Abs((float)Thickness.GetValue(layer)));
            properties.SetTexture(MapId,map);
            properties.SetVector(UvId,new Vector4((global.x-wind.x)/(scale*map.width),
                (global.z-wind.y)/(scale*map.height),1f/(scale*map.width),1f/(scale*map.height)));
            properties.SetVector(DataId,new Vector4(Datum.LocalSeaY+level.NetworkcloudHeight-source.position.y,
                halfThickness,.006f,1f));
        }
    }
}
