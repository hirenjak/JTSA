using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace JTSA.Utility;

public sealed class BatchObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> values)
    {
        var replacement = values.ToList();
        if (this.SequenceEqual(replacement)) return;
        CheckReentrancy();
        Items.Clear();
        foreach (var item in replacement) Items.Add(item);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
