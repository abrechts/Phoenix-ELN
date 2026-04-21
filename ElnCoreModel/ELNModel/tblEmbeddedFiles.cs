using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblEmbeddedFiles
{
    public string GUID { get; set; } = null!;

    public string ProtocolItemID { get; set; } = null!;

    public int FileType { get; set; }

    public string FileName { get; set; } = null!;

    public byte[] FileBytes { get; set; } = null!;

    public double? FileSizeMB { get; set; }

    public string FileComment { get; set; } = null!;

    public string? SHA256Hash { get; set; }

    public byte[]? IconImage { get; set; }

    public byte? IsPortraitMode { get; set; }

    public byte? IsRotated { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblProtocolItems ProtocolItem { get; set; } = null!;
}
