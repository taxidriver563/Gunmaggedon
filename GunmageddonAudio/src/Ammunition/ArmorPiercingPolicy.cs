using System;

namespace GunmageddonAudio
{
    public enum CoastalAmmoMode { Auto, HE, AP }

    internal static class ArmorPiercingPolicy
    {
        internal static bool UseAP(CoastalAmmoMode mode, bool ordinaryGun, bool ship, float length, float armorTier)
        {
            if (!ordinaryGun || mode == CoastalAmmoMode.HE) return false;
            if (mode == CoastalAmmoMode.AP) return true;
            return ship && (length >= 65f || armorTier >= 3);
        }

        internal static float NativePenetration(float launchDamage, float referenceSpeed, float launchSpeed)
        {
            if (launchDamage <= 0f || referenceSpeed <= 0f || launchSpeed <= 0f ||
                float.IsNaN(launchDamage) || float.IsNaN(referenceSpeed) || float.IsNaN(launchSpeed) ||
                float.IsInfinity(launchDamage) || float.IsInfinity(referenceSpeed) || float.IsInfinity(launchSpeed)) return 0f;
            return (float)(launchDamage * ((double)referenceSpeed * referenceSpeed) / ((double)launchSpeed * launchSpeed));
        }
    }
}
