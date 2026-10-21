using UnityEngine;

namespace GunmageddonAudio
{
    // A small, cosmetic opening charge. The clusterlets alone handle damage.
    public sealed class ClusterCarrierBurst : MonoBehaviour
    {
        private float born;
        private Light flash;

        internal void Initialize(GlobalPosition position)
        {
            born = Time.time;
            transform.position = position.ToLocalPosition();
            Burst("Opening flash", SpecialShells.SparkMaterial, 8, .2f, 7f, 3.5f, new Color(5f, 2.4f, .7f, 1f));
            Burst("Casing sparks", SpecialShells.SparkMaterial, 20, .55f, 18f, .35f, new Color(3f, 1.7f, .6f, 1f));
            Burst("Opening smoke", SpecialShells.SmokeMaterial, 9, 1.6f, 3f, 3f, new Color(.5f, .48f, .43f, .55f));
            flash = gameObject.AddComponent<Light>();
            flash.type = LightType.Point; flash.color = new Color(1f, .65f, .3f);
            flash.range = 24f; flash.intensity = 1400f; flash.shadows = LightShadows.None;
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
            ps.Play(); ps.Emit(count);
        }

        private void Update()
        {
            float age = Time.time - born;
            flash.intensity = 1400f * Mathf.Clamp01(1f - age / .18f);
            if (age >= 2f) Destroy(gameObject);
        }
    }
}
