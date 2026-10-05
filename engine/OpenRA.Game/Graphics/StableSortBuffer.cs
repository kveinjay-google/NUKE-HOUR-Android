// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, licensed under the GNU General Public License v3.

using System;
using System.Collections.Generic;

namespace OpenRA.Graphics
{
	// Keeps source order for equal keys without allocating LINQ's element/key/index
	// arrays every frame. One buffer belongs to one renderer; Clear releases items.
	sealed class StableSortBuffer<T>
	{
		long[] keys = Array.Empty<long>();
		T[] items = Array.Empty<T>();
		int count;
		internal int Count => count;
		internal T this[int index] => items[index];

		internal void Sort(IReadOnlyList<T> source, Func<T, int> key)
		{
			Clear();
			var sourceCount = source.Count;
			if (items.Length < sourceCount)
			{
				var capacity = Math.Max(sourceCount, items.Length * 2);
				keys = new long[capacity];
				items = new T[capacity];
			}

			// LINQ snapshots the whole source before calling any keys. Preserve that
			// sequence even if a key callback changes the original source list.
			for (var i = 0; i < sourceCount; i++)
			{
				items[i] = source[i];
				count++;
			}

			// The signed key occupies the high word, the source ordinal the low word.
			// Thus even an unstable numeric sort preserves input order for equal keys.
			// Evaluate each key once, in source order, before any preparation.
			for (var i = 0; i < count; i++)
				keys[i] = ((long)key(items[i]) << 32) | (uint)i;

			Array.Sort(keys, items, 0, count);
		}

		internal void Clear()
		{
			Array.Clear(items, 0, count);
			count = 0;
		}
	}
}
