using UnityEngine;

namespace GunmageddonAudio
{
    // Cache once per controller. Unity chooses LOD using the actual rendering
    // cameras (including zoom), rather than a second, conflicting distance rule.
    internal sealed class ModelDetail
    {
        readonly LODGroup group;
        readonly Renderer[] near;
        internal ModelDetail(Component owner)
        {
            group=owner.GetComponentInParent<LODGroup>();
            if(group){var levels=group.GetLODs();if(levels.Length>1)near=levels[0].renderers;}
        }
        internal bool Animate {get{return ShouldAnimate(Application.isPlaying);}}
        internal bool ShouldAnimate(bool playing)
        {
            // Keep editor previews and older packs functional. No camera on a
            // dedicated server naturally means there is no cosmetic work to do.
            if(!playing||!group||!group.enabled||near==null)return true;
            foreach(var renderer in near)if(renderer&&renderer.isVisible)return true;
            return false;
        }
    }
}
