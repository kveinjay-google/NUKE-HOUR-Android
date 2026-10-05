using System;
namespace OpenRA
{
    public sealed class MapRulesetCache
    {
        readonly object sync = new();
        object snapshot;
        Ruleset rules;
        bool loaded;
        public Ruleset Get(object revision, Func<Ruleset> load)
        {
            lock (sync)
            {
                if (loaded && ReferenceEquals(snapshot, revision))
                    return rules;
                var result = load();
                snapshot = revision;
                rules = result;
                loaded = true;
                return result;
            }
        }
    }
}
