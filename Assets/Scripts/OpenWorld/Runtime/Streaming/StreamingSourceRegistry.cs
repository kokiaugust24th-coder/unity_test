using System.Collections.Generic;
using UnityEngine;

namespace OpenWorld
{
    /// <summary>ストリーミングソースの登録簿。</summary>
    public static class StreamingSourceRegistry
    {
        static readonly List<IStreamingSource> _sources = new List<IStreamingSource>();

        public static IReadOnlyList<IStreamingSource> Sources => _sources;

        public static void Register(IStreamingSource source)
        {
            if (source != null && !_sources.Contains(source))
                _sources.Add(source);
        }

        public static void Unregister(IStreamingSource source)
        {
            _sources.Remove(source);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnDomainReload()
        {
            _sources.Clear();
        }
    }
}
