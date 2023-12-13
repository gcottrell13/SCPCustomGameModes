using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomGameModes.API
{
    public static class CollectionExtensions
    {
        public static T RandomChoice<T>(this IList<T> collection)
        {
            return collection[UnityEngine.Random.Range(0, collection.Count)];
        }

        /// <summary>
        /// Draws a random item from the pool. If the predicate returns false, discards the item and draws again.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pool"></param>
        /// <param name="predicateToRemove"></param>
        /// <returns></returns>
        public static T Pool<T>(this IList<T> pool, Func<T, bool> predicateToRemove)
        {
            pool = pool.ToList();
            while (pool.Count > 0)
            {
                var choice = pool.RandomChoice();
                if (predicateToRemove(choice))
                {
                    return choice;
                }
                pool.Remove(choice);
            }
            return default;
        }
    }
}
