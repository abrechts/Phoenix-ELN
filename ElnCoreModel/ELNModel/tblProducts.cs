using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblProducts
{
    public string GUID { get; set; } = null!;

    public string ProtocolItemID { get; set; } = null!;

    public short ProductIndex { get; set; }

    public string Name { get; set; } = null!;

    public double Grams { get; set; }

    public double MolecularWeight { get; set; }

    public double Yield { get; set; }

    public double? ExactMass { get; set; }

    public string? ElementalFormula { get; set; }

    public double? Purity { get; set; }

    public double? ResinLoad { get; set; }

    public string? BatchID { get; set; }

    public string? InChIKey { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblProtocolItems ProtocolItem { get; set; } = null!;
}
