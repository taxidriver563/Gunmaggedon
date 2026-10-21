using UnityEngine;

namespace GunmageddonAudio
{
    // Cosmetic only: the empty canopy owns its material after separation.
    public sealed class DiscardedTopAttackParachute : MonoBehaviour
    {
        private Transform canopy, lines;
        private Material material;
        private GlobalPosition position;
        private Vector3 velocity, canopyScale, lineScale;
        private float age;

        internal void Initialize(Transform top, Transform cords, Material ownedMaterial, Vector3 initialVelocity)
        {
            canopy = top; lines = cords; material = ownedMaterial;
            canopyScale = canopy.localScale; lineScale = lines.localScale;
            velocity = initialVelocity; position = transform.position.ToGlobalPosition();
        }

        private void Update()
        {
            float dt = Time.deltaTime; age += dt;
            if (age >= 8f || transform.position.y <= Datum.LocalSeaY + .1f)
            { if (Application.isPlaying) Destroy(gameObject); else gameObject.SetActive(false); return; }
            Vector3 wind = LevelInfo.i ? LevelInfo.i.NetworkwindVelocity * .35f : Vector3.zero;
            velocity = Vector3.Lerp(velocity, wind + Vector3.down * 2.5f, 1f - Mathf.Exp(-dt * 2f));
            position = (position.ToLocalPosition() + velocity * dt).ToGlobalPosition();
            transform.position = position.ToLocalPosition();
            float collapse = Mathf.SmoothStep(0f, 1f, age / 2.5f);
            canopy.localScale = Vector3.Scale(canopyScale, new Vector3(Mathf.Lerp(1f, .35f, collapse),
                Mathf.Lerp(1f, .45f, collapse), Mathf.Lerp(1f, 1.4f, collapse)));
            lines.localScale = Vector3.Scale(lineScale, new Vector3(Mathf.Lerp(1f, .35f, collapse),
                Mathf.Lerp(1f, .45f, collapse), 1f));
        }

        private void OnDestroy()
        {
            if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
