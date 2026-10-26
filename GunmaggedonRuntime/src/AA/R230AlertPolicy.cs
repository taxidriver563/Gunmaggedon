using System;
using System.Collections.Generic;
namespace GunmageddonAudio
{
    public enum RailgunAlertState : byte { Off, Tracking, Charging, Imminent }
    public static class R230AlertPolicy
    {
        public const double LeaseSeconds = .8, ImminentSeconds = .5;
        public static RailgunAlertState State(bool charging, double progress)
        { return !charging ? RailgunAlertState.Tracking : progress >= 1 - ImminentSeconds / R230Capacitor.ChargeSeconds ? RailgunAlertState.Imminent : RailgunAlertState.Charging; }
        // Map variable-duration charges onto the existing receiver cadence while
        // preserving exactly the final half-second sustained warning.
        public static double WarningProgress(double progress,double duration)
        {
            double split=1-ImminentSeconds/duration,normal=1-ImminentSeconds/R230Capacitor.ChargeSeconds;
            progress=Math.Max(0,Math.Min(1,progress));
            return progress<split?progress/split*normal:normal+(progress-split)/(1-split)*(1-normal);
        }
        public static double PulseInterval(RailgunAlertState state, double progress)
        { return state == RailgunAlertState.Tracking ? 2.4 : .65 + (.24 - .65) * Math.Max(0, Math.Min(1, progress / (1 - ImminentSeconds / R230Capacitor.ChargeSeconds))); }
        public static bool Fresh(double age) {return !double.IsNaN(age)&&!double.IsInfinity(age)&&age>=-1&&age<=LeaseSeconds;}
        public static bool Newer(uint next, uint previous) { return unchecked((int)(next - previous)) > 0; }
    }
    public sealed class R230AlertLedger
    {
        struct Item { internal int Target; internal RailgunAlertState State; internal double Progress, Until; internal uint Sequence; }
        readonly Dictionary<int, Item> items = new Dictionary<int, Item>();
        readonly List<int> expired = new List<int>();
        public bool Receive(int source, int target, RailgunAlertState state, double progress, uint sequence, double now)
        {
            if(source == 0 || (byte)state > 3 || double.IsNaN(progress) || double.IsInfinity(progress) || progress < 0 || progress > 1 || state != RailgunAlertState.Off && target == 0) return false;
            Item old; if(items.TryGetValue(source, out old) && !R230AlertPolicy.Newer(sequence, old.Sequence)) return false;
            items[source] = new Item { Target = target, State = state, Progress = progress, Sequence = sequence, Until = now + (state == RailgunAlertState.Off ? 2 : R230AlertPolicy.LeaseSeconds) };
            return true;
        }
        public RailgunAlertState Select(int target, double now, out double progress, out int count)
        {
            var highest = RailgunAlertState.Off; progress = 0; count = 0;
            foreach(var item in items.Values) if(item.Target == target && item.Until >= now && item.State != RailgunAlertState.Off) {
                count++;
                if(item.State > highest) { highest = item.State; progress = item.Progress; }
                else if(item.State == highest) progress = Math.Max(progress, item.Progress);
            }
            return highest;
        }
        public void Prune(double now)
        { expired.Clear(); foreach(var pair in items) if(pair.Value.Until < now) expired.Add(pair.Key); foreach(int key in expired) items.Remove(key); expired.Clear(); }
        public void Clear() { items.Clear(); expired.Clear(); }
    }
}
