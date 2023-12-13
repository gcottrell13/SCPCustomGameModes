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

        public static T? Pool<T>(this IList<T> pool)
        {
            throw new NotImplementedException();
        }
    }
}
