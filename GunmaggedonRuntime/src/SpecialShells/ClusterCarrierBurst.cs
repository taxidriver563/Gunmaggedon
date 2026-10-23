using UnityEngine;

namespace GunmageddonAudio
{
    // A small, cosmetic opening charge. The clusterlets alone handle damage.
    public sealed class ClusterCarrierBurst : MonoBehaviour
    {
        static readonly int[] Counts={8,20,9};
        private float born;
        private Light flash;
        ParticleSystem[] systems;ParticleSystemRenderer[] renderers;internal long Lease;

        internal void Initialize(GlobalPosition position)
        {
            born = Time.time;
            transform.position = position.ToLocalPosition();
            if(!flash) {
            Burst("Opening flash", SpecialShells.SparkMaterial, 8, .2f, 7f, 3.5f, new Color(5f, 2.4f, .7f, 1f));
            Burst("Casing sparks", SpecialShells.SparkMaterial, 20, .55f, 18f, .35f, new Color(3f, 1.7f, .6f, 1f));
            Burst("Opening smoke", SpecialShells.SmokeMaterial, 9, 1.6f, 3f, 3f, new Color(.5f, .48f, .43f, .55f));
            flash = gameObject.AddComponent<Light>();
            flash.type = LightType.Point; flash.color = new Color(1f, .65f, .3f);
            flash.range = 24f; flash.intensity = 1400f; flash.shadows = LightShadows.None;
            systems=GetComponentsInChildren<ParticleSystem>(true);
            renderers=new ParticleSystemRenderer[systems.Length];for(int i=0;i<systems.Length;i++)renderers[i]=systems[i].GetComponent<ParticleSystemRenderer>();
            }
            ResetEffect();flash.intensity=1400;
            var counts=Counts;for(int i=0;i<systems.Length;i++){renderers[i].sharedMaterial=i==2?SpecialShells.SmokeMaterial:SpecialShells.SparkMaterial;systems[i].Play();systems[i].Emit(counts[i]);}
        }

        private void Burst(string label, Material material, int count, float lifetime, float speed, float size, Color tint)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .65f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * .65f, size);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = tint; main.gravityModifier = label == "Casing sparks" ? .7f : 0f;
            var emission = ps.emission; emission.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .6f;
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;

        }

        internal void ResetEffect(){if(systems!=null)foreach(var ps in systems)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(flash)flash.intensity=0;}
        internal bool Advance(float now){float age=now-born;if(flash)flash.intensity=1400f*Mathf.Clamp01(1-age/.18f);return age>=2;}
    }
}
