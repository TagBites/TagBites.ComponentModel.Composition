using System;
using System.Collections.Generic;

namespace TagBites.ComponentModel.Composition;

public sealed class ExportCollectionChangedEventArgs : EventArgs
{
    public ICollection<Type> ChangedContractsTypes { get; }

    public ExportCollectionChangedEventArgs(ICollection<Type> changedContractsTypes)
    {
        ChangedContractsTypes = changedContractsTypes ?? throw new ArgumentNullException(nameof(changedContractsTypes));
    }
}
