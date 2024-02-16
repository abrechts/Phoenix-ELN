using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblReagents
{
    public string GUID { get; set; } = null!;

    public string ProtocolItemID { get; set; } = null!;

    public int SpecifiedUnitType { get; set; }

    public byte IsDisplayAsVolume { get; set; }

    public double? MolecularWeight { get; set; }

    public string Name { get; set; } = null!;

    public string? Source { get; set; }

    public double Grams { get; set; }

    public double MMols { get; set; }

    public double Equivalents { get; set; }

    public double? Density { get; set; }

    public double? Purity { get; set; }

    public double? Molarity { get; set; }

    public byte IsMolarity { get; set; }

    public double? ResinLoad { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblProtocolItems ProtocolItem { get; set; } = null!;
}
