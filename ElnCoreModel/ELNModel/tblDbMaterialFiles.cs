using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblDbMaterialFiles
{
    public string GUID { get; set; } = null!;

    public string DbMaterialID { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public byte[] FileBytes { get; set; } = null!;

    public double? FileSizeMB { get; set; }

    public byte[]? IconImage { get; set; }

    public int? SyncState { get; set; }

    public virtual tblMaterials DbMaterial { get; set; } = null!;
}
