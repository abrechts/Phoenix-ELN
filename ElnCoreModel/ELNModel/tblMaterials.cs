using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblMaterials
{
    public string GUID { get; set; } = null!;

    public string DatabaseID { get; set; } = null!;

    public string MatName { get; set; } = null!;

    public string? MatSource { get; set; }

    public short MatType { get; set; }

    public double? Molweight { get; set; }

    public double? Density { get; set; }

    public double? Purity { get; set; }

    public double? Molarity { get; set; }

    public string? InChIKey { get; set; }

    public byte? IsValidated { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblDatabaseInfo Database { get; set; } = null!;
}
