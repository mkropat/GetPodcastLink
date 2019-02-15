using System;
using System.Collections.Generic;
using System.Linq;

namespace GetPodcastLink.Helpers
{
    internal static class LinqExtensions
    {
        public static IEnumerable<T> TakeFromEnd<T>(this IEnumerable<T> items, int count)
        {
            var total = items.Count();
            return items.Skip(Math.Max(0, total - count));
        }
    }
}