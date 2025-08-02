using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MobileApp;

public static class ObservableCollectionExtensions
{
    public static void AddRange<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
