using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblDatabaseInfo
{
    public string GUID { get; set; } = null!;

    public string? CurrAppVersion { get; set; }

    public byte[]? CompanyLogo { get; set; }

    public string? LastSyncID { get; set; }

    public DateTime? LastSyncTime { get; set; }

    public byte? SyncState { get; set; }

    public virtual ICollection<sync_Tombstone> sync_Tombstone { get; set; } = new List<sync_Tombstone>();

    public virtual ICollection<tblMaterials> tblMaterials { get; set; } = new List<tblMaterials>();

    public virtual ICollection<tblUsers> tblUsers { get; set; } = new List<tblUsers>();
}
