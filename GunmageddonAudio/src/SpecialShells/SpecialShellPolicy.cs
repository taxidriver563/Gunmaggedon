namespace GunmageddonAudio
{
    public enum SpecialShellMode { Auto, Cluster, Smoke, Chaff, TopAttack }
    internal enum SpecialShellKind { None, Cluster, Smoke, Chaff, TopAttack }

    internal static class SpecialShellPolicy
    {
        internal static SpecialShellKind Choose(SpecialShellMode selection, float now,
            float lastSmoke, float lastChaff, float smokeLife, float chaffLife,
            int clustersSinceSupport, int minimumClusters)
        {
            if (selection != SpecialShellMode.Auto) return (SpecialShellKind)(int)selection;
            // Open with clusters and reserve at least this many rounds between
            // support shells, even when both obscurants have expired.
            if (clustersSinceSupport < minimumClusters) return SpecialShellKind.Cluster;
            float chaffOverdue = now - lastChaff - chaffLife - 10f;
            float smokeOverdue = now - lastSmoke - smokeLife - 10f;
            if (chaffOverdue >= 0f && chaffOverdue >= smokeOverdue) return SpecialShellKind.Chaff;
            if (smokeOverdue >= 0f) return SpecialShellKind.Smoke;
            return SpecialShellKind.Cluster;
        }
    }
}
