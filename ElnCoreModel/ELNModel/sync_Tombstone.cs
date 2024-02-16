using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class sync_Tombstone
{
    public string GUID { get; set; } = null!;

    public string DatabaseInfoID { get; set; } = null!;

    public string TableName { get; set; } = null!;

    public string PrimaryKeyVal { get; set; } = null!;

    public byte? SyncState { get; set; }

    public virtual tblDatabaseInfo DatabaseInfo { get; set; } = null!;
}
