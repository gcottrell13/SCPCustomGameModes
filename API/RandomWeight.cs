using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomGameModes.API
{
    internal class RandomWeight<T> : Dictionary<int, T>
    {
        public IEnumerable<(int key, T item)> Pairs()
        {
            foreach (var kvp in this) yield return (kvp.Key, kvp.Value);
        }

        public T GetRandom()
        {
            var total = Keys.Sum();
            var chosenValue = UnityEngine.Random.Range(1, total);
            foreach (var (key, value) in this.Pairs())
            {
                total -= key;
                if (chosenValue > total) // not GTE since we are subtracting from total before this check.
                {
                    return value;
                }
            }
            return default;
        }
    }
}
